using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Imaging;
using System.Windows.Media.Animation;
using System.Windows.Media;
using Dapper;
using MySql.Data.MySqlClient;
using System.Collections.Concurrent;

namespace WpfApp1
{
    public class Card
    {
        public int Id { get; set; }
        public int Number { get; set; }
        public string Name { get; set; } = string.Empty;
        public int EditionId { get; set; }
        public string Rarity { get; set; } = string.Empty;
        public decimal Price { get; set; }
        public int Copies { get; set; }
        public string? Image { get; set; }
        public DateTime? PullDate { get; set; }
    }

    public partial class MainWindow : Window
    {
        private string connStr;
        private Dictionary<string, (int Id, string Identifier)> editionMap;
        private ConcurrentDictionary<string, BitmapImage> imageCache;
        private bool isAnimating = false;

        public MainWindow ()
        {
            connStr = "server=localhost;user=root;database=pokemon_2025;port=3306;";
            editionMap = new Dictionary<string, (int Id, string Identifier)>();
            imageCache = new ConcurrentDictionary<string, BitmapImage>();

            InitializeComponent();

            // Initialize RenderTransform for animations
            DisplayArea.RenderTransform = new TranslateTransform();

            // Initialize database
            InitializeDatabase();

            LoadEditions();
        }

        private void InitializeDatabase ()
        {
            try
            {
                // Connection string without database to create it if needed
                string connStrWithoutDb = "server=localhost;user=root;port=3306;";

                using (var conn = new MySqlConnection(connStrWithoutDb))
                {
                    conn.Open();

                    // Create database if it doesn't exist
                    string createDbSql = @"CREATE DATABASE IF NOT EXISTS pokemon_2025 
                                          CHARACTER SET utf8mb4 
                                          COLLATE utf8mb4_general_ci;";
                    conn.Execute(createDbSql);

                    System.Diagnostics.Debug.WriteLine("Database 'pokemon_2025' ensured.");
                }

                // Now connect to the database and create tables
                using (var conn = new MySqlConnection(connStr))
                {
                    conn.Open();

                    // Create card_editions table
                    string createEditionsTableSql = @"
                        CREATE TABLE IF NOT EXISTS card_editions (
                            id INT(11) NOT NULL AUTO_INCREMENT,
                            type TEXT CHARACTER SET utf8mb4 COLLATE utf8mb4_general_ci NULL,
                            nr_pachete INT(11) NOT NULL DEFAULT 0,
                            edition_identifier TEXT CHARACTER SET utf8mb4 COLLATE utf8mb4_general_ci NOT NULL,
                            PRIMARY KEY (id)
                        ) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_general_ci;";

                    conn.Execute(createEditionsTableSql);
                    System.Diagnostics.Debug.WriteLine("Table 'card_editions' ensured.");

                    // Create cards table
                    string createCardsTableSql = @"
                        CREATE TABLE IF NOT EXISTS cards (
                            id INT(11) NOT NULL AUTO_INCREMENT,
                            number INT(11) NULL,
                            name TEXT CHARACTER SET utf8mb4 COLLATE utf8mb4_general_ci NULL,
                            edition_id INT(11) NULL,
                            rarity TEXT CHARACTER SET utf8mb4 COLLATE utf8mb4_general_ci NULL,
                            price DECIMAL(10,2) NULL,
                            copies INT(11) NOT NULL DEFAULT 1,
                            image TEXT CHARACTER SET utf8mb4 COLLATE utf8mb4_general_ci NULL,
                            pull_date DATETIME NULL,
                            PRIMARY KEY (id),
                            INDEX idx_edition_id (edition_id)
                        ) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_general_ci;";

                    conn.Execute(createCardsTableSql);
                    System.Diagnostics.Debug.WriteLine("Table 'cards' ensured.");

                    // Optionally add foreign key constraint if not exists
                    string addForeignKeySql = @"
                        ALTER TABLE cards 
                        ADD CONSTRAINT fk_edition 
                        FOREIGN KEY (edition_id) 
                        REFERENCES card_editions(id) 
                        ON DELETE CASCADE;";

                    try
                    {
                        conn.Execute(addForeignKeySql);
                        System.Diagnostics.Debug.WriteLine("Foreign key constraint added.");
                    }
                    catch
                    {
                        // Foreign key might already exist, ignore error
                        System.Diagnostics.Debug.WriteLine("Foreign key constraint already exists or couldn't be added.");
                    }
                }

                System.Diagnostics.Debug.WriteLine("Database initialization completed successfully.");
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error initializing database: {ex.Message}\n\nPlease ensure MySQL is running and accessible.",
                    "Database Initialization Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);

                System.Diagnostics.Debug.WriteLine($"Database initialization error: {ex.Message}");
            }
        }

