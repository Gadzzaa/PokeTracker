using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Imaging;
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
        public string? Image { get; set; }
        public DateTime? PullDate { get; set; }
    }
    
    public partial class MainWindow : Window
    {
        private string connStr;
        private Dictionary<string, (int Id, string Identifier)> editionMap; // Changed to tuple
        private ConcurrentDictionary<string, BitmapImage> imageCache;

        public MainWindow ()
        {
            connStr = "server=localhost;user=root;database=pokemon_2025;port=3306;";
            editionMap = new Dictionary<string, (int Id, string Identifier)>();
            imageCache = new ConcurrentDictionary<string, BitmapImage>();

            InitializeComponent();
            LoadEditions();
        }

        private void LoadEditions ()
        {
            using (var conn = new MySqlConnection(connStr))
            {
                // Query updated to include edition_identifier
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
                await LoadCardsForEditionAsync(selectedEdition);
            }
        }

        private async Task LoadCardsForEditionAsync(string editionType)
        {
            if (!editionMap.ContainsKey(editionType))
                return;

            int editionId = editionMap[editionType].Id; // Access Id from tuple

            List<Card> cards;
            using (var conn = new MySqlConnection(connStr))
            {
                string sql = @"SELECT id as Id, number as Number, name as Name, 
                              rarity as Rarity, price as Price, image as Image, 
                              pull_date as PullDate, edition_id as EditionId 
                              FROM cards WHERE edition_id = @EditionId 
                              ORDER BY price DESC";

                cards = conn.Query<Card>(sql, new { EditionId = editionId }).ToList();
            }

            CardListBox.ItemsSource = cards;
            
            // Select the first card if available
            if (cards.Count > 0)
            {
                CardListBox.SelectedIndex = 0;
            }
            else
            {
                ClearCardDetails();
            }

            // Preload all images for this edition in the background
            await PreloadImagesAsync(cards);
        }

        private async Task PreloadImagesAsync(List<Card> cards)
        {
            // Get unique image URLs that aren't already cached
            var imagesToLoad = cards
                .Where(c => !string.IsNullOrEmpty(c.Image) && !imageCache.ContainsKey(c.Image))
                .Select(c => c.Image!)
                .Distinct()
                .ToList();

            if (imagesToLoad.Count == 0)
                return;

            System.Diagnostics.Debug.WriteLine($"Preloading {imagesToLoad.Count} images...");

            // Load images in parallel (max 5 at a time to avoid overwhelming the connection)
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

            // Process 5 images at a time
            await Task.WhenAll(tasks.Take(5));
            
            // Then process the rest
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
                // Download image data on background thread
                byte[]? imageData = await Task.Run(async () =>
                {
                    using var client = new System.Net.Http.HttpClient();
                    return await client.GetByteArrayAsync(imageUrl);
                });

                if (imageData == null)
                    return null;

                // Create BitmapImage on UI thread
                return await Dispatcher.InvokeAsync(() =>
                {
                    var bitmap = new BitmapImage();
                    bitmap.BeginInit();
                    bitmap.CacheOption = BitmapCacheOption.OnLoad;
                    bitmap.CreateOptions = BitmapCreateOptions.IgnoreColorProfile;
                    bitmap.StreamSource = new System.IO.MemoryStream(imageData);
                    bitmap.DecodePixelWidth = 436; // Decode to display size
                    bitmap.EndInit();
                    bitmap.Freeze(); // Make thread-safe
                    return bitmap;
                });
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Failed to load image: {ex.Message}");
                return null;
            }
        }

        private void CardListBox_SelectionChanged (object sender, SelectionChangedEventArgs e)
        {
            if (CardListBox.SelectedItem is Card selectedCard)
            {
                DisplayCardDetails(selectedCard);
            }
        }

        private async void DisplayCardDetails(Card card)
        {
            CardNumber.Text = card.Number.ToString();
            CardName.Text = card.Name;
            CardRarity.Text = card.Rarity;
            CardPrice.Text = $"${card.Price:F2}";
            
            // Updated to include time (hour)
            PullDate.Text = card.PullDate?.ToString("yyyy-MM-dd HH:mm") ?? "N/A";

            // Set edition name
            var editionName = editionMap.FirstOrDefault(x => x.Value.Id == card.EditionId).Key ?? "Unknown";
            CardEdition.Text = editionName;

            // Load image from cache or download
            if (!string.IsNullOrEmpty(card.Image))
            {
                try
                {
                    // Check cache first
                    if (imageCache.TryGetValue(card.Image, out var cachedImage))
                    {
                        CardImage.Source = cachedImage;
                        System.Diagnostics.Debug.WriteLine("Image loaded from cache!");
                    }
                    else
                    {
                        // Not in cache, load and cache it
                        System.Diagnostics.Debug.WriteLine("Image not in cache, loading...");
                        var bitmap = await LoadImageAsync(card.Image);
                        if (bitmap != null)
                        {
                            imageCache.TryAdd(card.Image, bitmap);
                            CardImage.Source = bitmap;
                        }
                        else
                        {
                            CardImage.Source = null;
                        }
                    }
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Image loading error: {ex.Message}");
                    CardImage.Source = null;
                }
            }
            else
            {
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
        }

        private async void AddCard_Click(object sender, RoutedEventArgs e)
        {
            if (EditionSelector.SelectedItem is string selectedEdition)
            {
                var editionData = editionMap[selectedEdition];
                var dialog = new AddCardDialog(editionData.Id, editionData.Identifier, connStr);
                
                if (dialog.ShowDialog() == true)
                {
                    // Refresh the card list
                    await LoadCardsForEditionAsync(selectedEdition);
                    
                    // Select the newly added card
                    if (dialog.NewCardId.HasValue && CardListBox.ItemsSource is List<Card> cards)
                    {
                        var newCard = cards.FirstOrDefault(c => c.Id == dialog.NewCardId.Value);
                        if (newCard != null)
                        {
                            CardListBox.SelectedItem = newCard;
                            CardListBox.ScrollIntoView(newCard);
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

                        // Remove from cache if exists
                        if (!string.IsNullOrEmpty(card.Image))
                        {
                            imageCache.TryRemove(card.Image, out _);
                        }

                        // Refresh the card list
                        if (EditionSelector.SelectedItem is string selectedEdition)
                        {
                            await LoadCardsForEditionAsync(selectedEdition);
                        }

                        MessageBox.Show($"'{card.Name}' has been deleted.", "Success",
                            MessageBoxButton.OK, MessageBoxImage.Information);
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show($"Error deleting card: {ex.Message}", "Error",
                            MessageBoxButton.OK, MessageBoxImage.Error);
                    }
                }
            }
        }

        private void AddEdition_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new AddEditionDialog(connStr);
            
            if (dialog.ShowDialog() == true)
            {
                // Reload editions
                LoadEditions();
                
                // The new edition will be automatically selected if it's the first one
                // Or you can select the last one (newly added)
                if (EditionSelector.Items.Count > 0)
                {
                    EditionSelector.SelectedIndex = EditionSelector.Items.Count - 1;
                }
            }
        }
    }
}