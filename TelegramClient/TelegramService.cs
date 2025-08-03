// TelegramClient/TelegramService.cs
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using TL;
using WTelegram;

namespace TelegramClient
{
    public class TelegramAccount
    {
        public string Name { get; set; }
        public string ApiId { get; set; }
        public string ApiHash { get; set; }
        public string PhoneNumber { get; set; }
        public Client Client { get; set; }
        public bool IsConnected { get; set; }
    }

    public class DialogInfo
    {
        public string Username { get; set; }
        public long ChatId { get; set; }
        public string Title { get; set; }
        public TelegramAccount Account { get; set; }
        public bool IsFound { get; set; }
        public ChatBase Chat { get; set; }
        public User User { get; set; } // Добавляем отдельное поле для пользователя
        public bool IsUser => User != null; // Флаг для определения типа диалога
    }

    public class TelegramService
    {
        private readonly List<TelegramAccount> _accounts = new();
        private readonly Dictionary<string, Client> _clients = new();

        public event Action<string> OnStatusChanged;
        public event Action<string> OnError;
        public event Func<string, string> OnVerificationCodeRequested;
        public event Func<string, string> OnPasswordRequested;

        public async Task<bool> AddAccountAsync(string name, string apiId, string apiHash, string phoneNumber)
        {
            try
            {
                var account = new TelegramAccount
                {
                    Name = name,
                    ApiId = apiId,
                    ApiHash = apiHash,
                    PhoneNumber = phoneNumber
                };

                OnStatusChanged?.Invoke($"Подключение к аккаунту {name} ({phoneNumber})...");

                // Создаем функцию конфигурации
                Func<string, string> configFunc = what => what switch
                {
                    "api_id" => apiId,
                    "api_hash" => apiHash,
                    "phone_number" => phoneNumber,
                    "session_pathname" => GetSessionPath(phoneNumber),
                    _ => null
                };

                var client = new Client(configFunc);

                // Выполняем авторизацию пошагово
                var loginState = await client.Login(phoneNumber);
                
                while (loginState != null)
                {
                    switch (loginState)
                    {
                        case "verification_code":
                            OnStatusChanged?.Invoke($"Запрос кода подтверждения для {phoneNumber}");
                            var code = OnVerificationCodeRequested?.Invoke(phoneNumber);
                            
                            if (string.IsNullOrWhiteSpace(code))
                            {
                                OnError?.Invoke("Код подтверждения не был введен");
                                return false;
                            }
                            
                            OnStatusChanged?.Invoke("Код подтверждения получен, проверяем...");
                            loginState = await client.Login(code.Trim());
                            break;

                        case "password":
                            OnStatusChanged?.Invoke($"Запрос пароля 2FA для {phoneNumber}");
                            var password = OnPasswordRequested?.Invoke(phoneNumber);
                            
                            if (string.IsNullOrWhiteSpace(password))
                            {
                                OnError?.Invoke("Пароль 2FA не был введен");
                                return false;
                            }
                            
                            OnStatusChanged?.Invoke("Пароль 2FA получен, проверяем...");
                            loginState = await client.Login(password);
                            break;

                        case "name":
                            // Если требуется имя пользователя (новый аккаунт)
                            OnStatusChanged?.Invoke("Требуется ввод имени пользователя");
                            loginState = await client.Login("User"); // Можно запросить через UI
                            break;

                        default:
                            OnError?.Invoke($"Неожиданное состояние авторизации: {loginState}");
                            return false;
                    }
                }

                // Проверяем, что авторизация прошла успешно
                var me = await client.Users_GetUsers(new[] { new InputUserSelf() });
                if (me?.Length > 0)
                {
                    account.Client = client;
                    account.IsConnected = true;
                    
                    _accounts.Add(account);
                    _clients[name] = client;
                    
                    OnStatusChanged?.Invoke($"Аккаунт {name} ({phoneNumber}) подключен успешно");
                    return true;
                }
                else
                {
                    OnError?.Invoke("Не удалось получить информацию о пользователе после авторизации");
                    return false;
                }
            }
            catch (Exception ex)
            {
                OnError?.Invoke($"Ошибка подключения аккаунта {name}: {ex.Message}");
                return false;
            }
        }

