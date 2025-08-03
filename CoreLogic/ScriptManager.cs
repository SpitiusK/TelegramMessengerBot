// CoreLogic/ScriptManager.cs
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Text.RegularExpressions;
using TelegramClient;
using CoreLogic.Models;
using CoreLogic.Interfaces;

namespace CoreLogic
{
    /// <summary>
    /// Менеджер для выполнения скриптов отправки сообщений в Telegram
    /// </summary>
    public sealed class ScriptManager : IDisposable
    {
        #region Private Fields

        private readonly TelegramService _telegramService;
        private readonly IMessageTemplateManager _templateManager;
        private readonly Regex _parameterRegex;
        private bool _disposed;

        #endregion

        #region Events

        public event Action<string>? OnStatusChanged;
        public event Action<string>? OnError;

        #endregion

        #region Properties

        /// <summary>
        /// Предоставляет доступ к TelegramService для подписки на события
        /// </summary>
        public TelegramService TelegramService => _telegramService;

        /// <summary>
        /// Предоставляет доступ к MessageTemplateManager
        /// </summary>
        public IMessageTemplateManager TemplateManager => _templateManager;

        #endregion

        #region Constructor

        public ScriptManager(string? templatesFilePath = null)
        {
            _telegramService = new TelegramService();
            _templateManager = new MessageTemplateManager(templatesFilePath);
            _parameterRegex = new Regex(@"\{([^}]+)\}", RegexOptions.Compiled);
            
            SubscribeToEvents();
        }

        /// <summary>
        /// Конструктор для внедрения зависимостей (для тестирования)
        /// </summary>
        internal ScriptManager(TelegramService telegramService, IMessageTemplateManager templateManager)
        {
            _telegramService = telegramService ?? throw new ArgumentNullException(nameof(telegramService));
            _templateManager = templateManager ?? throw new ArgumentNullException(nameof(templateManager));
            _parameterRegex = new Regex(@"\{([^}]+)\}", RegexOptions.Compiled);
            
            SubscribeToEvents();
        }

        #endregion

        #region Public Methods

