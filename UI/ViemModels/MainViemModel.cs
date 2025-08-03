// UI/ViewModels/MainViewModel.cs
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using System.Windows.Threading;
using CoreLogic;
using CoreLogic.Models;
using TelegramClient;
using UI.Views;

namespace UI.ViewModels
{
    public class RelayCommand : ICommand
    {
        private readonly Action<object> _execute;
        private readonly Func<object, bool> _canExecute;

        public RelayCommand(Action<object> execute, Func<object, bool> canExecute = null)
        {
            _execute = execute ?? throw new ArgumentNullException(nameof(execute));
            _canExecute = canExecute;
        }

        public event EventHandler CanExecuteChanged
        {
            add { CommandManager.RequerySuggested += value; }
            remove { CommandManager.RequerySuggested -= value; }
        }

        public bool CanExecute(object parameter) => _canExecute?.Invoke(parameter) ?? true;
        public void Execute(object parameter) => _execute(parameter);
    }

    public class AsyncRelayCommand : ICommand
    {
        private readonly Func<object, Task> _execute;
        private readonly Func<object, bool> _canExecute;
        private bool _isExecuting;

        public AsyncRelayCommand(Func<object, Task> execute, Func<object, bool> canExecute = null)
        {
            _execute = execute ?? throw new ArgumentNullException(nameof(execute));
            _canExecute = canExecute;
        }

        public event EventHandler CanExecuteChanged
        {
            add { CommandManager.RequerySuggested += value; }
            remove { CommandManager.RequerySuggested -= value; }
        }

        public bool CanExecute(object parameter)
        {
            return !_isExecuting && (_canExecute?.Invoke(parameter) ?? true);
        }

        public async void Execute(object parameter)
        {
            _isExecuting = true;
            CommandManager.InvalidateRequerySuggested();

            try
            {
                await _execute(parameter);
            }
            finally
            {
                _isExecuting = false;
                CommandManager.InvalidateRequerySuggested();
            }
        }
    }

    public class MainViewModel : INotifyPropertyChanged
    {
        private readonly ScriptManager _scriptManager;
        private readonly Dispatcher _dispatcher;
        private DialogInfo _currentDialog;

        #region INotifyPropertyChanged Implementation

        public event PropertyChangedEventHandler PropertyChanged;

        protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        #endregion

        #region Properties

        private string _username = "";
        public string Username
        {
            get => _username;
            set
            {
                _username = value;
                OnPropertyChanged();
                IsDialogFound = false;
                IsDialogNotFound = false;
            }
        }

        private string _name = "";
        public string Name
        {
            get => _name;
            set
            {
                _name = value;
                OnPropertyChanged();
            }
        }

        private string _date = DateTime.Now.ToString("dd.MM.yyyy");
        public string Date
        {
            get => _date;
            set
            {
                _date = value;
                OnPropertyChanged();
            }
        }

        private bool _isDialogFound;
        public bool IsDialogFound
        {
            get => _isDialogFound;
            set
            {
                _isDialogFound = value;
                OnPropertyChanged();
            }
        }

        private bool _isDialogNotFound;
        public bool IsDialogNotFound
        {
            get => _isDialogNotFound;
            set
            {
                _isDialogNotFound = value;
                OnPropertyChanged();
            }
        }

        private string _statusMessage = "Готов к работе";
        public string StatusMessage
        {
            get => _statusMessage;
            set
            {
                _statusMessage = value;
                OnPropertyChanged();
            }
        }

        private bool _isLoading;
        public bool IsLoading
        {
            get => _isLoading;
            set
            {
                _isLoading = value;
                OnPropertyChanged();
            }
        }

        // Account Connection Properties
        private string _accountName = "";
        public string AccountName
        {
            get => _accountName;
            set
            {
                _accountName = value;
                OnPropertyChanged();
            }
        }

        private string _apiId = "14261180";
        public string ApiId
        {
            get => _apiId;
            set
            {
                _apiId = value;
                OnPropertyChanged();
            }
        }

        private string _apiHash = "b333eb50e310c7774cd0adc6d18a0875";
        public string ApiHash
        {
            get => _apiHash;
            set
            {
                _apiHash = value;
                OnPropertyChanged();
            }
        }

        private string _phoneNumber = "";
        public string PhoneNumber
        {
            get => _phoneNumber;
            set
            {
                _phoneNumber = value;
                OnPropertyChanged();
            }
        }

