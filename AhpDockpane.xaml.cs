using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;

namespace AhpFahpAnalyzer
{
    /// <summary>
    /// Interaction logic for AhpDockpaneView.xaml
    /// </summary>
    public partial class AhpDockpaneView : UserControl
    {
        public AhpDockpaneView()
        {
            InitializeComponent();
        }

        private void DataGrid_AutoGeneratingColumn(object sender, DataGridAutoGeneratingColumnEventArgs e)
        {
            if (e.PropertyDescriptor is System.ComponentModel.PropertyDescriptor descriptor)
            {
                e.Column.Header = descriptor.DisplayName;
            }
        }
    }
}
