using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media.Imaging;
using System.Windows.Media.Animation;
using System.Windows.Media;
using Dapper;
using MySql.Data.MySqlClient;
using System.Collections.Concurrent;
using System.IO;
using System.Net.Http;

namespace WpfApp1
{
    public class CachedImage
    {
        public BitmapImage Image { get; set; }
        public DateTime LastAccessed { get; set; }
        public long EstimatedSize { get; set; }

        public CachedImage (BitmapImage image)
        {
            Image = image;
            LastAccessed = DateTime.Now;
            EstimatedSize = image.PixelWidth * image.PixelHeight * 4;
        }
    }

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

    public class EditionWithPrice
    {
        public string Name { get; set; } = string.Empty;
        public decimal TotalValue { get; set; }
    }

    public partial class MainWindow : Window
    {
        private string connStr;
        private DatabaseConfig dbConfig;
        private Dictionary<string, (int Id, string Identifier)> editionMap;
        private ConcurrentDictionary<string, CachedImage> imageCache;
        private bool isAnimating = false;
        private Card? pendingCard = null;

        private const int MAX_CACHE_SIZE_MB = 1000;
        private const int MAX_CACHE_ITEMS = 100;
        private long currentCacheSize = 0;

        public MainWindow ()
        {
            dbConfig = DatabaseConfig.Load();
            connStr = dbConfig.GetConnectionString();

            editionMap = new Dictionary<string, (int Id, string Identifier)>();
            imageCache = new ConcurrentDictionary<string, CachedImage>();

            InitializeComponent();

            DisplayArea.RenderTransform = new TranslateTransform();

            this.PreviewKeyDown += MainWindow_PreviewKeyDown;

            if (!InitializeDatabase())
            {
                ShowDatabaseSettingsDialog();
            }

            LoadEditions();

            this.Loaded += MainWindow_Loaded;
        }

        private async void MainWindow_Loaded (object sender, RoutedEventArgs e)
        {
            await CheckAndUpdatePricesAsync();
        }

        protected override void OnClosed (EventArgs e)
        {
            base.OnClosed(e);

            this.PreviewKeyDown -= MainWindow_PreviewKeyDown;
            ClearImageCache();
        }

        private void ClearImageCache ()
        {
            foreach (var entry in imageCache.Values)
            {
                entry.Image.StreamSource?.Dispose();
            }
            imageCache.Clear();
            currentCacheSize = 0;

            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();
        }

        private void TrimCache ()
        {
            long maxSize = MAX_CACHE_SIZE_MB * 1024 * 1024;

            if (imageCache.Count <= MAX_CACHE_ITEMS && currentCacheSize <= maxSize)
            {
                return;
            }

            HashSet<string> protectedUrls = new HashSet<string>();
            if (CardListBox.ItemsSource is List<Card> cards)
            {
                foreach (var card in cards.Where(c => !string.IsNullOrEmpty(c.Image)))
                {
                    protectedUrls.Add(card.Image!);
                }
            }

            var sortedEntries = imageCache
                .Where(kvp => !protectedUrls.Contains(kvp.Key))
                .OrderBy(kvp => kvp.Value.LastAccessed)
                .ToList();

            int removed = 0;
            foreach (var entry in sortedEntries)
            {
                if (imageCache.Count <= MAX_CACHE_ITEMS / 2 && currentCacheSize <= maxSize / 2)
                {
                    break;
                }

                if (imageCache.TryRemove(entry.Key, out var cached))
                {
                    cached.Image.StreamSource?.Dispose();
                    currentCacheSize -= cached.EstimatedSize;
                    removed++;
                }
            }

            if (removed > 10)
            {
                GC.Collect();
            }
        }

