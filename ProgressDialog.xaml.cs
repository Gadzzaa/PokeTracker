using System.Windows;

namespace WpfApp1
{
    public partial class ProgressDialog : Window
    {
        public ProgressDialog ()
        {
            InitializeComponent();
        }

        public void UpdateProgress (int current, int total, int successful, int failed)
        {
            Dispatcher.Invoke(() =>
            {
                double percentage = total > 0 ? (double)current / total * 100 : 0;
                UpdateProgressBar.Value = percentage;

                StatusText.Text = $"Updating card {current} of {total}...";
                ProgressText.Text = $"{current} / {total} cards updated";
                DetailsText.Text = $"✓ {successful} successful • ✗ {failed} failed";
            });
        }

        public void SetCompleted (int total, int successful, int failed)
        {
            Dispatcher.Invoke(() =>
            {
                UpdateProgressBar.Value = 100;
                StatusText.Text = "Update completed!";
                ProgressText.Text = $"{total} / {total} cards processed";
                DetailsText.Text = $"✓ {successful} successful • ✗ {failed} failed";
            });
        }
    }
}