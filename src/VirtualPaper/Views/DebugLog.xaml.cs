using System.Windows;

namespace VirtualPaper.Views
{
    /// <summary>
    /// DebugLog.xaml 的交互逻辑
    /// </summary>
    public partial class DebugLog : Window
    {
        public DebugLog()
        {
            InitializeComponent();
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            this.Activate();
        }
    }
}