        private void LoadEditions ()
        {
            using (var conn = new MySqlConnection(connStr))
            {
                var editions = conn.Query<(int Id, string Type, string EditionIdentifier)>(
                    "SELECT id, type, edition_identifier FROM card_editions").ToList();

                editionMap.Clear();
                List<string> editionNames = new List<string>();

                foreach (var edition in editions)
                {
                    editionMap[edition.Type] = (edition.Id, edition.EditionIdentifier);
                    editionNames.Add(edition.Type);
                }

                EditionSelector.ItemsSource = editionNames;

                if (editionNames.Count > 0)
                {
                    EditionSelector.SelectedIndex = 0;
                }
            }
        }

        private async void EditionSelector_SelectionChanged (object sender, SelectionChangedEventArgs e)
        {
            if (EditionSelector.SelectedItem is string selectedEdition)
            {
                // First, clear the current card details immediately
                ClearCardDetails();
                
                // Fade out the panel
                await AnimateEditionChange();
                
                // Load new cards while panel is fading back in
                await LoadCardsForEditionAsync(selectedEdition);
            }
        }

        private async Task AnimateEditionChange()
        {
            var fadeOut = new DoubleAnimation
            {
                From = 1,
                To = 0,
                Duration = TimeSpan.FromMilliseconds(150)
            };
            
            // Fade out both the card display panel and the card list
            CardDisplayPanel.BeginAnimation(OpacityProperty, fadeOut);
            CardListBox.BeginAnimation(OpacityProperty, fadeOut);
            await Task.Delay(150);

            var fadeIn = new DoubleAnimation
            {
                From = 0,
                To = 1,
                Duration = TimeSpan.FromMilliseconds(200)
            };
            
            // Fade in both the card display panel and the card list
            CardDisplayPanel.BeginAnimation(OpacityProperty, fadeIn);
            CardListBox.BeginAnimation(OpacityProperty, fadeIn);
        }

        private async Task LoadCardsForEditionAsync(string editionType)
        {
            if (!editionMap.ContainsKey(editionType))
                return;

            int editionId = editionMap[editionType].Id;

            List<Card> cards;
            using (var conn = new MySqlConnection(connStr))
            {
                string sql = @"SELECT id as Id, number as Number, name as Name, 
                              rarity as Rarity, price as Price, image as Image, 
                              pull_date as PullDate, edition_id as EditionId,
                              copies as Copies
                              FROM cards WHERE edition_id = @EditionId 
                              ORDER BY price DESC";

                cards = conn.Query<Card>(sql, new { EditionId = editionId }).ToList();
            }

            CardListBox.ItemsSource = cards;
            
            isAnimating = true;
            
            if (cards.Count > 0)
            {
                CardListBox.SelectedIndex = 0;
                await Task.Delay(50);
                
                if (CardListBox.SelectedItem is Card firstCard)
                {
                    DisplayCardDetails(firstCard);
                }
            }
            else
            {
                ClearCardDetails();
            }
            
            isAnimating = false;

            await PreloadImagesAsync(cards);
        }