        private bool InitializeDatabase ()
        {
            try
            {
                string connStrWithoutDb = dbConfig.GetConnectionStringWithoutDb();

                using (var conn = new MySqlConnection(connStrWithoutDb))
                {
                    conn.Open();

                    string createDbSql = @"CREATE DATABASE IF NOT EXISTS " + dbConfig.Database + @" 
                                      CHARACTER SET utf8mb4 
                                      COLLATE utf8mb4_general_ci;";
                    conn.Execute(createDbSql);
                }

                using (var conn = new MySqlConnection(connStr))
                {
                    conn.Open();

                    string createEditionsTableSql = @"
                        CREATE TABLE IF NOT EXISTS card_editions (
                            id INT(11) NOT NULL AUTO_INCREMENT,
                            type TEXT CHARACTER SET utf8mb4 COLLATE utf8mb4_general_ci NULL,
                            nr_pachete INT(11) NOT NULL DEFAULT 0,
                            edition_identifier TEXT CHARACTER SET utf8mb4 COLLATE utf8mb4_general_ci NOT NULL,
                            PRIMARY KEY (id)
                        ) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_general_ci;";

                    conn.Execute(createEditionsTableSql);

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

                    try
                    {
                        string addForeignKeySql = @"
                            ALTER TABLE cards 
                            ADD CONSTRAINT fk_edition 
                            FOREIGN KEY (edition_id) 
                            REFERENCES card_editions(id) 
                            ON DELETE CASCADE;";

                        conn.Execute(addForeignKeySql);
                    }
                    catch { }
                }

                return true;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error initializing database: {ex.Message}\n\nPlease configure database settings.",
                    "Database Initialization Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);

                return false;
            }
        }

        private void DatabaseSettings_Click (object sender, RoutedEventArgs e)
        {
            ShowDatabaseSettingsDialog();
        }

        private void ShowDatabaseSettingsDialog ()
        {
            var dialog = new DatabaseSettingsDialog(dbConfig);
            if (dialog.ShowDialog() == true)
            {
                var result = MessageBox.Show(
                    "Database settings have been updated.\n\nWould you like to restart the application now?",
                    "Restart Required",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Question);

                if (result == MessageBoxResult.Yes)
                {
                    System.Diagnostics.Process.Start(
                        Environment.ProcessPath ?? Application.ResourceAssembly.Location);
                    Application.Current.Shutdown();
                }
            }
        }

        private void LoadEditions ()
        {
            using (var conn = new MySqlConnection(connStr))
            {
                var editions = conn.Query<(int Id, string Type, string EditionIdentifier)>(
                    "SELECT id, type, edition_identifier FROM card_editions").ToList();

                editionMap.Clear();
                var editionList = new List<EditionWithPrice>();
                decimal totalCollectionValue = 0m;

                foreach (var edition in editions)
                {
                    editionMap[edition.Type] = (edition.Id, edition.EditionIdentifier);

                    // Calculate total value for this edition
                    string sql = @"SELECT COALESCE(SUM(price * copies), 0) as TotalValue
                          FROM cards 
                          WHERE edition_id = @EditionId";

                    decimal editionValue = conn.QueryFirstOrDefault<decimal>(sql, new { EditionId = edition.Id });

                    editionList.Add(new EditionWithPrice
                    {
                        Name = edition.Type,
                        TotalValue = editionValue
                    });

                    totalCollectionValue += editionValue;
                }

                EditionSelector.ItemsSource = editionList;
                TotalCollectionValue.Text = $"${totalCollectionValue:F2}";

                if (editionList.Count > 0)
                {
                    EditionSelector.SelectedIndex = 0;
                }
            }
        }

        private async void EditionSelector_SelectionChanged (object sender, SelectionChangedEventArgs e)
        {
            if (EditionSelector.SelectedItem is EditionWithPrice selectedEdition)
            {
                ClearCardDetails();
                await AnimateEditionChange();

                TrimCache();

                await LoadCardsForEditionAsync(selectedEdition.Name);
            }
        }

        private async Task AnimateEditionChange ()
        {
            var fadeOut = new DoubleAnimation
            {
                From = 1,
                To = 0,
                Duration = TimeSpan.FromMilliseconds(150)
            };

            CardDisplayPanel.BeginAnimation(OpacityProperty, fadeOut);
            CardListBox.BeginAnimation(OpacityProperty, fadeOut);
            await Task.Delay(150);

            var fadeIn = new DoubleAnimation
            {
                From = 0,
                To = 1,
                Duration = TimeSpan.FromMilliseconds(200)
            };

            CardDisplayPanel.BeginAnimation(OpacityProperty, fadeIn);
            CardListBox.BeginAnimation(OpacityProperty, fadeIn);
        }

        private async Task LoadCardsForEditionAsync (string editionType, bool selectFirst = true)
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

