using System.Collections.ObjectModel;
using Microsoft.UI.Xaml.Controls;
using VirtualPaper.IntelligentPanel.Models;

// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.

namespace VirtualPaper.IntelligentPanel.Views.Comp {
    public sealed partial class TasksView : UserControl {
        private readonly ObservableCollection<StyleTransferOutput> Tasks = [];

        public TasksView() {
            InitializeComponent();
        }
    }
}