        // НОВЫЕ СВОЙСТВА: Выбор аккаунта для отправки сообщений
        private TelegramAccount _selectedAccountForNoReply;
        public TelegramAccount SelectedAccountForNoReply
        {
            get => _selectedAccountForNoReply;
            set
            {
                _selectedAccountForNoReply = value;
                OnPropertyChanged();
            }
        }

        private TelegramAccount _selectedAccountForFirstMessage;
        public TelegramAccount SelectedAccountForFirstMessage
        {
            get => _selectedAccountForFirstMessage;
            set
            {
                _selectedAccountForFirstMessage = value;
                OnPropertyChanged();
            }
        }

        public ObservableCollection<TelegramAccount> ConnectedAccounts { get; } = new();
        public ObservableCollection<string> StatusLog { get; } = new();

        #endregion

        #region Commands

        public ICommand SearchDialogCommand { get; }
        public ICommand SendNoReplyCommand { get; }
        public ICommand SendFirstMessageCommand { get; }
        public ICommand ConnectAccountCommand { get; }
        public ICommand RefreshAccountsCommand { get; }
        public ICommand ClearStatusLogCommand { get; }
        public ICommand DisconnectAccountCommand { get; }

        #endregion

        public MainViewModel()
        {
            // Сохраняем Dispatcher для выполнения операций в UI потоке
            _dispatcher = Dispatcher.CurrentDispatcher;
            
            _scriptManager = new ScriptManager();

            // Подписываемся на события
            _scriptManager.OnStatusChanged += OnStatusChanged;
            _scriptManager.OnError += OnError;
            
            // Подписываемся на события для диалогов ввода
            _scriptManager.TelegramService.OnVerificationCodeRequested += OnVerificationCodeRequested;
            _scriptManager.TelegramService.OnPasswordRequested += OnPasswordRequested;

            // Инициализируем команды
            SearchDialogCommand = new AsyncRelayCommand(SearchDialogAsync, CanExecuteSearch);
            SendNoReplyCommand = new AsyncRelayCommand(SendNoReplyAsync, CanExecuteNoReply);
            SendFirstMessageCommand = new AsyncRelayCommand(SendFirstMessageAsync, CanExecuteFirstMessage);
            ConnectAccountCommand = new AsyncRelayCommand(ConnectAccountAsync, CanExecuteConnectAccount);
            RefreshAccountsCommand = new RelayCommand(RefreshAccounts);
            ClearStatusLogCommand = new RelayCommand(ClearStatusLog);
            DisconnectAccountCommand = new AsyncRelayCommand(DisconnectAccountAsync, CanExecuteDisconnectAccount);

            // Загружаем подключенные аккаунты
            RefreshAccounts(null);
        }

        #region Event Handlers

        private void OnStatusChanged(string status)
        {
            // Выполняем в UI потоке
            _dispatcher.BeginInvoke(new Action(() =>
            {
                StatusMessage = status;
                StatusLog.Add($"[{DateTime.Now:HH:mm:ss}] {status}");
            }));
        }

        private void OnError(string error)
        {
            // Выполняем в UI потоке
            _dispatcher.BeginInvoke(new Action(() =>
            {
                StatusMessage = $"Ошибка: {error}";
                StatusLog.Add($"[{DateTime.Now:HH:mm:ss}] ОШИБКА: {error}");
            }));
        }

        private string OnVerificationCodeRequested(string phoneNumber)
        {
            string code = null;
            
            // Создаем ManualResetEventSlim для синхронизации
            using var resetEvent = new ManualResetEventSlim(false);
            
            // Выполняем показ диалога в UI потоке и ждем результат
            _dispatcher.Invoke(() =>
            {
                try
                {
                    // Получаем главное окно приложения
                    var mainWindow = Application.Current?.MainWindow;
                    
                    // Показываем диалог
                    code = InputDialog.ShowVerificationCodeDialog(phoneNumber, mainWindow);
                }
                catch (Exception ex)
                {
                    // Логируем ошибку
                    OnError($"Ошибка при показе диалога кода подтверждения: {ex.Message}");
                }
                finally
                {
                    // Сигнализируем о завершении
                    resetEvent.Set();
                }
            });
            
            // Ждем завершения операции в UI потоке
            resetEvent.Wait();
            
            return code;
        }

