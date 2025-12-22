using Newtonsoft.Json.Linq;
using System.Net.Http;
using System.Windows;

namespace WpfApp1
{
    public class PokemonTcgService
    {
        private static readonly HttpClient _httpClient; // Static for reuse
        private const string API_BASE_URL = "https://api.tcgdex.net/v2/en";

        // Static constructor to initialize HttpClient once
        static PokemonTcgService()
        {
            _httpClient = new HttpClient
            {
                Timeout = TimeSpan.FromSeconds(10) // Add timeout
            };
            // Enable compression
            _httpClient.DefaultRequestHeaders.Add("Accept-Encoding", "gzip, deflate");
        }

        public async Task<CardData?> GetCardByNumberAsync(string cardNumber)
        {
            try
            {
                // Search by card number - the API uses the format "set-number"
                string url = $"{API_BASE_URL}/cards/{cardNumber}";
                var response = await _httpClient.GetStringAsync(url);
                var card = JObject.Parse(response);
                
                if (card == null)
                {
                    return null;
                }


                return new CardData
                {
                    Number = card["id"]?.ToString() ?? cardNumber,
                    Name = card["name"]?.ToString() ?? "Unknown",
                    Rarity = card["rarity"]?.ToString() ?? "Unknown",
                    ImageUrl = card["image"]?.ToString() + "/low.webp",
                    Set = card["set"]?["name"]?.ToString() ?? "Unknown",
                    Price = ExtractPrice(card)
                };
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"API Error: {ex.Message}");
                return null;
            }
        }

        private decimal ExtractPrice(JToken card)
        {
            try
            {
                // Remove the MessageBox - it blocks the UI!
                //MessageBox.Show(card["pricing"]?.ToString());

                // Try Cardmarket prices
                var cardmarketPrice = card["pricing"]?["cardmarket"];

                if (cardmarketPrice != null)
                {
                    var categoriesCM = new[] { "avg1", "avg1-holo" };

                    foreach (var category in categoriesCM)
                    {
                        var cmPrices = cardmarketPrice[category];
                        if (cmPrices != null && decimal.TryParse(cmPrices.ToString(), out decimal cmPrice))
                        {
                            return cmPrice;
                        }
                    }
                }

                // Try to get TCGPlayer market price
                var tcgPrices = card["pricing"]?["tcgplayer"];
                if (tcgPrices != null)
                {
                    // Try different price categories
                    var categories = new[] { "normal", "holofoil", "reverse-holofoil", "1st-edition", "1st-edition-holofoil", "unlimited", "unlimited-holofoil" };
                    
                    foreach (var category in categories)
                    {
                        var marketPrice = tcgPrices[category]?["marketPrice"];
                        if (marketPrice != null && decimal.TryParse(marketPrice.ToString(), out decimal price))
                        {
                            return price;
                        }
                    }
                }

                
            }
            catch { }

            return 0;
        }

        public async Task<byte[]?> DownloadImageAsync(string imageUrl)
        {
            try
            {
                return await _httpClient.GetByteArrayAsync(imageUrl);
            }
            catch
            {
                return null;
            }
        }
    }

    public class CardData
    {
        public string Number { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string Rarity { get; set; } = string.Empty;
        public string? ImageUrl { get; set; }
        public string Set { get; set; } = string.Empty;
        public decimal Price { get; set; }
    }
}