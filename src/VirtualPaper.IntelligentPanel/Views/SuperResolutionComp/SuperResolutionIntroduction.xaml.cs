using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.

namespace VirtualPaper.IntelligentPanel.Views.SuperResolutionComp {
    public sealed partial class SuperResolutionIntroduction : UserControl {
        public SuperResolutionIntroduction() {
            InitializeComponent();
            Loaded += OnLoaded;
            Unloaded += OnUnloaded;
        }

        private void OnLoaded(object sender, RoutedEventArgs e) {
        }

        private void OnUnloaded(object sender, RoutedEventArgs e) {
        }

        private readonly string _intelli_Demo_SuperResolution_Origin = "Intelli_Demo_SuperResolution_Origin";
        private readonly string _intelli_Demo_SuperResolution_Result = "Intelli_Demo_SuperResolution_Result";
    }
}
