using System;
using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using VirtualPaper.Models.Mvvm;
using VirtualPaper.UIComponent.Input;
using Windows.UI;

// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.

namespace Workloads.Creation.StaticImg.Views.Tools {
    public sealed partial class ColorPaletteControl : UserControl {
        public event EventHandler<ColorChangeEventArgs> CustomeColorChanged;

        public Color ForegroundColor {
            get { return (Color)GetValue(ForegroundColorProperty); }
            set { SetValue(ForegroundColorProperty, value); }
        }
        public static readonly DependencyProperty ForegroundColorProperty =
            DependencyProperty.Register(nameof(ForegroundColor), typeof(Color), typeof(ColorPaletteControl), new PropertyMetadata(Colors.Transparent));

        public Color BackgroundColor {
            get { return (Color)GetValue(BackgroundColorProperty); }
            set { SetValue(BackgroundColorProperty, value); }
        }
        public static readonly DependencyProperty BackgroundColorProperty =
            DependencyProperty.Register(nameof(BackgroundColor), typeof(Color), typeof(ColorPaletteControl), new PropertyMetadata(Colors.Transparent));

        public ObservableList<Color> CustomColors {
            get { return (ObservableList<Color>)GetValue(CustomColorsProperty); }
            set { SetValue(CustomColorsProperty, value); }
        }
        public static readonly DependencyProperty CustomColorsProperty =
            DependencyProperty.Register(nameof(CustomColors), typeof(ObservableList<Color>), typeof(ColorPaletteControl), new PropertyMetadata(null));

        public ColorPaletteControl() {
            this.InitializeComponent();
        }

        private void ArcPalette_OnCustomeColorChangedEvent(object sender, ColorChangeEventArgs e) {
            CustomeColorChanged?.Invoke(this, e);
        }
    }
}