        private string OnPasswordRequested(string phoneNumber)
        {
            string password = null;
            
            // Создаем ManualResetEventSlim для синхронизации
            using var resetEvent = new ManualResetEventSlim(false);
            
            // Выполняем показ диалога в UI потоке и ждем результат
            _dispatcher.Invoke(() =>
            {
                try
                {
                    // Получаем главное окно приложения
                    var mainWindow = Application.Current?.MainWindow;
                    
                    // Показываем диалог
                    password = InputDialog.ShowPasswordDialog(phoneNumber, mainWindow);
                }
                catch (Exception ex)
                {
                    // Логируем ошибку
                    OnError($"Ошибка при показе диалога пароля 2FA: {ex.Message}");
                }
                finally
                {
                    // Сигнализируем о завершении
                    resetEvent.Set();
                }
            });
            
            // Ждем завершения операции в UI потоке
            resetEvent.Wait();
            
            return password;
        }

        #endregion

        #region Command Implementations

        private async Task SearchDialogAsync(object parameter)
        {
            if (string.IsNullOrWhiteSpace(Username))
                return;

            IsLoading = true;
            IsDialogFound = false;
            IsDialogNotFound = false;

            try
            {
                StatusMessage = $"Поиск диалога @{Username.TrimStart('@')}...";
                
                _currentDialog = await _scriptManager.SearchDialogAsync(Username);

                if (_currentDialog.IsFound)
                {
                    IsDialogFound = true;
                    StatusMessage = $"Диалог @{Username.TrimStart('@')} найден!";
                }
                else
                {
                    IsDialogNotFound = true;
                    StatusMessage = $"Диалог @{Username.TrimStart('@')} не найден";
                }
            }
            catch (Exception ex)
            {
                StatusMessage = $"Ошибка поиска: {ex.Message}";
            }
            finally
            {
                IsLoading = false;
            }
        }

        private async Task SendNoReplyAsync(object parameter)
        {
            if (_currentDialog == null || !_currentDialog.IsFound || string.IsNullOrWhiteSpace(Name))
                return;

            IsLoading = true;

            try
            {
                var parameters = new Dictionary<string, string>
                {
                    ["Имя"] = Name
                };

                ScriptResult result;

                // ОБНОВЛЕНО: Проверяем, выбран ли конкретный аккаунт
                if (SelectedAccountForNoReply != null)
                {
                    result = await _scriptManager.SendScriptToFoundDialogFromAccountAsync(_currentDialog, "no_reply", parameters, SelectedAccountForNoReply);
                }
                else
                {
                    result = await _scriptManager.SendScriptToFoundDialogAsync(_currentDialog, "no_reply", parameters);
                }

                if (result.Success)
                {
                    var accountInfo = SelectedAccountForNoReply != null ? $" через аккаунт {SelectedAccountForNoReply.Name}" : "";
                    StatusMessage = $"Сообщение 'нет ответа' отправлено успешно{accountInfo}";
                }
                else
                {
                    StatusMessage = $"Ошибка отправки: {result.ErrorMessage}";
                }
            }
            catch (Exception ex)
            {
                StatusMessage = $"Ошибка: {ex.Message}";
            }
            finally
            {
                IsLoading = false;
            }
        }

        private async Task SendFirstMessageAsync(object parameter)
        {
            if (string.IsNullOrWhiteSpace(Username) || string.IsNullOrWhiteSpace(Name))
                return;

            IsLoading = true;

            try
            {
                var parameters = new Dictionary<string, string>
                {
                    ["Имя"] = Name,
                    ["Дата"] = Date
                };

                ScriptResult result;

                // ОБНОВЛЕНО: Проверяем, выбран ли конкретный аккаунт
                if (SelectedAccountForFirstMessage != null)
                {
                    result = await _scriptManager.SendScriptFromAccountAsync("first_message", Username, parameters, SelectedAccountForFirstMessage);
                }
                else
                {
                    result = await _scriptManager.SendScriptAsync("first_message", Username, parameters);
                }

                if (result.Success)
                {
                    var accountInfo = SelectedAccountForFirstMessage != null ? $" через аккаунт {SelectedAccountForFirstMessage.Name}" : "";
                    StatusMessage = $"Первое сообщение отправлено успешно{accountInfo}";
                }
                else
                {
                    StatusMessage = $"Ошибка отправки: {result.ErrorMessage}";
                }
            }
            catch (Exception ex)
            {
                StatusMessage = $"Ошибка: {ex.Message}";
            }
            finally
            {
                IsLoading = false;
            }
        }

