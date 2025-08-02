// CoreLogic/ScriptManager.cs
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using TelegramClient;
using CoreLogic.Models;

namespace CoreLogic
{
    public class ScriptManager : IDisposable
    {
        private readonly TelegramService _telegramService;

        public event Action<string> OnStatusChanged;
        public event Action<string> OnError;

        // Предоставляем доступ к TelegramService для подписки на события
        public TelegramService TelegramService => _telegramService;

        public ScriptManager()
        {
            _telegramService = new TelegramService();
            
            // Перенаправляем события от TelegramService
            _telegramService.OnStatusChanged += status => OnStatusChanged?.Invoke(status);
            _telegramService.OnError += error => OnError?.Invoke(error);
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
            return scriptType switch
            {
                "no_reply" => BuildNoReplyMessage(parameters),
                "first_message" => BuildFirstMessage(parameters),
                _ => throw new ArgumentException($"Неизвестный тип скрипта: {scriptType}")
            };
        }

        private string BuildNoReplyMessage(Dictionary<string, string> parameters)
        {
            var name = parameters.GetValueOrDefault("Имя", "");
            
            return $"Привет, {name}! 👋\n\n" +
                   "Я заметил, что ты не ответил на мое предыдущее сообщение. " +
                   "Возможно, ты был занят или не заметил его.\n\n" +
                   "Если у тебя есть время, буду рад продолжить наше общение! 😊";
        }

        private string BuildFirstMessage(Dictionary<string, string> parameters)
        {
            var name = parameters.GetValueOrDefault("Имя", "");
            var date = parameters.GetValueOrDefault("Дата", DateTime.Now.ToString("dd.MM.yyyy"));
            
            return $"Привет, {name}! 👋\n\n" +
                   $"Сегодня {date}, и я решил написать тебе первое сообщение!\n\n" +
                   "Надеюсь, у тебя отличное настроение, и мы сможем хорошо пообщаться! 😊\n\n" +
                   "Как дела? Чем занимаешься?";
        }

        public void Dispose()
        {
            _telegramService?.Dispose();
        }
    }
}