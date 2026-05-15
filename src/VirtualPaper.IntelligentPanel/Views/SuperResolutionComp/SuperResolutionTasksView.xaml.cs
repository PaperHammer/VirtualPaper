using System.Collections.ObjectModel;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using VirtualPaper.IntelligentPanel.Models;

// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.

namespace VirtualPaper.IntelligentPanel.Views.SuperResolutionComp {
    public sealed partial class SuperResolutionTasksView : UserControl {
        public ObservableCollection<SuperResolutionTaskItem> Tasks {
            get { return (ObservableCollection<SuperResolutionTaskItem>)GetValue(TasksProperty); }
            set { SetValue(TasksProperty, value); }
        }
        public static readonly DependencyProperty TasksProperty =
            DependencyProperty.Register(nameof(Tasks), typeof(ObservableCollection<SuperResolutionTaskItem>), typeof(SuperResolutionTasksView), new PropertyMetadata(null));

        public SuperResolutionTasksView() {
            InitializeComponent();
        }
    }
}
