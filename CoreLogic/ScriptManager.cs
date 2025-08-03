// CoreLogic/ScriptManager.cs
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Text.RegularExpressions;
using TelegramClient;
using CoreLogic.Models;

namespace CoreLogic
{
    public class ScriptManager : IDisposable
    {
        private readonly TelegramService _telegramService;
        private readonly MessageTemplateManager _templateManager;

        public event Action<string> OnStatusChanged;
        public event Action<string> OnError;

        // Предоставляем доступ к TelegramService для подписки на события
        public TelegramService TelegramService => _telegramService;

        // Предоставляем доступ к MessageTemplateManager
        public MessageTemplateManager TemplateManager => _templateManager;

        public ScriptManager(string scriptsFilePath = "messageScripts.json")
        {
            _telegramService = new TelegramService();
            _templateManager = new MessageTemplateManager(scriptsFilePath);
            
            // Перенаправляем события от TelegramService
            _telegramService.OnStatusChanged += status => OnStatusChanged?.Invoke(status);
            _telegramService.OnError += error => OnError?.Invoke(error);
            
            // Перенаправляем события от TemplateManager
            _templateManager.OnError += error => OnError?.Invoke(error);
        }

        public async Task<bool> ConnectAccountAsync(AccountConnectionRequest request)
        {
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

        public async Task<DialogInfo> SearchDialogAsync(string username)
        {
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

        public async Task<ScriptResult> SendScriptToFoundDialogAsync(DialogInfo dialogInfo, string scriptType, Dictionary<string, string> parameters)
        {
            try
            {
                var message = BuildMessage(scriptType, parameters);
                var success = await _telegramService.SendMessageAsync(dialogInfo, message);
                
                return new ScriptResult
                {
                    Success = success,
                    ErrorMessage = success ? null : "Не удалось отправить сообщение"
                };
            }
            catch (Exception ex)
            {
                OnError?.Invoke($"Ошибка отправки сообщения: {ex.Message}");
                return new ScriptResult
                {
                    Success = false,
                    ErrorMessage = ex.Message
                };
            }
        }

        public async Task<ScriptResult> SendScriptAsync(string scriptType, string username, Dictionary<string, string> parameters)
        {
            try
            {
                var message = BuildMessage(scriptType, parameters);
                var success = await _telegramService.SendMessageToUsernameAsync(username, message);
                
                return new ScriptResult
                {
                    Success = success,
                    ErrorMessage = success ? null : "Не удалось отправить сообщение"
                };
            }
            catch (Exception ex)
            {
                OnError?.Invoke($"Ошибка отправки сообщения: {ex.Message}");
                return new ScriptResult
                {
                    Success = false,
                    ErrorMessage = ex.Message
                };
            }
        }

        // НОВЫЙ МЕТОД: Отправка сообщения с выбранного аккаунта
        public async Task<ScriptResult> SendScriptFromAccountAsync(string scriptType, string username, Dictionary<string, string> parameters, TelegramAccount selectedAccount)
        {
            try
            {
                var message = BuildMessage(scriptType, parameters);
                var success = await _telegramService.SendMessageFromConnectedAccountToUsername(selectedAccount, username, message);
                
                return new ScriptResult
                {
                    Success = success,
                    ErrorMessage = success ? null : "Не удалось отправить сообщение"
                };
            }
            catch (Exception ex)
            {
                OnError?.Invoke($"Ошибка отправки сообщения: {ex.Message}");
                return new ScriptResult
                {
                    Success = false,
                    ErrorMessage = ex.Message
                };
            }
        }

        // НОВЫЙ МЕТОД: Отправка сообщения найденному диалогу с выбранного аккаунта
        public async Task<ScriptResult> SendScriptToFoundDialogFromAccountAsync(DialogInfo dialogInfo, string scriptType, Dictionary<string, string> parameters, TelegramAccount selectedAccount)
        {
            try
            {
                var message = BuildMessage(scriptType, parameters);
                var success = await _telegramService.SendMessageFromConnectedAccountToUsername(selectedAccount, dialogInfo.Username, message);
                
                return new ScriptResult
                {
                    Success = success,
                    ErrorMessage = success ? null : "Не удалось отправить сообщение"
                };
            }
            catch (Exception ex)
            {
                OnError?.Invoke($"Ошибка отправки сообщения: {ex.Message}");
                return new ScriptResult
                {
                    Success = false,
                    ErrorMessage = ex.Message
                };
            }
        }

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

        private string BuildMessage(string scriptType, Dictionary<string, string> parameters)
        {
            try
            {
                var template = LoadMessageTemplate(scriptType);
                return CreateMessage(template, parameters);
            }
            catch (Exception ex)
            {
                throw new ArgumentException($"Ошибка создания сообщения для скрипта '{scriptType}': {ex.Message}");
            }
        }

        private string LoadMessageTemplate(string scriptType)
        {
            var messageTemplate = _templateManager.GetTemplate(scriptType);
            if (messageTemplate != null)
            {
                return messageTemplate.Template;
            }

            throw new ArgumentException($"Шаблон для скрипта '{scriptType}' не найден");
        }

        private string CreateMessage(string template, Dictionary<string, string> parameters)
        {
            if (string.IsNullOrEmpty(template))
            {
                throw new ArgumentException("Шаблон сообщения не может быть пустым");
            }

            var result = template;

            // Используем регулярное выражение для поиска всех плейсхолдеров вида {Параметр}
            var regex = new Regex(@"\{([^}]+)\}");
            var matches = regex.Matches(template);

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
                    // Если параметр не найден, заменяем на пустую строку или оставляем как есть
                    // В зависимости от требований можно выбросить исключение
                    result = result.Replace(placeholder, $"[{parameterName}]");
                }
            }

            return result;
        }

        public async Task<bool> DisconnectAccountAsync(string accountName)
        {
            return await TelegramService.DisconnectAccountAsync(accountName);
        }

        // Метод для перезагрузки шаблонов (может быть полезен если файл изменился)
        public void ReloadMessageTemplates()
        {
            _templateManager.ReloadTemplates();
        }

        // Метод для получения доступных типов скриптов
        public IEnumerable<string> GetAvailableScriptTypes()
        {
            return _templateManager.GetTemplateNames();
        }

        // Метод для получения информации о шаблоне
        public MessageTemplate GetMessageTemplate(string scriptType)
        {
            return _templateManager.GetTemplate(scriptType);
        }

        // Метод для получения всех шаблонов
        public IEnumerable<MessageTemplate> GetAllMessageTemplates()
        {
            return _templateManager.GetAllTemplates();
        }

        public void Dispose()
        { 
            _telegramService?.Dispose();
        }
    }
}