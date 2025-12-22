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
        public int Copies { get; set; } // Added Copies field
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
            
            LoadEditions();
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
            
            CardDisplayPanel.BeginAnimation(OpacityProperty, fadeOut);
            await Task.Delay(150);

            var fadeIn = new DoubleAnimation
            {
                From = 0,
                To = 1,
                Duration = TimeSpan.FromMilliseconds(200)
            };
            
            CardDisplayPanel.BeginAnimation(OpacityProperty, fadeIn);
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
                    // Temporarily disable selection changed to prevent animation conflicts
                    isAnimating = true;
                    
                    await LoadCardsForEditionAsync(selectedEdition);
                    
                    if (dialog.NewCardId.HasValue && CardListBox.ItemsSource is List<Card> cards)
                    {
                        var newCard = cards.FirstOrDefault(c => c.Id == dialog.NewCardId.Value);
                        if (newCard != null)
                        {
                            // First, clear the display to avoid showing old card
                            ClearCardDetails();
                            
                            // Small delay to ensure UI updates
                            await Task.Delay(50);
                            
                            // Now select the new card
                            CardListBox.SelectedItem = newCard;
                            CardListBox.ScrollIntoView(newCard);
                            
                            // Manually trigger display with animation
                            isAnimating = false;
                            await AnimateNewCardDisplay(newCard);
                        }
                        else
                        {
                            isAnimating = false;
                        }
                    }
                    else
                    {
                        isAnimating = false;
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
    }
}