        private async Task ConnectAccountAsync(object parameter)
        {
            if (string.IsNullOrWhiteSpace(AccountName) || 
                string.IsNullOrWhiteSpace(ApiId) || 
                string.IsNullOrWhiteSpace(ApiHash) || 
                string.IsNullOrWhiteSpace(PhoneNumber))
                return;

            IsLoading = true;

            try
            {
                var request = new AccountConnectionRequest
                {
                    Name = AccountName,
                    ApiId = ApiId,
                    ApiHash = ApiHash,
                    PhoneNumber = PhoneNumber
                };

                bool success = await _scriptManager.ConnectAccountAsync(request);

                if (success)
                {
                    StatusMessage = $"Аккаунт {AccountName} подключен успешно";
                    RefreshAccounts(null);
                    
                    // Очищаем поля
                    AccountName = "";
                    PhoneNumber = "";
                }
                else
                {
                    StatusMessage = "Ошибка подключения аккаунта";
                }
            }
            catch (Exception ex)
            {
                StatusMessage = $"Ошибка: {ex.Message}";
            }
            finally
            {
                IsLoading = false;
            }
        }

        private void RefreshAccounts(object parameter)
        {
            // Обновляем коллекцию в UI потоке
            _dispatcher.BeginInvoke(new Action(() =>
            {
                var previousNoReplySelection = SelectedAccountForNoReply;
                var previousFirstMessageSelection = SelectedAccountForFirstMessage;

                ConnectedAccounts.Clear();
                var accounts = _scriptManager.GetConnectedAccounts();
                
                foreach (var account in accounts)
                {
                    ConnectedAccounts.Add(account);
                }

                // ОБНОВЛЕНО: Восстанавливаем выбранные аккаунты если они еще существуют
                if (previousNoReplySelection != null)
                {
                    SelectedAccountForNoReply = accounts.FirstOrDefault(a => a.Name == previousNoReplySelection.Name);
                }
                
                if (previousFirstMessageSelection != null)
                {
                    SelectedAccountForFirstMessage = accounts.FirstOrDefault(a => a.Name == previousFirstMessageSelection.Name);
                }

                StatusMessage = $"Подключено аккаунтов: {accounts.Count}";
            }));
        }

        private void ClearStatusLog(object parameter)
        {
            // Очищаем лог в UI потоке
            _dispatcher.BeginInvoke(new Action(() =>
            {
                StatusLog.Clear();
            }));
        }

        private async Task DisconnectAccountAsync(object parameter)
        {
            if (parameter is not string accountName || string.IsNullOrWhiteSpace(accountName))
                return;

            IsLoading = true;

            try
            {
                bool success = await _scriptManager.DisconnectAccountAsync(accountName);

                if (success)
                {
                    // ОБНОВЛЕНО: Очищаем выбранные аккаунты если отключается выбранный
                    if (SelectedAccountForNoReply?.Name == accountName)
                        SelectedAccountForNoReply = null;
                    
                    if (SelectedAccountForFirstMessage?.Name == accountName)
                        SelectedAccountForFirstMessage = null;

                    StatusMessage = $"Аккаунт {accountName} отключен успешно";
                    RefreshAccounts(null);
                }
                else
                {
                    StatusMessage = $"Ошибка отключения аккаунта {accountName}";
                }
            }
            catch (Exception ex)
            {
                StatusMessage = $"Ошибка: {ex.Message}";
            }
            finally
            {
                IsLoading = false;
            }
        }

        #endregion

        #region Command CanExecute Methods

        private bool CanExecuteSearch(object parameter)
        {
            return !IsLoading && !string.IsNullOrWhiteSpace(Username);
        }

        private bool CanExecuteNoReply(object parameter)
        {
            return !IsLoading && 
                   _currentDialog != null && 
                   _currentDialog.IsFound && 
                   !string.IsNullOrWhiteSpace(Name);
        }

        private bool CanExecuteFirstMessage(object parameter)
        {
            return !IsLoading && 
                   !string.IsNullOrWhiteSpace(Username) && 
                   !string.IsNullOrWhiteSpace(Name);
        }

        private bool CanExecuteConnectAccount(object parameter)
        {
            return !IsLoading && 
                   !string.IsNullOrWhiteSpace(AccountName) && 
                   !string.IsNullOrWhiteSpace(ApiId) && 
                   !string.IsNullOrWhiteSpace(ApiHash) && 
                   !string.IsNullOrWhiteSpace(PhoneNumber);
        }

        private bool CanExecuteDisconnectAccount(object parameter)
        {
            return !IsLoading && parameter is string accountName && !string.IsNullOrWhiteSpace(accountName);
        }

        #endregion

        #region Cleanup

        public void Dispose()
        {
            _scriptManager?.Dispose();
        }

        #endregion
    }
}