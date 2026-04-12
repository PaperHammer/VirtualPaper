using System;
using System.Numerics;
using System.Threading.Tasks;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Markup;
using VirtualPaper.UIComponent.Templates;

// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.

namespace VirtualPaper.UIComponent.Data {
    [ContentProperty(Name = "RootContent")]
    public sealed partial class Card : UserControl {
        public object RootContent {
            get => GetValue(RootContentProperty);
            set => SetValue(RootContentProperty, value);
        }
        public static readonly DependencyProperty RootContentProperty =
            DependencyProperty.Register(nameof(RootContent), typeof(object), typeof(ArcPageHost), new PropertyMetadata(null));

        public double Elevation {
            get { return (double)GetValue(ElevationProperty); }
            set { SetValue(ElevationProperty, value); }
        }
        public static readonly DependencyProperty ElevationProperty =
            DependencyProperty.Register(nameof(Elevation), typeof(double), typeof(Card), new PropertyMetadata(32.0, OnElevationChanged));

        internal Vector3 ElevationVector { get; private set; }

        public Card() {
            this.InitializeComponent();
            this.Loaded += Card_Loaded;
            this.CornerRadius = new CornerRadius(8);
            this.Padding = new Thickness(50);

            UpdateElevationVector();
        }

        private void Card_Loaded(object sender, RoutedEventArgs e) {
            if (PART_CardBorder != null) PART_CardBorder.Translation = new Vector3(0, 0, (float)Elevation);
        }

        private static void OnElevationChanged(DependencyObject d, DependencyPropertyChangedEventArgs e) {
            if (d is Card card) {
                card.UpdateElevationVector();
            }
        }

        private void UpdateElevationVector() {
            ElevationVector = new Vector3(0, 0, (float)Elevation);
            if (PART_CardBorder != null) {
                PART_CardBorder.Translation = ElevationVector;
            }
        }

    }

    public interface ICardComponent {
        string PreviousStepBtnText => string.Empty;
        string NextStepBtnText => string.Empty;
        bool IsNextEnable => false;
        bool BtnVisible => false;
        Func<object?, Task>? PreviousStepAction => null;
        Func<object?, Task>? NextStepAction => null;
        Action? CardUIStateChanged { get; set; }
    }
}
