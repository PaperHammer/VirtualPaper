using System.Windows;
using System.Windows.Media.Animation;

namespace VirtualPaper.Views
{
    /// <summary>
    /// SplashWindow.xaml 的交互逻辑
    /// </summary>
    public partial class SplashWindow : Window
    {
        private readonly double fadeInDuration = 200;
        private readonly double fadeOutDuration = 200;

        public SplashWindow()
        {
            InitializeComponent();
        }

        public SplashWindow(double fadeInDuration, double fadeOutDuration) : this()
        {
            this.fadeInDuration = fadeInDuration;
            this.fadeOutDuration = fadeOutDuration;
        }

        private void Window_Closing(object? sender, System.ComponentModel.CancelEventArgs e)
        {
            this.Closing -= Window_Closing;
            e.Cancel = true;
            var anim = new DoubleAnimation(0, (Duration)TimeSpan.FromMilliseconds(fadeOutDuration));
            anim.Completed += (s, _) => this.Close();
            this.BeginAnimation(UIElement.OpacityProperty, anim);
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            var anim = new DoubleAnimation(0, 1, (Duration)TimeSpan.FromMilliseconds(fadeInDuration));
            this.BeginAnimation(UIElement.OpacityProperty, anim);
        }
    }
}
