using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.

namespace VirtualPaper.IntelligentPanel.Views.StyleTransferComp {
    public sealed partial class StyleTransferIntroduction : UserControl {
        public StyleTransferIntroduction() {
            InitializeComponent();
            Loaded += OnLoaded;
            Unloaded += OnUnloaded;
        }

        private void OnLoaded(object sender, RoutedEventArgs e) {
            demoCrossfadeStoryboard.Begin();
        }

        private void IntroductionRoot_SizeChanged(object sender, SizeChangedEventArgs e) {
            // Use this page's available width rather than the entire window.
            VisualStateManager.GoToState(this, e.NewSize.Width >= 960 ? "WideLayout" : "NarrowLayout", false);
        }

        private void OnUnloaded(object sender, RoutedEventArgs e) {
            demoCrossfadeStoryboard.Stop();
        }

        private readonly string _intelli_Demo_StyleTransfer_Origin = "Intelli_Demo_StyleTransfer_Origin";
        private readonly string _intelli_Demo_StyleTransfer_Result = "Intelli_Demo_StyleTransfer_Result";
    }
}