        private string GetSessionPath(string phoneNumber)
        {
            // Создаем путь к файлу сессии в папке Sessions
            var baseDirectory = AppDomain.CurrentDomain.BaseDirectory;
            var sessionsDirectory = System.IO.Path.Combine(baseDirectory, "Sessions");
            
            // Создаем папку, если она не существует
            if (!System.IO.Directory.Exists(sessionsDirectory))
            {
                System.IO.Directory.CreateDirectory(sessionsDirectory);
            }
            
            // Убираем символы, которые нельзя использовать в имени файла
            var cleanPhoneNumber = phoneNumber.Replace("+", "").Replace(" ", "").Replace("-", "");
            return System.IO.Path.Combine(sessionsDirectory, $"session_{cleanPhoneNumber}.dat");
        }

        public async Task<DialogInfo> SearchDialogAsync(string username)
        {
            if (string.IsNullOrWhiteSpace(username))
                throw new ArgumentException("Username не может быть пустым");

            username = username.TrimStart('@');

            foreach (var account in _accounts.Where(a => a.IsConnected))
            {
                try
                {
                    OnStatusChanged?.Invoke($"Поиск @{username} в аккаунте {account.Name}...");

                    var dialogs = await account.Client.Messages_GetAllDialogs();
                    
                    // Сначала ищем среди пользователей (личные диалоги)
                    foreach (var (userId, user) in dialogs.users)
                    {
                        if (!string.IsNullOrEmpty(user.username) && 
                            user.username.Equals(username, StringComparison.OrdinalIgnoreCase))
                        {
                            OnStatusChanged?.Invoke($"Диалог с пользователем @{username} найден в аккаунте {account.Name}");
                            
                            return new DialogInfo
                            {
                                Username = username,
                                ChatId = userId,
                                Title = $"{user.first_name} {user.last_name}".Trim(),
                                Account = account,
                                IsFound = true,
                                Chat = null, // Для пользователей Chat = null
                                User = user  // Сохраняем пользователя в отдельном поле
                            };
                        }
                    }
                    
                    // Если не найден среди пользователей, ищем среди каналов и групп
                    foreach (var (chatId, chat) in dialogs.chats)
                    {
                        string chatUsername = null;
                        string chatTitle = null;

                        // Проверяем конкретные типы чатов
                        switch (chat)
                        {
                            case Channel channel:
                                chatUsername = channel.username;
                                chatTitle = channel.title;
                                break;
                            case Chat groupChat:
                                // У обычных групп обычно нет username, но проверим
                                chatTitle = groupChat.title;
                                break;
                        }

                        if (!string.IsNullOrEmpty(chatUsername) && 
                            chatUsername.Equals(username, StringComparison.OrdinalIgnoreCase))
                        {
                            OnStatusChanged?.Invoke($"Диалог @{username} найден в аккаунте {account.Name} (канал/группа)");
                            
                            return new DialogInfo
                            {
                                Username = username,
                                ChatId = chatId,
                                Title = chatTitle,
                                Account = account,
                                IsFound = true,
                                Chat = chat,
                                User = null // Для каналов/групп User = null
                            };
                        }
                    }
                }
                catch (Exception ex)
                {
                    OnError?.Invoke($"Ошибка поиска в аккаунте {account.Name}: {ex.Message}");
                }
            }

            OnStatusChanged?.Invoke($"Диалог @{username} не найден ни в одном аккаунте");
            return new DialogInfo
            {
                Username = username,
                IsFound = false
            };
        }

