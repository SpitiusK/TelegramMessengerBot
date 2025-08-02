// UI/Views/InputDialog.xaml.cs
using System;
using System.Windows;
using System.Windows.Input;

namespace UI.Views
{
    /// <summary>
    /// Диалоговое окно для ввода данных (код подтверждения, пароль 2FA)
    /// </summary>
    public partial class InputDialog : Window
    {
        public string InputText { get; private set; }
        public bool IsPasswordMode { get; private set; }

        public InputDialog()
        {
            InitializeComponent();
            InputTextBox.Focus();
        }

        /// <summary>
        /// Показать диалог для ввода кода подтверждения
        /// </summary>
        public static string ShowVerificationCodeDialog(string phoneNumber, Window owner = null)
        {
            var dialog = new InputDialog
            {
                Owner = owner,
                Title = "Код подтверждения",
                IsPasswordMode = false
            };

            dialog.TitleTextBlock.Text = "Код подтверждения";
            dialog.DescriptionTextBlock.Text = 
                $"На номер {phoneNumber} отправлен код подтверждения.\n" +
                "Проверьте сообщения в Telegram и введите полученный код:";

            dialog.InputTextBox.Visibility = Visibility.Visible;
            dialog.PasswordBox.Visibility = Visibility.Collapsed;
            dialog.InputTextBox.Focus();

            return dialog.ShowDialog() == true ? dialog.InputText : null;
        }

        /// <summary>
        /// Показать диалог для ввода пароля 2FA
        /// </summary>
        public static string ShowPasswordDialog(string phoneNumber, Window owner = null)
        {
            var dialog = new InputDialog
            {
                Owner = owner,
                Title = "Пароль двухфакторной аутентификации",
                IsPasswordMode = true
            };

            dialog.TitleTextBlock.Text = "Двухфакторная аутентификация";
            dialog.DescriptionTextBlock.Text = 
                $"Для аккаунта {phoneNumber} включена двухфакторная аутентификация.\n" +
                "Введите пароль облачной авторизации:";

            dialog.InputTextBox.Visibility = Visibility.Collapsed;
            dialog.PasswordBox.Visibility = Visibility.Visible;
            dialog.PasswordBox.Focus();

            return dialog.ShowDialog() == true ? dialog.InputText : null;
        }

        /// <summary>
        /// Универсальный метод для показа диалога с произвольным текстом
        /// </summary>
        public static string ShowInputDialog(string title, string description, bool isPassword = false, Window owner = null)
        {
            var dialog = new InputDialog
            {
                Owner = owner,
                Title = title,
                IsPasswordMode = isPassword
            };

            dialog.TitleTextBlock.Text = title;
            dialog.DescriptionTextBlock.Text = description;

            if (isPassword)
            {
                dialog.InputTextBox.Visibility = Visibility.Collapsed;
                dialog.PasswordBox.Visibility = Visibility.Visible;
                dialog.PasswordBox.Focus();
            }
            else
            {
                dialog.InputTextBox.Visibility = Visibility.Visible;
                dialog.PasswordBox.Visibility = Visibility.Collapsed;
                dialog.InputTextBox.Focus();
            }

            return dialog.ShowDialog() == true ? dialog.InputText : null;
        }

        private void OkButton_Click(object sender, RoutedEventArgs e)
        {
            if (IsPasswordMode)
            {
                InputText = PasswordBox.Password;
            }
            else
            {
                InputText = InputTextBox.Text;
            }

            if (string.IsNullOrWhiteSpace(InputText))
            {
                MessageBox.Show(
                    "Поле не может быть пустым!", 
                    "Внимание", 
                    MessageBoxButton.OK, 
                    MessageBoxImage.Warning);
                return;
            }

            DialogResult = true;
            Close();
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }

        private void InputTextBox_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                OkButton_Click(sender, e);
            }
        }

        private void PasswordBox_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                OkButton_Click(sender, e);
            }
        }
    }
}