using System.Collections.ObjectModel;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using VirtualPaper.IntelligentPanel.Models;

// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.

namespace VirtualPaper.IntelligentPanel.Views.StyleTransferComp {
    public sealed partial class StyleTransferTasksView : UserControl {
        public ObservableCollection<StyleTransferTaskItem> Tasks {
            get { return (ObservableCollection<StyleTransferTaskItem>)GetValue(TasksProperty); }
            set { SetValue(TasksProperty, value); }
        }
        public static readonly DependencyProperty TasksProperty =
            DependencyProperty.Register(nameof(Tasks), typeof(ObservableCollection<StyleTransferTaskItem>), typeof(StyleTransferTasksView), new PropertyMetadata(null));

        public StyleTransferTasksView() {
            InitializeComponent();
        }
    }
}
