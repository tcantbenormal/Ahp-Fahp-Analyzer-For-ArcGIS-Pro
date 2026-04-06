using System.Windows;

namespace AhpFahpAnalyzer
{
    public partial class ReclassManagerView : Window
    {
        public ReclassManagerView()
        {
            InitializeComponent();
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            this.DialogResult = true;
            this.Close();
        }
    }
}
