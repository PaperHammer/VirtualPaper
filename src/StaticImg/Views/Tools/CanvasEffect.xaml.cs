using System;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.

namespace Workloads.Creation.StaticImg.Views.Tools {
    public sealed partial class CanvasEffect : UserControl {
        public event EventHandler<RoutedEventArgs>? CanvasEffectCancel;
        public event EventHandler<RoutedEventArgs>? CanvasEffectCommit;

        public string? ClickedEffectId {
            get { return (string?)GetValue(ClickedEffectIdProperty); }
            set { SetValue(ClickedEffectIdProperty, value); }
        }
        public static readonly DependencyProperty ClickedEffectIdProperty =
            DependencyProperty.Register(nameof(ClickedEffectId), typeof(string), typeof(CanvasEffect), new PropertyMetadata(null));

        public CanvasEffect() {
            InitializeComponent();
        }

        private void SelectCancelBtn_Click(object sender, RoutedEventArgs e) {
            CanvasEffectCancel?.Invoke(this, e);
        }

        private void SelectCommitBtn_Click(object sender, RoutedEventArgs e) {
            CanvasEffectCommit?.Invoke(this, e);
        }
    }
}