            if (cards.Count > 0 && selectFirst)
            {
                isAnimating = true;

                CardListBox.SelectedIndex = 0;
                await Task.Delay(50);

                if (CardListBox.SelectedItem is Card firstCard)
                {
                    DisplayCardDetails(firstCard);
                }

                isAnimating = false;
                pendingCard = null;
            }
            else if (cards.Count == 0)
            {
                ClearCardDetails();
            }

            await PreloadImagesAsync(cards);
        }

        private async Task PreloadImagesAsync (List<Card> cards)
        {
            var imagesToLoad = cards
                .Where(c => !string.IsNullOrEmpty(c.Image) && !imageCache.ContainsKey(c.Image))
                .Select(c => c.Image!)
                .Distinct()
                .Take(10)
                .ToList();

            if (imagesToLoad.Count == 0)
                return;

            var tasks = imagesToLoad.Select(async imageUrl =>
            {
                try
                {
                    var bitmap = await LoadImageAsync(imageUrl);
                    if (bitmap != null)
                    {
                        var cached = new CachedImage(bitmap);
                        if (imageCache.TryAdd(imageUrl, cached))
                        {
                            currentCacheSize += cached.EstimatedSize;
                        }
                    }
                }
                catch { }
            });

            await Task.WhenAll(tasks);

            TrimCache();
        }

