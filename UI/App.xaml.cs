// UI/App.xaml.cs
using System;
using System.IO;
using System.Windows;
using System.Windows.Threading;

namespace UI
{
    /// <summary>
    /// Логика взаимодействия для App.xaml
    /// </summary>
    public partial class App : Application
    {
        protected override void OnStartup(StartupEventArgs e)
        {
            // Настройка обработки необработанных исключений
            SetupExceptionHandling();

            // Создание необходимых директорий
            CreateRequiredDirectories();

            base.OnStartup(e);
        }

        private void SetupExceptionHandling()
        {
            // Обработка исключений в UI потоке
            DispatcherUnhandledException += OnDispatcherUnhandledException;

            // Обработка исключений в других потоках
            AppDomain.CurrentDomain.UnhandledException += OnUnhandledException;

            // Обработка исключений в Task
            TaskScheduler.UnobservedTaskException += OnUnobservedTaskException;
        }

        private void OnDispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
        {
            LogException("UI Thread Exception", e.Exception);
            
            var result = MessageBox.Show(
                $"Произошла непредвиденная ошибка:\n\n{e.Exception.Message}\n\nПродолжить работу?",
                "Ошибка приложения",
                MessageBoxButton.YesNo,
                MessageBoxImage.Error);

            if (result == MessageBoxResult.Yes)
            {
                e.Handled = true; // Продолжаем работу
            }
            else
            {
                Current.Shutdown(); // Закрываем приложение
            }
        }

        private void OnUnhandledException(object sender, UnhandledExceptionEventArgs e)
        {
            LogException("AppDomain Exception", e.ExceptionObject as Exception);
            
            MessageBox.Show(
                $"Критическая ошибка приложения:\n\n{(e.ExceptionObject as Exception)?.Message}",
                "Критическая ошибка",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
        }

        private void OnUnobservedTaskException(object sender, UnobservedTaskExceptionEventArgs e)
        {
            LogException("Task Exception", e.Exception);
            e.SetObserved(); // Помечаем исключение как обработанное
        }

        private void LogException(string source, Exception exception)
        {
            try
            {
                var logDirectory = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Logs");
                Directory.CreateDirectory(logDirectory);

                var logFile = Path.Combine(logDirectory, $"error_{DateTime.Now:yyyyMMdd}.log");
                var logEntry = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] {source}: {exception}\n\n";

                File.AppendAllText(logFile, logEntry);
            }
            catch
            {
                // Игнорируем ошибки логирования
            }
        }

        private void CreateRequiredDirectories()
        {
            try
            {
                var baseDirectory = AppDomain.CurrentDomain.BaseDirectory;
                
                // Создаем директории для сессий Telegram
                var sessionsDirectory = Path.Combine(baseDirectory, "Sessions");
                Directory.CreateDirectory(sessionsDirectory);

                // Создаем директории для логов
                var logsDirectory = Path.Combine(baseDirectory, "Logs");
                Directory.CreateDirectory(logsDirectory);

                // Создаем директории для скриптов (если нужно)
                var scriptsDirectory = Path.Combine(baseDirectory, "Scripts");
                Directory.CreateDirectory(scriptsDirectory);
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    $"Не удалось создать необходимые директории:\n{ex.Message}",
                    "Ошибка инициализации",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
            }
        }

        protected override void OnExit(ExitEventArgs e)
        {
            try
            {
                // Здесь можно добавить логику очистки ресурсов
                // например, закрытие соединений с Telegram
                
                // Логируем завершение работы приложения
                LogApplicationExit();
            }
            catch (Exception ex)
            {
                LogException("Application Exit", ex);
            }
            finally
            {
                base.OnExit(e);
            }
        }

        private void LogApplicationExit()
        {
            try
            {
                var logDirectory = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Logs");
                var logFile = Path.Combine(logDirectory, $"app_{DateTime.Now:yyyyMMdd}.log");
                var logEntry = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] Application shutdown\n";

                File.AppendAllText(logFile, logEntry);
            }
            catch
            {
                // Игнорируем ошибки логирования при выходе
            }
        }
    }
}