        private async Task PreloadImagesAsync(List<Card> cards)
        {
            var imagesToLoad = cards
                .Where(c => !string.IsNullOrEmpty(c.Image) && !imageCache.ContainsKey(c.Image))
                .Select(c => c.Image!)
                .Distinct()
                .ToList();

            if (imagesToLoad.Count == 0)
                return;

            System.Diagnostics.Debug.WriteLine($"Preloading {imagesToLoad.Count} images...");

            var tasks = imagesToLoad.Select(async imageUrl =>
            {
                try
                {
                    var bitmap = await LoadImageAsync(imageUrl);
                    if (bitmap != null)
                    {
                        imageCache.TryAdd(imageUrl, bitmap);
                    }
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Failed to cache image {imageUrl}: {ex.Message}");
                }
            });

            await Task.WhenAll(tasks.Take(5));
            
            if (imagesToLoad.Count > 5)
            {
                await Task.WhenAll(tasks.Skip(5));
            }

            System.Diagnostics.Debug.WriteLine($"Cached {imageCache.Count} total images");
        }

        private async Task<BitmapImage?> LoadImageAsync(string imageUrl)
        {
            try
            {
                byte[]? imageData = await Task.Run(async () =>
                {
                    using var client = new System.Net.Http.HttpClient();
                    return await client.GetByteArrayAsync(imageUrl);
                });

                if (imageData == null)
                    return null;

                return await Dispatcher.InvokeAsync(() =>
                {
                    var bitmap = new BitmapImage();
                    bitmap.BeginInit();
                    bitmap.CacheOption = BitmapCacheOption.OnLoad;
                    bitmap.CreateOptions = BitmapCreateOptions.IgnoreColorProfile;
                    bitmap.StreamSource = new System.IO.MemoryStream(imageData);
                    bitmap.DecodePixelWidth = 436;
                    bitmap.EndInit();
                    bitmap.Freeze();
                    return bitmap;
                });
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Failed to load image: {ex.Message}");
                return null;
            }
        }

