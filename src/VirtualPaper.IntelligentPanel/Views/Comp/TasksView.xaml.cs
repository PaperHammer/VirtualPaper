using System.Collections.ObjectModel;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using VirtualPaper.IntelligentPanel.Models;

// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.

namespace VirtualPaper.IntelligentPanel.Views.Comp {
    public sealed partial class TasksView : UserControl {
        public ObservableCollection<StyleTransferOutput> Tasks {
            get { return (ObservableCollection<StyleTransferOutput>)GetValue(TasksProperty); }
            set { SetValue(TasksProperty, value); }
        }
        public static readonly DependencyProperty TasksProperty =
            DependencyProperty.Register(nameof(Tasks), typeof(ObservableCollection<StyleTransferOutput>), typeof(TasksView), new PropertyMetadata(null));

        public TasksView() {
            InitializeComponent();
        }
    }
}
