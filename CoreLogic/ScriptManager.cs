// CoreLogic/ScriptManager.cs
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.IO;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Linq;
using TelegramClient;
using CoreLogic.Models;

namespace CoreLogic
{
    public class ScriptManager : IDisposable
    {
        private readonly TelegramService _telegramService;
        private readonly string _scriptsFilePath;
        private Dictionary<string, MessageTemplate> _messageTemplates;

        public event Action<string> OnStatusChanged;
        public event Action<string> OnError;

        // Предоставляем доступ к TelegramService для подписки на события
        public TelegramService TelegramService => _telegramService;

        public ScriptManager(string scriptsFilePath = "messageScripts.json")
        {
            _telegramService = new TelegramService();
            _scriptsFilePath = scriptsFilePath;
            
            // Перенаправляем события от TelegramService
            _telegramService.OnStatusChanged += status => OnStatusChanged?.Invoke(status);
            _telegramService.OnError += error => OnError?.Invoke(error);
            
            // Загружаем шаблоны сообщений при инициализации
            LoadMessageTemplates();
        }

        private void LoadMessageTemplates()
        {
            try
            {
                if (File.Exists(_scriptsFilePath))
                {
                    var json = File.ReadAllText(_scriptsFilePath);
                    var simpleTemplates = JsonSerializer.Deserialize<Dictionary<string, string>>(json) 
                                        ?? new Dictionary<string, string>();
                    
                    // Конвертируем простые шаблоны в MessageTemplate объекты
                    _messageTemplates = new Dictionary<string, MessageTemplate>();
                    foreach (var kvp in simpleTemplates)
                    {
                        _messageTemplates[kvp.Key] = new MessageTemplate
                        {
                            Name = kvp.Key,
                            Template = kvp.Value,
                            Parameters = ExtractParametersFromTemplate(kvp.Value),
                            Description = GetTemplateDescription(kvp.Key)
                        };
                    }
                }
                else
                {
                    _messageTemplates = new Dictionary<string, MessageTemplate>();
                    OnError?.Invoke($"Файл шаблонов сообщений не найден: {_scriptsFilePath}");
                }
            }
            catch (Exception ex)
            {
                _messageTemplates = new Dictionary<string, MessageTemplate>();
                OnError?.Invoke($"Ошибка загрузки шаблонов сообщений: {ex.Message}");
            }
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
            if (_messageTemplates == null)
            {
                LoadMessageTemplates();
            }

            if (_messageTemplates.TryGetValue(scriptType, out var messageTemplate))
            {
                return messageTemplate.Template;
            }

            throw new ArgumentException($"Шаблон для скрипта '{scriptType}' не найден в файле {_scriptsFilePath}");
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
            LoadMessageTemplates();
        }

        // Метод для получения доступных типов скриптов
        public IEnumerable<string> GetAvailableScriptTypes()
        {
            return _messageTemplates?.Keys ?? Enumerable.Empty<string>();
        }

        // Метод для получения информации о шаблоне
        public MessageTemplate GetMessageTemplate(string scriptType)
        {
            if (_messageTemplates == null)
            {
                LoadMessageTemplates();
            }

            return _messageTemplates.TryGetValue(scriptType, out var template) ? template : null;
        }

        // Метод для получения всех шаблонов
        public IEnumerable<MessageTemplate> GetAllMessageTemplates()
        {
            if (_messageTemplates == null)
            {
                LoadMessageTemplates();
            }

            return _messageTemplates?.Values ?? Enumerable.Empty<MessageTemplate>();
        }

        // Извлекает параметры из шаблона (все что в фигурных скобках)
        private string[] ExtractParametersFromTemplate(string template)
        {
            if (string.IsNullOrEmpty(template))
                return new string[0];

            var regex = new Regex(@"\{([^}]+)\}");
            var matches = regex.Matches(template);
            
            return matches.Cast<Match>()
                          .Select(m => m.Groups[1].Value)
                          .Distinct()
                          .ToArray();
        }

        // Возвращает описание шаблона на основе его типа
        private string GetTemplateDescription(string scriptType)
        {
            return scriptType switch
            {
                "no_reply" => "Сообщение для напоминания о неотвеченном сообщении",
                "first_message" => "Первое сообщение для начала общения",
                "reminder" => "Напоминание о встрече или событии",
                "follow_up" => "Последующее сообщение по договоренности",
                "custom_greeting" => "Персонализированное приветствие",
                _ => $"Шаблон сообщения типа '{scriptType}'"
            };
        }

        public void Dispose()
        { 
            _telegramService?.Dispose();
        }
    }
}