        private async void AddCard_Click(object sender, RoutedEventArgs e)
        {
            if (EditionSelector.SelectedItem is string selectedEdition)
            {
                var editionData = editionMap[selectedEdition];
                var dialog = new AddCardDialog(editionData.Id, editionData.Identifier, connStr);
                
                if (dialog.ShowDialog() == true)
                {
                    if (dialog.NewCardId.HasValue)
                    {
                        // Don't reload everything - just update the specific card
                        isAnimating = true;
                        
                        if (CardListBox.ItemsSource is List<Card> cards)
                        {
                            var existingCard = cards.FirstOrDefault(c => c.Id == dialog.NewCardId.Value);
                            
                            if (existingCard != null)
                            {
                                // Card already exists - just update the copy count
                                using (var conn = new MySqlConnection(connStr))
                                {
                                    var updatedCard = conn.QueryFirstOrDefault<Card>(
                                        @"SELECT id as Id, number as Number, name as Name, 
                                          rarity as Rarity, price as Price, image as Image, 
                                          pull_date as PullDate, edition_id as EditionId, copies as Copies
                                          FROM cards WHERE id = @Id",
                                    new { Id = dialog.NewCardId.Value });
                                    
                                    if (updatedCard != null)
                                    {
                                        // Update the object in the list
                                        existingCard.Copies = updatedCard.Copies;
                                        
                                        // Refresh display if this card is currently selected
                                        if (CardListBox.SelectedItem is Card currentCard && currentCard.Id == updatedCard.Id)
                                        {
                                            CardCopies.Text = updatedCard.Copies.ToString();
                                        }
                                        
                                        // Refresh the list display to show updated count
                                        CardListBox.Items.Refresh();
                                    }
                                }
                                
                                isAnimating = false;
                            }
                            else
                            {
                                // New card added - reload the list
                                await LoadCardsForEditionAsync(selectedEdition);
                                
                                var newCard = ((List<Card>)CardListBox.ItemsSource).FirstOrDefault(c => c.Id == dialog.NewCardId.Value);
                                if (newCard != null)
                                {
                                    ClearCardDetails();
                                    await Task.Delay(50);
                                    
                                    CardListBox.SelectedItem = newCard;
                                    CardListBox.ScrollIntoView(newCard);
                                    
                                    isAnimating = false;
                                    await AnimateNewCardDisplay(newCard);
                                    return;
                                }
                                
                                isAnimating = false;
                            }
                        }
                        else
                        {
                            isAnimating = false;
                        }
                    }
                }
            }
            else
            {
                MessageBox.Show("Please select an edition first.", "No Edition Selected",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        private async Task AnimateNewCardDisplay(Card card)
        {
            // Ensure we start from invisible state
            DisplayArea.Opacity = 0;
            
            // Update content while invisible
            DisplayCardDetails(card);
            
            // Small delay to ensure content is rendered
            await Task.Delay(50);
            
            // Slide in and fade in NEW content
            var slideIn = new DoubleAnimation
            {
                From = 30,
                To = 0,
                Duration = TimeSpan.FromMilliseconds(300),
                EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
            };
            
            var fadeIn = new DoubleAnimation
            {
                From = 0,
                To = 1,
                Duration = TimeSpan.FromMilliseconds(300)
            };
            
            DisplayArea.RenderTransform.BeginAnimation(TranslateTransform.XProperty, slideIn);
            DisplayArea.BeginAnimation(OpacityProperty, fadeIn);
            
            await Task.Delay(300);
        }

        private async void CardListBox_SelectionChanged (object sender, SelectionChangedEventArgs e)
        {
            if (CardListBox.SelectedItem is Card selectedCard && !isAnimating)
            {
                isAnimating = true;
                
                // Slide out and fade out OLD content
                var slideOut = new DoubleAnimation
                {
                    From = 0,
                    To = -30,
                    Duration = TimeSpan.FromMilliseconds(150)
                };
                
                var fadeOut = new DoubleAnimation
                {
                    From = 1,
                    To = 0,
                    Duration = TimeSpan.FromMilliseconds(150)
                };
                
                DisplayArea.RenderTransform.BeginAnimation(TranslateTransform.XProperty, slideOut);
                DisplayArea.BeginAnimation(OpacityProperty, fadeOut);
                
                // Wait for animation to complete before updating content
                await Task.Delay(150);
                
                // NOW update the content (while it's invisible)
                DisplayCardDetails(selectedCard);
                
                // Small delay to ensure content is updated
                await Task.Delay(50);
                
                // Slide in and fade in NEW content
                var slideIn = new DoubleAnimation
                {
                    From = 30,
                    To = 0,
                    Duration = TimeSpan.FromMilliseconds(200),
                    EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
                };
                
                var fadeIn = new DoubleAnimation
                {
                    From = 0,
                    To = 1,
                    Duration = TimeSpan.FromMilliseconds(200)
                };
                
                DisplayArea.RenderTransform.BeginAnimation(TranslateTransform.XProperty, slideIn);
                DisplayArea.BeginAnimation(OpacityProperty, fadeIn);
                
                await Task.Delay(200);
                isAnimating = false;
            }
        }

        private void DisplayCardDetails(Card card)
        {
            CardNumber.Text = card.Number.ToString();
            CardName.Text = card.Name;
            CardRarity.Text = card.Rarity;
            CardPrice.Text = $"${card.Price:F2}";
            PullDate.Text = card.PullDate?.ToString("yyyy-MM-dd HH:mm") ?? "N/A";

            var editionName = editionMap.FirstOrDefault(x => x.Value.Id == card.EditionId).Key ?? "Unknown";
            CardEdition.Text = editionName;

            // Load image synchronously if cached
            if (!string.IsNullOrEmpty(card.Image))
            {
                if (imageCache.TryGetValue(card.Image, out var cachedImage))
                {
                    CardImage.Source = cachedImage;
                    System.Diagnostics.Debug.WriteLine("Image loaded from cache!");
                }
                else
                {
                    // Start async load but don't wait
                    _ = LoadAndDisplayImageAsync(card.Image);
                }
            }
            else
            {
                CardImage.Source = null;
            }

            CardCopies.Text = card.Copies.ToString(); // Update copies text
        }

        private async Task LoadAndDisplayImageAsync(string imageUrl)
        {
            System.Diagnostics.Debug.WriteLine("Image not in cache, loading...");
            try
            {
                var bitmap = await LoadImageAsync(imageUrl);
                if (bitmap != null)
                {
                    imageCache.TryAdd(imageUrl, bitmap);
                    CardImage.Source = bitmap;
                }
                else
                {
                    CardImage.Source = null;
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Image loading error: {ex.Message}");
                CardImage.Source = null;
            }
        }

        private void ClearCardDetails ()
        {
            CardNumber.Text = "N/A";
            CardName.Text = "N/A";
            CardRarity.Text = "N/A";
            CardPrice.Text = "N/A";
            PullDate.Text = "N/A";
            CardEdition.Text = "N/A";
            CardImage.Source = null;
            CardCopies.Text = "0"; // Reset copies text
        }

        private async void AddEdition_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new AddEditionDialog(connStr);
            
            if (dialog.ShowDialog() == true)
            {
                LoadEditions();
                
                if (EditionSelector.Items.Count > 0)
                {
                    EditionSelector.SelectedIndex = EditionSelector.Items.Count - 1;
                }
            }
        }

        private async void DeleteCard_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button button && button.Tag is Card card)
            {
                var result = MessageBox.Show(
                    $"Are you sure you want to delete '{card.Name}'?",
                    "Confirm Delete",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Warning);

                if (result == MessageBoxResult.Yes)
                {
                    try
                    {
                        using (var conn = new MySqlConnection(connStr))
                        {
                            string sql = "DELETE FROM cards WHERE id = @Id";
                            conn.Execute(sql, new { Id = card.Id });
                        }

                        if (!string.IsNullOrEmpty(card.Image))
                        {
                            imageCache.TryRemove(card.Image, out _);
                        }

                        if (EditionSelector.SelectedItem is string selectedEdition)
                        {
                            await LoadCardsForEditionAsync(selectedEdition);
                        }
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show($"Error deleting card: {ex.Message}", "Error",
                            MessageBoxButton.OK, MessageBoxImage.Error);
                    }
                }
            }
        }

        private void RemoveCopy_Click(object sender, RoutedEventArgs e)
        {
            if (CardListBox.SelectedItem is Card card)
            {
                if (card.Copies <= 1)
                {
                    MessageBox.Show("This is the last copy. Use the delete button (✕) to remove the card completely.",
                        "Cannot Remove",
                        MessageBoxButton.OK,
                        MessageBoxImage.Information);
                    return;
                }

                var result = MessageBox.Show(
                    $"Remove one copy of '{card.Name}'?\n\nCopies will go from {card.Copies} to {card.Copies - 1}",
                    "Remove Copy",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Question);

                if (result == MessageBoxResult.Yes)
                {
                    try
                    {
                        using (var conn = new MySqlConnection(connStr))
                        {
                            string sql = "UPDATE cards SET copies = copies - 1 WHERE id = @Id";
                            conn.Execute(sql, new { Id = card.Id });
                        }

                        // Update the card object and UI
                        card.Copies--;
                        CardCopies.Text = card.Copies.ToString();
                        
                        // Refresh the list display to show updated count
                        CardListBox.Items.Refresh();
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show($"Error removing copy: {ex.Message}", "Error",
                            MessageBoxButton.OK, MessageBoxImage.Error);
                    }
                }
            }
        }
        }
}