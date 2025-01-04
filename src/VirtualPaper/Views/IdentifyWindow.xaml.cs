using System.Windows;
using VirtualPaper.ViewModels;

namespace VirtualPaper.Views
{
    /// <summary>
    /// IdentifyWindow.xaml 的交互逻辑
    /// </summary>
    public partial class IdentifyWindow : Window
    {        
        public IdentifyWindow(int index)
        {
            InitializeComponent();
            _viewMdoel = new(index);
            this.DataContext = _viewMdoel;
        }

        private IdentifyWindowViewModel _viewMdoel;
    }
}
