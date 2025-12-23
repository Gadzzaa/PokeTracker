using System.IO;
using System.Text;

namespace WpfApp1
{
    public static class ErrorLog
    {
        private static string LogDirectory => Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "PokemonCardCollection",
            "Logs"
        );

        private static string GetLogFilePath(string prefix)
        {
            string filename = $"{prefix}_{DateTime.Now:yyyy-MM-dd}.log";
            return Path.Combine(LogDirectory, filename);
        }

        public static void Initialize()
        {
            try
            {
                if (!Directory.Exists(LogDirectory))
                {
                    Directory.CreateDirectory(LogDirectory);
                }
            }
            catch { }
        }

        public static void LogPriceUpdateFailure(string cardId, string editionIdentifier, int cardNumber, string reason)
        {
            try
            {
                Initialize();
                string logPath = GetLogFilePath("price_update_failures");
                
                string logEntry = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] " +
                                $"Card: {cardId} | " +
                                $"Edition: {editionIdentifier} | " +
                                $"Number: {cardNumber} | " +
                                $"Reason: {reason}\n";

                File.AppendAllText(logPath, logEntry);
            }
            catch { }
        }

        public static void LogPriceUpdateSuccess(string cardId, decimal oldPrice, decimal newPrice)
        {
            try
            {
                Initialize();
                string logPath = GetLogFilePath("price_update_success");
                
                string logEntry = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] " +
                                $"Card: {cardId} | " +
                                $"Old Price: ${oldPrice:F2} | " +
                                $"New Price: ${newPrice:F2}\n";

                File.AppendAllText(logPath, logEntry);
            }
            catch { }
        }

        public static string GetTodaysLogPath(string prefix)
        {
            return GetLogFilePath(prefix);
        }

        public static void CreateSummaryReport(int total, int successful, int failed, List<string> failedCards)
        {
            try
            {
                Initialize();
                string reportPath = Path.Combine(LogDirectory, $"update_summary_{DateTime.Now:yyyy-MM-dd_HHmmss}.txt");
                
                var sb = new StringBuilder();
                sb.AppendLine("???????????????????????????????????????????????????");
                sb.AppendLine("       PRICE UPDATE SUMMARY REPORT");
                sb.AppendLine($"       {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
                sb.AppendLine("???????????????????????????????????????????????????");
                sb.AppendLine();
                sb.AppendLine($"Total Cards:        {total}");
                sb.AppendLine($"? Successful:       {successful} ({(total > 0 ? successful * 100.0 / total : 0):F1}%)");
                sb.AppendLine($"? Failed:           {failed} ({(total > 0 ? failed * 100.0 / total : 0):F1}%)");
                sb.AppendLine();
                
                if (failedCards.Count > 0)
                {
                    sb.AppendLine("???????????????????????????????????????????????????");
                    sb.AppendLine("       FAILED CARDS");
                    sb.AppendLine("???????????????????????????????????????????????????");
                    sb.AppendLine();
                    
                    foreach (var card in failedCards)
                    {
                        sb.AppendLine($"  • {card}");
                    }
                }
                
                sb.AppendLine();
                sb.AppendLine("???????????????????????????????????????????????????");
                sb.AppendLine($"Full logs available at:");
                sb.AppendLine($"{LogDirectory}");
                sb.AppendLine("???????????????????????????????????????????????????");

                File.WriteAllText(reportPath, sb.ToString());
            }
            catch { }
        }
    }
}