        private InputPeer CreateInputPeer(DialogInfo dialogInfo)
        {
            // Если это пользователь
            if (dialogInfo.IsUser)
            {
                return new InputPeerUser(dialogInfo.ChatId, dialogInfo.User.access_hash);
            }
            
            // Если это канал или группа
            switch (dialogInfo.Chat)
            {
                case Channel channel:
                    return new InputPeerChannel(dialogInfo.ChatId, channel.access_hash);
                case Chat groupChat:
                    return new InputPeerChat(dialogInfo.ChatId);
                default:
                    throw new InvalidOperationException($"Не удалось создать InputPeer для chatId: {dialogInfo.ChatId}");
            }
        }

        public async Task<bool> SendMessageAsync(DialogInfo dialogInfo, string message)
        {
            if (dialogInfo?.Account?.Client == null || !dialogInfo.IsFound)
            {
                OnError?.Invoke("Невозможно отправить сообщение: диалог не найден или аккаунт не подключен");
                return false;
            }

            try
            {
                OnStatusChanged?.Invoke($"Отправка сообщения в диалог @{dialogInfo.Username}...");

                // Создаем InputPeer на основе типа диалога
                InputPeer peer = CreateInputPeer(dialogInfo);
                
                await dialogInfo.Account.Client.SendMessageAsync(peer, message);
                
                OnStatusChanged?.Invoke($"Сообщение успешно отправлено в диалог @{dialogInfo.Username}");
                return true;
            }
            catch (Exception ex)
            {
                OnError?.Invoke($"Ошибка отправки сообщения: {ex.Message}");
                return false;
            }
        }

        public async Task<bool> SendMessageToUsernameAsync(string username, string message)
        {
            try
            {
                username = username.TrimStart('@');

                foreach (var account in _accounts.Where(a => a.IsConnected))
                {
                    try
                    {
                        OnStatusChanged?.Invoke($"Попытка отправить сообщение @{username} через аккаунт {account.Name}");
                        
                        // Попытка найти пользователя через поиск
                        var resolved = await account.Client.Contacts_ResolveUsername(username);
                        
                        if (resolved.users.Count > 0)
                        {
                            var user = resolved.users.Values.First();
                            
                            // Создание InputPeerUser
                            InputPeer peer = new InputPeerUser(user.ID, user.access_hash);
                            
                            await account.Client.SendMessageAsync(peer, message);
                            
                            OnStatusChanged?.Invoke($"Сообщение отправлено @{username} через аккаунт {account.Name}");
                            return true;
                        }
                    }
                    catch (Exception ex)
                    {
                        OnError?.Invoke($"Ошибка отправки через аккаунт {account.Name}: {ex.Message}");
                    }
                }

                OnError?.Invoke($"Не удалось отправить сообщение @{username} ни через один аккаунт");
                return false;
            }
            catch (Exception ex)
            {
                OnError?.Invoke($"Общая ошибка отправки сообщения: {ex.Message}");
                return false;
            }
        }

        public List<TelegramAccount> GetAccounts()
        {
            return _accounts.ToList();
        }

        public async Task<bool> DisconnectAccountAsync(string accountName)
        {
            try
            {
                var account = _accounts.FirstOrDefault(a => a.Name == accountName);
                if (account == null)
                {
                    OnError?.Invoke($"Аккаунт {accountName} не найден");
                    return false;
                }

                if (_clients.TryGetValue(accountName, out var client))
                {
                    client?.Dispose();
                    _clients.Remove(accountName);
                }

                account.IsConnected = false;
                account.Client = null;
                _accounts.Remove(account);
                OnStatusChanged?.Invoke($"Аккаунт {accountName} отключен");
                return true;
            }
            catch (Exception ex)
            {
                OnError?.Invoke($"Ошибка отключения аккаунта {accountName}: {ex.Message}");
                return false;
            }
        }

        public void Dispose()
        {
            try
            {
                foreach (var client in _clients.Values)
                {
                    client?.Dispose();
                }
                _clients.Clear();
                
                foreach (var account in _accounts)
                {
                    account.IsConnected = false;
                    account.Client = null;
                }
                _accounts.Clear();
            }
            catch (Exception ex)
            {
                OnError?.Invoke($"Ошибка при освобождении ресурсов: {ex.Message}");
            }
        }
    }
}