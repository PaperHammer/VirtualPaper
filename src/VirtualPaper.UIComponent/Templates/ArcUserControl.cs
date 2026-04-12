using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using VirtualPaper.UIComponent.Utils;

namespace VirtualPaper.UIComponent.Templates {
    public partial class ArcUserControl : UserControl {
        public FrameworkPayload? Payload {
            get { return (FrameworkPayload?)GetValue(PayloadProperty); }
            set { SetValue(PayloadProperty, value); }
        }
        public static readonly DependencyProperty PayloadProperty =
            DependencyProperty.Register(nameof(Payload), typeof(FrameworkPayload), typeof(ArcUserControl), new PropertyMetadata(null, OnPayloadChangedStatic));

        private static void OnPayloadChangedStatic(DependencyObject d, DependencyPropertyChangedEventArgs e) {
            if (d is ArcUserControl control) {
                control.OnPayloadChanged(e.NewValue as FrameworkPayload, e.OldValue as FrameworkPayload);
            }
        }

        protected virtual void OnPayloadChanged(FrameworkPayload? newPayload, FrameworkPayload? oldPayload) {
        }
    }
}
