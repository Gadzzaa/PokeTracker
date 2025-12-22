using System.Windows;
using System.Windows.Input;
using Dapper;
using MySql.Data.MySqlClient;

namespace WpfApp1
{
    public partial class AddCardDialog : Window
    {
        private string connStr;
        private int editionId;
        private string editionIdentifier;
        private PokemonTcgService apiService;
        
        // Property to return the ID of the newly added card
        public int? NewCardId { get; private set; }

        public AddCardDialog(int editionId, string editionIdentifier, string connectionString)
        {
            InitializeComponent();
            this.editionId = editionId;
            this.editionIdentifier = editionIdentifier;
            this.connStr = connectionString;
            this.apiService = new PokemonTcgService();

            CardNumberBox.Focus();
        }

        private void CardNumberBox_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                AddCard_Click(sender, e);
            }
        }

        private async void AddCard_Click(object sender, RoutedEventArgs e)
        {
            string cardNumber = CardNumberBox.Text.Trim();

            if (string.IsNullOrWhiteSpace(cardNumber))
            {
                MessageBox.Show("Please enter a card number.", "Validation Error",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            // Show loading
            AddButton.IsEnabled = false;
            CardNumberBox.IsEnabled = false;
            LoadingBar.Visibility = Visibility.Visible;
            StatusText.Visibility = Visibility.Visible;
            StatusText.Text = "Fetching card data from API...";

            try
            {
                // Build full card ID: edition_identifier-cardNumber (e.g., "sv07-123")
                string fullCardId = $"{editionIdentifier}-{cardNumber}";
                
                StatusText.Text = $"Fetching {fullCardId}...";

                // Fetch card data from API
                var cardData = await apiService.GetCardByNumberAsync(fullCardId);
                
                if (cardData == null)
                {
                    MessageBox.Show($"Card '{fullCardId}' not found in the Pokemon TCG API.\n\nPlease check the card number.",
                        "Card Not Found",
                        MessageBoxButton.OK,
                        MessageBoxImage.Warning);
                    return;
                }

                StatusText.Text = $"Found: {cardData.Name} - Adding to database...";

                // Parse numeric part for storage
                int numericCardNumber;
                if (!int.TryParse(cardNumber, out numericCardNumber))
                {
                    numericCardNumber = 0;
                }

                // Check if card already exists
                using (var conn = new MySqlConnection(connStr))
                {
                    var existing = conn.QueryFirstOrDefault<int?>(
                        "SELECT id FROM cards WHERE number = @Number AND edition_id = @EditionId",
                        new { Number = numericCardNumber, EditionId = editionId });

                    if (existing.HasValue)
                    {
                        var result = MessageBox.Show(
                            $"Card #{cardNumber} already exists in this edition.\n\nDo you want to add it again?",
                            "Duplicate Card",
                            MessageBoxButton.YesNo,
                            MessageBoxImage.Question);

                        if (result == MessageBoxResult.No)
                        {
                            return;
                        }
                    }
                }

                // Insert card into database and get the new ID
                using (var conn = new MySqlConnection(connStr))
                {
                    string sql = @"INSERT INTO cards (number, name, rarity, price, image, pull_date, edition_id) 
                                   VALUES (@Number, @Name, @Rarity, @Price, @Image, @PullDate, @EditionId);
                                   SELECT LAST_INSERT_ID();";

                    var card = new
                    {
                        Number = numericCardNumber,
                        Name = cardData.Name,
                        Rarity = cardData.Rarity,
                        Price = cardData.Price,
                        Image = cardData.ImageUrl,
                        PullDate = DateTime.Now,
                        EditionId = editionId
                    };

                    // Execute and get the new card ID
                    NewCardId = conn.ExecuteScalar<int>(sql, card);
                }

                StatusText.Text = "Card added successfully!";
                await Task.Delay(500);

                DialogResult = true;
                Close();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error adding card: {ex.Message}", "Error",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                AddButton.IsEnabled = true;
                CardNumberBox.IsEnabled = true;
                LoadingBar.Visibility = Visibility.Collapsed;
                StatusText.Visibility = Visibility.Collapsed;
            }
        }

        private void Cancel_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }
    }
}