        private async Task<BitmapImage?> LoadImageAsync (string imageUrl)
        {
            try
            {
                byte[]? imageData = await Task.Run(async () =>
                {
                    using var client = new System.Net.Http.HttpClient();
                    client.Timeout = TimeSpan.FromSeconds(10);
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
            catch
            {
                return null;
            }
        }

        private async void AddCard_Click (object sender, RoutedEventArgs e)
        {
            if (EditionSelector.SelectedItem is EditionWithPrice selectedEdition)
            {
                var editionData = editionMap[selectedEdition.Name];
                var dialog = new AddCardDialog(editionData.Id, editionData.Identifier, connStr);

                if (dialog.ShowDialog() == true)
                {
                    if (dialog.NewCardId.HasValue)
                    {
                        if (CardListBox.ItemsSource is List<Card> cards)
                        {
                            var existingCard = cards.FirstOrDefault(c => c.Id == dialog.NewCardId.Value);

                            if (existingCard != null)
                            {
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
                                        existingCard.Copies = updatedCard.Copies;

                                        if (CardListBox.SelectedItem is Card currentCard && currentCard.Id == updatedCard.Id)
                                        {
                                            CardCopies.Text = updatedCard.Copies.ToString();
                                            CardListBox.Items.Refresh();
                                        }
                                        else
                                        {
                                            CardListBox.Items.Refresh();
                                            CardListBox.SelectedItem = existingCard;
                                            CardListBox.ScrollIntoView(existingCard);
                                        }
                                    }
                                }
                            }
                            else
                            {
                                isAnimating = true;
                                await LoadCardsForEditionAsync(selectedEdition.Name, selectFirst: false);

                                var newCard = ((List<Card>)CardListBox.ItemsSource).FirstOrDefault(c => c.Id == dialog.NewCardId.Value);
                                if (newCard != null)
                                {
                                    ClearCardDetails();
                                    DisplayArea.Opacity = 0;

                                    await Task.Delay(50);

                                    isAnimating = false;

                                    CardListBox.SelectedItem = newCard;
                                    CardListBox.ScrollIntoView(newCard);
                                }
                                else
                                {
                                    isAnimating = false;
                                }
                            }
                        }

                        // Reload editions while preserving selection WITHOUT triggering SelectionChanged
                        string currentEditionName = selectedEdition.Name;

                        // Temporarily unsubscribe from SelectionChanged event
                        EditionSelector.SelectionChanged -= EditionSelector_SelectionChanged;

                        LoadEditions();

                        // Restore the selection to the current edition
                        var editionToSelect = ((List<EditionWithPrice>)EditionSelector.ItemsSource)
                            .FirstOrDefault(e => e.Name == currentEditionName);
                        if (editionToSelect != null)
                        {
                            EditionSelector.SelectedItem = editionToSelect;
                        }

                        // Re-subscribe to SelectionChanged event
                        EditionSelector.SelectionChanged += EditionSelector_SelectionChanged;
                    }
                }
            }
            else
            {
                MessageBox.Show("Please select an edition first.", "No Edition Selected",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        private async Task AnimateNewCardDisplay (Card card)
        {
            DisplayArea.Opacity = 0;
            DisplayCardDetails(card);
            await Task.Delay(50);

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
            if (CardListBox.SelectedItem is Card selectedCard)
            {
                if (isAnimating)
                {
                    pendingCard = selectedCard;
                    return;
                }

                await AnimateCardSelection(selectedCard);

                while (pendingCard != null)
                {
                    var cardToShow = pendingCard;
                    pendingCard = null;
                    await AnimateCardSelection(cardToShow);
                }
            }
        }

        private async Task AnimateCardSelection (Card selectedCard)
        {
            isAnimating = true;

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

            await Task.Delay(150);

            DisplayCardDetails(selectedCard);

            await Task.Delay(50);

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

        private void DisplayCardDetails (Card card)
        {
            CardNumber.Text = card.Number.ToString();
            CardName.Text = card.Name;
            CardRarity.Text = card.Rarity;
            CardPrice.Text = $"${card.Price:F2}";
            PullDate.Text = card.PullDate?.ToString("yyyy-MM-dd HH:mm") ?? "N/A";

            var editionName = editionMap.FirstOrDefault(x => x.Value.Id == card.EditionId).Key ?? "Unknown";
            CardEdition.Text = editionName;

            if (!string.IsNullOrEmpty(card.Image))
            {
                if (imageCache.TryGetValue(card.Image, out var cachedImage))
                {
                    cachedImage.LastAccessed = DateTime.Now;
                    CardImage.Source = cachedImage.Image;
                }
                else
                {
                    _ = LoadAndDisplayImageAsync(card.Image);
                }
            }
            else
            {
                CardImage.Source = null;
            }

            CardCopies.Text = card.Copies.ToString();
        }

        private async Task LoadAndDisplayImageAsync (string imageUrl)
        {
            try
            {
                var bitmap = await LoadImageAsync(imageUrl);
                if (bitmap != null)
                {
                    var cached = new CachedImage(bitmap);
                    if (imageCache.TryAdd(imageUrl, cached))
                    {
                        currentCacheSize += cached.EstimatedSize;
                        TrimCache();
                    }
                    CardImage.Source = bitmap;
                }
                else
                {
                    CardImage.Source = null;
                }
            }
            catch
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
            CardCopies.Text = "0";
        }

        private async void AddEdition_Click (object sender, RoutedEventArgs e)
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

        private async void DeleteCard_Click (object sender, RoutedEventArgs e)
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

                        if (EditionSelector.SelectedItem is EditionWithPrice selectedEdition)
                        {
                            string currentEditionName = selectedEdition.Name;

                            await LoadCardsForEditionAsync(selectedEdition.Name);

                            // Temporarily unsubscribe from SelectionChanged event
                            EditionSelector.SelectionChanged -= EditionSelector_SelectionChanged;

                            LoadEditions();

                            var editionToSelect = ((List<EditionWithPrice>)EditionSelector.ItemsSource)
                                .FirstOrDefault(e => e.Name == currentEditionName);
                            if (editionToSelect != null)
                            {
                                EditionSelector.SelectedItem = editionToSelect;
                            }

                            // Re-subscribe to SelectionChanged event
                            EditionSelector.SelectionChanged += EditionSelector_SelectionChanged;
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

        private void RemoveCopy_Click (object sender, RoutedEventArgs e)
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

                        card.Copies--;
                        CardCopies.Text = card.Copies.ToString();
                        CardListBox.Items.Refresh();

                        // Reload editions to update prices while preserving selection
                        if (EditionSelector.SelectedItem is EditionWithPrice selectedEdition)
                        {
                            string currentEditionName = selectedEdition.Name;

                            // Temporarily unsubscribe from SelectionChanged event
                            EditionSelector.SelectionChanged -= EditionSelector_SelectionChanged;

                            LoadEditions();

                            var editionToSelect = ((List<EditionWithPrice>)EditionSelector.ItemsSource)
                                .FirstOrDefault(e => e.Name == currentEditionName);
                            if (editionToSelect != null)
                            {
                                EditionSelector.SelectedItem = editionToSelect;
                            }

                            // Re-subscribe to SelectionChanged event
                            EditionSelector.SelectionChanged += EditionSelector_SelectionChanged;
                        }
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show($"Error removing copy: {ex.Message}", "Error",
                            MessageBoxButton.OK, MessageBoxImage.Error);
                    }
                }
            }
        }

        private void MainWindow_PreviewKeyDown (object sender, KeyEventArgs e)
        {
            if (isAnimating || CardListBox.ItemsSource == null)
                return;

            if (e.Key == Key.Left || e.Key == Key.Right)
            {
                var items = CardListBox.Items;
                if (items.Count == 0)
                    return;

                int currentIndex = CardListBox.SelectedIndex;
                int newIndex = currentIndex;

                if (e.Key == Key.Right)
                {
                    newIndex = currentIndex + 1;
                    if (newIndex >= items.Count)
                        newIndex = 0;
                }
                else if (e.Key == Key.Left)
                {
                    newIndex = currentIndex - 1;
                    if (newIndex < 0)
                        newIndex = items.Count - 1;
                }

                if (newIndex != currentIndex)
                {
                    CardListBox.SelectedIndex = newIndex;
                    CardListBox.ScrollIntoView(CardListBox.SelectedItem);
                    e.Handled = true;
                }
            }
        }

        private async Task CheckAndUpdatePricesAsync ()
        {
            try
            {
                if (dbConfig.LastPriceUpdate.HasValue &&
                    dbConfig.LastPriceUpdate.Value.Date == DateTime.Today)
                {
                    return;
                }

                List<(int Id, int Number, string EditionIdentifier)> cardsToUpdate;
                using (var conn = new MySqlConnection(connStr))
                {
                    string sql = @"
                SELECT c.id as Id, c.number as Number, ce.edition_identifier as EditionIdentifier
                FROM cards c
                INNER JOIN card_editions ce ON c.edition_id = ce.id
                ORDER BY c.id";

                    cardsToUpdate = conn.Query<(int Id, int Number, string EditionIdentifier)>(sql).ToList();
                }

                if (cardsToUpdate.Count == 0)
                {
                    return;
                }

                var result = MessageBox.Show(
                    $"Found {cardsToUpdate.Count} cards in your collection.\n\nWould you like to update all card prices?\n(This happens once per day and may take a few moments)",
                    "Update Card Prices",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Question);

                if (result != MessageBoxResult.Yes)
                {
                    dbConfig.LastPriceUpdate = DateTime.Now;
                    dbConfig.Save();
                    return;
                }

                var (successful, failed, failedCards) = await UpdateAllCardPricesAsync(cardsToUpdate);

                dbConfig.LastPriceUpdate = DateTime.Now;
                dbConfig.Save();

                string message = $"Price update complete!\n\n";
                message += $"Total cards: {cardsToUpdate.Count}\n";
                message += $"✓ Successfully updated: {successful}\n";
                message += $"✗ Failed to update: {failed}";

                if (failed > 0)
                {
                    message += $"\n\n📝 Detailed error log saved to:\n";
                    message += $"{Path.GetDirectoryName(ErrorLog.GetTodaysLogPath("price_update_failures"))}";
                    message += $"\n\nWould you like to open the log folder?";

                    var openLog = MessageBox.Show(
                        message,
                        "Update Complete",
                        MessageBoxButton.YesNo,
                        MessageBoxImage.Warning);

                    if (openLog == MessageBoxResult.Yes)
                    {
                        try
                        {
                            string logDir = Path.GetDirectoryName(ErrorLog.GetTodaysLogPath("price_update_failures"))!;
                            System.Diagnostics.Process.Start("explorer.exe", logDir);
                        }
                        catch { }
                    }
                }
                else
                {
                    MessageBox.Show(
                        message,
                        "Update Complete",
                        MessageBoxButton.OK,
                        MessageBoxImage.Information);
                }

                if (EditionSelector.SelectedItem is EditionWithPrice selectedEdition)
                {
                    await LoadCardsForEditionAsync(selectedEdition.Name);
                    LoadEditions();
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    $"Error updating prices: {ex.Message}\n\nYou can try again next time you open the app.",
                    "Update Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
            }
        }

        private async Task<(int successful, int failed, List<string> failedCards)> UpdateAllCardPricesAsync (List<(int Id, int Number, string EditionIdentifier)> cardsToUpdate)
        {
            ProgressDialog? progressDialog = null;
            int successful = 0;
            int failed = 0;
            var failedCards = new List<string>();

            ErrorLog.Initialize();

            try
            {
                await Dispatcher.InvokeAsync(() =>
                {
                    progressDialog = new ProgressDialog();

                    try
                    {
                        if (this.IsLoaded)
                        {
                            progressDialog.Owner = this;
                        }
                    }
                    catch { }

                    progressDialog.Show();
                });

                var apiService = new PokemonTcgService();
                int processed = 0;

                int batchSize = 5;
                for (int i = 0; i < cardsToUpdate.Count; i += batchSize)
                {
                    var batch = cardsToUpdate.Skip(i).Take(batchSize);
                    var tasks = batch.Select(async card =>
                    {
                        string formattedNumber = card.Number.ToString("D3");
                        string fullCardId = $"{card.EditionIdentifier}-{formattedNumber}";

                        try
                        {
                            var cardData = await apiService.GetCardByNumberAsync(fullCardId);

                            if (cardData == null)
                            {
                                string reason = "Card not found in API";
                                ErrorLog.LogPriceUpdateFailure(fullCardId, card.EditionIdentifier, card.Number, reason);
                                return (false, fullCardId + " - " + reason, 0m);
                            }

                            if (cardData.Price <= 0)
                            {
                                string reason = $"Invalid price returned (${cardData.Price:F2})";
                                ErrorLog.LogPriceUpdateFailure(fullCardId, card.EditionIdentifier, card.Number, reason);
                                return (false, fullCardId + " - " + reason, 0m);
                            }

                            decimal oldPrice = 0m;
                            using (var conn = new MySqlConnection(connStr))
                            {
                                oldPrice = conn.QueryFirstOrDefault<decimal>(
                                    "SELECT price FROM cards WHERE id = @Id",
                                    new { Id = card.Id });

                                string updateSql = "UPDATE cards SET price = @Price WHERE id = @Id";
                                conn.Execute(updateSql, new { Price = cardData.Price, Id = card.Id });
                            }

                            ErrorLog.LogPriceUpdateSuccess(fullCardId, oldPrice, cardData.Price);
                            return (true, string.Empty, cardData.Price);
                        }
                        catch (HttpRequestException ex)
                        {
                            string reason = $"Network error: {ex.Message}";
                            ErrorLog.LogPriceUpdateFailure(fullCardId, card.EditionIdentifier, card.Number, reason);
                            return (false, fullCardId + " - Network error", 0m);
                        }
                        catch (TimeoutException)
                        {
                            string reason = "Request timeout";
                            ErrorLog.LogPriceUpdateFailure(fullCardId, card.EditionIdentifier, card.Number, reason);
                            return (false, fullCardId + " - Timeout", 0m);
                        }
                        catch (Exception ex)
                        {
                            string reason = $"Unknown error: {ex.Message}";
                            ErrorLog.LogPriceUpdateFailure(fullCardId, card.EditionIdentifier, card.Number, reason);
                            return (false, fullCardId + " - " + ex.Message, 0m);
                        }
                    });

                    var results = await Task.WhenAll(tasks);

                    foreach (var (success, errorInfo, price) in results)
                    {
                        processed++;
                        if (success)
                        {
                            successful++;
                        }
                        else
                        {
                            failed++;
                            if (!string.IsNullOrEmpty(errorInfo))
                            {
                                failedCards.Add(errorInfo);
                            }
                        }

                        progressDialog?.UpdateProgress(processed, cardsToUpdate.Count, successful, failed);
                    }

                    if (i + batchSize < cardsToUpdate.Count)
                    {
                        await Task.Delay(2000);
                    }
                }

                ErrorLog.CreateSummaryReport(cardsToUpdate.Count, successful, failed, failedCards);

                progressDialog?.SetCompleted(cardsToUpdate.Count, successful, failed);
                await Task.Delay(2000);
            }
            finally
            {
                progressDialog?.Close();
            }

            return (successful, failed, failedCards);
        }
    }
}