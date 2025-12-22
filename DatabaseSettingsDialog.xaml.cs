using System.Windows;
using MySql.Data.MySqlClient;

namespace WpfApp1
{
    public partial class DatabaseSettingsDialog : Window
    {
        private DatabaseConfig config;

        public DatabaseSettingsDialog (DatabaseConfig currentConfig)
        {
            InitializeComponent();
            this.config = currentConfig;

            // Load current settings
            ServerBox.Text = config.Server;
            PortBox.Text = config.Port.ToString();
            UserBox.Text = config.User;
            PasswordBox.Password = config.Password;
            DatabaseBox.Text = config.Database;

            ServerBox.Focus();
        }

        private void TestConnection_Click (object sender, RoutedEventArgs e)
        {
            // Update config from UI
            UpdateConfigFromUI();

            StatusText.Visibility = Visibility.Visible;
            StatusText.Foreground = new System.Windows.Media.SolidColorBrush(
                System.Windows.Media.Color.FromRgb(252, 202, 87)); // Yellow
            StatusText.Text = "Testing connection...";

            TestButton.IsEnabled = false;

            Task.Run(() =>
            {
                bool success = config.TestConnection(out string errorMessage);

                Dispatcher.Invoke(() =>
                {
                    if (success)
                    {
                        StatusText.Foreground = new System.Windows.Media.SolidColorBrush(
                            System.Windows.Media.Color.FromRgb(92, 216, 90)); // Green
                        StatusText.Text = "✅ Connection successful!";
                    }
                    else
                    {
                        StatusText.Foreground = new System.Windows.Media.SolidColorBrush(
                            System.Windows.Media.Color.FromRgb(238, 21, 21)); // Red
                        StatusText.Text = $"❌ Connection failed: {errorMessage}";
                    }

                    TestButton.IsEnabled = true;
                });
            });
        }

        private void Save_Click (object sender, RoutedEventArgs e)
        {
            // Validation
            if (string.IsNullOrWhiteSpace(ServerBox.Text))
            {
                MessageBox.Show("Server is required.", "Validation Error",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (!int.TryParse(PortBox.Text, out int port) || port <= 0)
            {
                MessageBox.Show("Please enter a valid port number.", "Validation Error",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (string.IsNullOrWhiteSpace(DatabaseBox.Text))
            {
                MessageBox.Show("Database name is required.", "Validation Error",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            // Update and save config
            UpdateConfigFromUI();
            config.Save();

            MessageBox.Show("Database settings saved!\n\nPlease restart the application for changes to take effect.",
                "Settings Saved",
                MessageBoxButton.OK,
                MessageBoxImage.Information);

            DialogResult = true;
            Close();
        }

        private void Cancel_Click (object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }

        private void UpdateConfigFromUI ()
        {
            config.Server = ServerBox.Text.Trim();
            config.Port = int.TryParse(PortBox.Text, out int port) ? port : 3306;
            config.User = UserBox.Text.Trim();
            config.Password = PasswordBox.Password;
            config.Database = DatabaseBox.Text.Trim();
        }
    }
}