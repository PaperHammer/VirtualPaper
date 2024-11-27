using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using NLog;
using VirtualPaper.UI.ViewModels.Utils;

// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.

namespace VirtualPaper.UI.Views.Utils {
    /// <summary>
    /// An empty page that can be used on its own or navigated to within a Frame.
    /// </summary>
    public sealed partial class DetailedInfoView : Page {
        public double Rating {
            get { return (double)GetValue(RatingProperty); }
            set { SetValue(RatingProperty, value); }
        }
        public static readonly DependencyProperty RatingProperty =
            DependencyProperty.Register("Rating", typeof(double), typeof(DetailedInfoView), new PropertyMetadata(-1.0));

        public double RatingShow {
            get { return (double)GetValue(RatingShowProperty); }
            set { SetValue(RatingShowProperty, value); }
        }
        public static readonly DependencyProperty RatingShowProperty =
            DependencyProperty.Register("RatingShow", typeof(double), typeof(DetailedInfoView), new PropertyMetadata(0.0, (s, e) => {
                var val = (double)e.NewValue;
                ((DetailedInfoView)s).Rating = val == 0 ? -1 : val;
            }));

        public DetailedInfoView(DetailedInfoViewModel viewModel) {
            this.InitializeComponent();
            this.DataContext = viewModel;
            _viewModel = viewModel;
        }

        private async void BtnPreview_Click(object sender, RoutedEventArgs e) {
            await _viewModel.PreviewAsync();
        }

        private void BtnDownload_Click(object sender, RoutedEventArgs e) {

        }

        private async void BtnSubmitScore_Click(object sender, RoutedEventArgs e) {
            //await _viewModel.SubmitSocreAsync(RatingShow);
            if (this.BtnScore.Flyout is Flyout f) {
                f.Hide();
            }
        }

        private void Image_ImageFailed(object sender, ExceptionRoutedEventArgs e) {
            _logger.Error($"RImage loading failed: {e.ErrorMessage}");
        }

        private readonly Logger _logger = LogManager.GetCurrentClassLogger();
        private DetailedInfoViewModel _viewModel;
    }
}
