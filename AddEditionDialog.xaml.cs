using System.Windows;
using Dapper;
using MySql.Data.MySqlClient;

namespace WpfApp1
{
    public partial class AddEditionDialog : Window
    {
        private string connStr;

        public AddEditionDialog (string connectionString)
        {
            InitializeComponent();
            this.connStr = connectionString;
            EditionNameBox.Focus();
        }

        private void AddEdition_Click (object sender, RoutedEventArgs e)
        {
            string editionName = EditionNameBox.Text.Trim();
            string editionIdentifier = EditionIdentifierBox.Text.Trim();

            // Validation
            if (string.IsNullOrWhiteSpace(editionName))
            {
                MessageBox.Show("Please enter an edition name.", "Validation Error",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (string.IsNullOrWhiteSpace(editionIdentifier))
            {
                MessageBox.Show("Please enter an edition identifier.", "Validation Error",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            try
            {
                using (var conn = new MySqlConnection(connStr))
                {
                    // Check if edition already exists
                    var existing = conn.QueryFirstOrDefault<int?>(
                        "SELECT id FROM card_editions WHERE type = @Type OR edition_identifier = @Identifier",
                        new { Type = editionName, Identifier = editionIdentifier });

                    if (existing.HasValue)
                    {
                        MessageBox.Show("An edition with this name or identifier already exists.",
                            "Duplicate Edition",
                            MessageBoxButton.OK,
                            MessageBoxImage.Warning);
                        return;
                    }

                    // Insert new edition
                    string sql = @"INSERT INTO card_editions (type, edition_identifier, nr_pachete) 
                                   VALUES (@Type, @Identifier, 0)";

                    conn.Execute(sql, new
                    {
                        Type = editionName,
                        Identifier = editionIdentifier
                    });
                }

                MessageBox.Show($"Edition '{editionName}' added successfully!",
                    "Success",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);

                DialogResult = true;
                Close();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error adding edition: {ex.Message}",
                    "Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
        }

        private void Cancel_Click (object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }
    }
}