        /// <summary>
        /// Подключает аккаунт Telegram
        /// </summary>
        public async Task<bool> ConnectAccountAsync(AccountConnectionRequest request)
        {
            ValidateConnectionRequest(request);

            try
            {
                return await _telegramService.AddAccountAsync(
                    request.Name,
                    request.ApiId,
                    request.ApiHash,
                    request.PhoneNumber
                );
            }
            catch (Exception ex)
            {
                OnError?.Invoke($"Ошибка подключения аккаунта: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Выполняет поиск диалога по username
        /// </summary>
        public async Task<DialogInfo> SearchDialogAsync(string username)
        {
            if (string.IsNullOrWhiteSpace(username))
            {
                return new DialogInfo { Username = username ?? string.Empty, IsFound = false };
            }

            try
            {
                return await _telegramService.SearchDialogAsync(username);
            }
            catch (Exception ex)
            {
                OnError?.Invoke($"Ошибка поиска диалога: {ex.Message}");
                return new DialogInfo { Username = username, IsFound = false };
            }
        }

        /// <summary>
        /// Отправляет сообщение по скриптовому шаблону найденному диалогу
        /// </summary>
        public async Task<ScriptResult> SendScriptToFoundDialogAsync(
            DialogInfo dialogInfo, 
            string scriptType, 
            Dictionary<string, string> parameters)
        {
            if (dialogInfo is null)
            {
                return CreateErrorResult("Информация о диалоге не может быть null");
            }

            try
            {
                var message = BuildMessage(scriptType, parameters);
                var success = await _telegramService.SendMessageAsync(dialogInfo, message);
                
                return new ScriptResult
                {
                    Success = success,
                    Message = message,
                    ErrorMessage = success ? null : "Не удалось отправить сообщение"
                };
            }
            catch (Exception ex)
            {
                var errorMessage = $"Ошибка отправки сообщения: {ex.Message}";
                OnError?.Invoke(errorMessage);
                return CreateErrorResult(errorMessage);
            }
        }

        /// <summary>
        /// Отправляет сообщение по скриптовому шаблону пользователю по username
        /// </summary>
        public async Task<ScriptResult> SendScriptAsync(
            string scriptType, 
            string username, 
            Dictionary<string, string> parameters)
        {
            if (string.IsNullOrWhiteSpace(username))
            {
                return CreateErrorResult("Username не может быть пустым");
            }

            try
            {
                var message = BuildMessage(scriptType, parameters);
                var success = await _telegramService.SendMessageToUsernameAsync(username, message);
                
                return new ScriptResult
                {
                    Success = success,
                    Message = message,
                    ErrorMessage = success ? null : "Не удалось отправить сообщение"
                };
            }
            catch (Exception ex)
            {
                var errorMessage = $"Ошибка отправки сообщения: {ex.Message}";
                OnError?.Invoke(errorMessage);
                return CreateErrorResult(errorMessage);
            }
        }

        /// <summary>
        /// Отправляет сообщение с выбранного аккаунта пользователю по username
        /// </summary>
        public async Task<ScriptResult> SendScriptFromAccountAsync(
            string scriptType, 
            string username, 
            Dictionary<string, string> parameters, 
            TelegramAccount selectedAccount)
        {
            if (string.IsNullOrWhiteSpace(username))
            {
                return CreateErrorResult("Username не может быть пустым");
            }

            if (selectedAccount is null)
            {
                return CreateErrorResult("Выбранный аккаунт не может быть null");
            }

            try
            {
                var message = BuildMessage(scriptType, parameters);
                var success = await _telegramService.SendMessageFromConnectedAccountToUsername(
                    selectedAccount, username, message);
                
                return new ScriptResult
                {
                    Success = success,
                    Message = message,
                    ErrorMessage = success ? null : "Не удалось отправить сообщение"
                };
            }
            catch (Exception ex)
            {
                var errorMessage = $"Ошибка отправки сообщения: {ex.Message}";
                OnError?.Invoke(errorMessage);
                return CreateErrorResult(errorMessage);
            }
        }

        /// <summary>
        /// Отправляет сообщение найденному диалогу с выбранного аккаунта
        /// </summary>
        public async Task<ScriptResult> SendScriptToFoundDialogFromAccountAsync(
            DialogInfo dialogInfo, 
            string scriptType, 
            Dictionary<string, string> parameters, 
            TelegramAccount selectedAccount)
        {
            if (dialogInfo is null)
            {
                return CreateErrorResult("Информация о диалоге не может быть null");
            }

            if (selectedAccount is null)
            {
                return CreateErrorResult("Выбранный аккаунт не может быть null");
            }

            try
            {
                var message = BuildMessage(scriptType, parameters);
                var success = await _telegramService.SendMessageFromConnectedAccountToUsername(
                    selectedAccount, dialogInfo.Username, message);
                
                return new ScriptResult
                {
                    Success = success,
                    Message = message,
                    ErrorMessage = success ? null : "Не удалось отправить сообщение"
                };
            }
            catch (Exception ex)
            {
                var errorMessage = $"Ошибка отправки сообщения: {ex.Message}";
                OnError?.Invoke(errorMessage);
                return CreateErrorResult(errorMessage);
            }
        }

        /// <summary>
        /// Получает список подключенных аккаунтов
        /// </summary>
        public List<TelegramAccount> GetConnectedAccounts()
        {
            try
            {
                return _telegramService.GetAccounts();
            }
            catch (Exception ex)
            {
                OnError?.Invoke($"Ошибка получения списка аккаунтов: {ex.Message}");
                return new List<TelegramAccount>();
            }
        }

        /// <summary>
        /// Отключает аккаунт
        /// </summary>
        public async Task<bool> DisconnectAccountAsync(string accountName)
        {
            if (string.IsNullOrWhiteSpace(accountName))
            {
                OnError?.Invoke("Имя аккаунта не может быть пустым");
                return false;
            }

            return await _telegramService.DisconnectAccountAsync(accountName);
        }

        /// <summary>
        /// Перезагружает шаблоны сообщений
        /// </summary>
        public void ReloadMessageTemplates()
        {
            _templateManager.ReloadTemplates();
        }

        /// <summary>
        /// Получает доступные типы скриптов
        /// </summary>
        public IReadOnlyList<string> GetAvailableScriptTypes()
        {
            return _templateManager.GetTemplateNames();
        }

        /// <summary>
        /// Получает информацию о шаблоне
        /// </summary>
        public MessageTemplate? GetMessageTemplate(string scriptType)
        {
            return _templateManager.GetTemplate(scriptType);
        }

        /// <summary>
        /// Получает все шаблоны сообщений
        /// </summary>
        public IReadOnlyList<MessageTemplate> GetAllMessageTemplates()
        {
            return _templateManager.GetAllTemplates();
        }

        #endregion

        #region Private Methods

        /// <summary>
        /// Подписывается на события от сервисов
        /// </summary>
        private void SubscribeToEvents()
        {
            // Перенаправляем события от TelegramService
            _telegramService.OnStatusChanged += status => OnStatusChanged?.Invoke(status);
            _telegramService.OnError += error => OnError?.Invoke(error);
            
            // Перенаправляем события от TemplateManager
            _templateManager.OnError += error => OnError?.Invoke(error);
        }

        /// <summary>
        /// Создает сообщение на основе шаблона и параметров
        /// </summary>
        private string BuildMessage(string scriptType, Dictionary<string, string>? parameters)
        {
            if (string.IsNullOrWhiteSpace(scriptType))
            {
                throw new ArgumentException("Тип скрипта не может быть пустым", nameof(scriptType));
            }

            var template = LoadMessageTemplate(scriptType);
            return CreateMessage(template, parameters ?? new Dictionary<string, string>());
        }

        /// <summary>
        /// Загружает шаблон сообщения по типу скрипта
        /// </summary>
        private string LoadMessageTemplate(string scriptType)
        {
            var messageTemplate = _templateManager.GetTemplate(scriptType);
            if (messageTemplate?.Template != null)
            {
                return messageTemplate.Template;
            }

            throw new ArgumentException($"Шаблон для скрипта '{scriptType}' не найден");
        }

        /// <summary>
        /// Создает сообщение, заменяя параметры в шаблоне
        /// </summary>
        private string CreateMessage(string template, Dictionary<string, string> parameters)
        {
            if (string.IsNullOrEmpty(template))
            {
                throw new ArgumentException("Шаблон сообщения не может быть пустым");
            }

            var result = template;
            var matches = _parameterRegex.Matches(template);

            foreach (Match match in matches)
            {
                var placeholder = match.Value; // {Имя}
                var parameterName = match.Groups[1].Value; // Имя

                if (parameters.TryGetValue(parameterName, out var parameterValue))
                {
                    result = result.Replace(placeholder, parameterValue);
                }
                else
                {
                    // Если параметр не найден, оставляем заметку
                    result = result.Replace(placeholder, $"[{parameterName}]");
                }
            }

            return result;
        }

        /// <summary>
        /// Валидирует запрос подключения аккаунта
        /// </summary>
        private static void ValidateConnectionRequest(AccountConnectionRequest request)
        {
            if (request is null)
                throw new ArgumentNullException(nameof(request));

            if (string.IsNullOrWhiteSpace(request.Name))
                throw new ArgumentException("Имя аккаунта не может быть пустым", nameof(request));

            if (string.IsNullOrWhiteSpace(request.ApiId))
                throw new ArgumentException("API ID не может быть пустым", nameof(request));

            if (string.IsNullOrWhiteSpace(request.ApiHash))
                throw new ArgumentException("API Hash не может быть пустым", nameof(request));

            if (string.IsNullOrWhiteSpace(request.PhoneNumber))
                throw new ArgumentException("Номер телефона не может быть пустым", nameof(request));
        }

        /// <summary>
        /// Создает результат с ошибкой
        /// </summary>
        private static ScriptResult CreateErrorResult(string errorMessage)
        {
            return new ScriptResult
            {
                Success = false,
                ErrorMessage = errorMessage
            };
        }

        #endregion

        #region IDisposable Implementation

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        private void Dispose(bool disposing)
        {
            if (!_disposed && disposing)
            {
                _telegramService?.Dispose();
                _disposed = true;
            }
        }

        #endregion
    }
}