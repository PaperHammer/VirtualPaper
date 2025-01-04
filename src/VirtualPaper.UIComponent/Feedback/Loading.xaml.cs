using System;
using System.Threading;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.

namespace VirtualPaper.UIComponent.Feedback {
    public sealed partial class Loading : UserControl {
        public bool CancelEnable {
            get { return (bool)GetValue(CancelEnableProperty); }
            set { SetValue(CancelEnableProperty, value); }
        }
        public static readonly DependencyProperty CancelEnableProperty =
            DependencyProperty.Register("CancelEnable", typeof(bool), typeof(Loading), new PropertyMetadata(false));

        public bool ProgressbarEnable {
            get { return (bool)GetValue(ProgressbarEnableProperty); }
            set { SetValue(ProgressbarEnableProperty, value); }
        }
        public static readonly DependencyProperty ProgressbarEnableProperty =
            DependencyProperty.Register("ProgressbarEnable", typeof(bool), typeof(Loading), new PropertyMetadata(false));

        public CancellationTokenSource[] CtsTokens {
            get { return (CancellationTokenSource[])GetValue(CtsTokensProperty); }
            set { SetValue(CtsTokensProperty, value); }
        }
        public static readonly DependencyProperty CtsTokensProperty =
            DependencyProperty.Register("CtsTokens", typeof(CancellationTokenSource[]), typeof(Loading), new PropertyMetadata(Array.Empty<CancellationTokenSource>()));

        public int TotalValue {
            get { return (int)GetValue(ImportTotalCntProperty); }
            set { SetValue(ImportTotalCntProperty, value); }
        }
        public static readonly DependencyProperty ImportTotalCntProperty =
            DependencyProperty.Register("TotalValue", typeof(int), typeof(Loading), new PropertyMetadata(0, InitValue));

        public int CurValue {
            get { return (int)GetValue(ImportValueProperty); }
            set { SetValue(ImportValueProperty, value); }
        }
        public static readonly DependencyProperty ImportValueProperty =
            DependencyProperty.Register("CurValue", typeof(int), typeof(Loading), new PropertyMetadata(0, UpdateValue));

        public string TextLoading {
            get { return (string)GetValue(TextLoadingProperty); }
            set { SetValue(TextLoadingProperty, value); }
        }
        public static readonly DependencyProperty TextLoadingProperty =
            DependencyProperty.Register("TextLoading", typeof(string), typeof(Loading), new PropertyMetadata("Loading..."));

        public string TextCancel {
            get { return (string)GetValue(TextCancelProperty); }
            set { SetValue(TextCancelProperty, value); }
        }
        public static readonly DependencyProperty TextCancelProperty =
            DependencyProperty.Register("TextCancel", typeof(string), typeof(Loading), new PropertyMetadata("Cancel"));

        private string ValueString {
            get { return (string)GetValue(ValueStringProperty); }
            set { SetValue(ValueStringProperty, value); }
        }
        private static readonly DependencyProperty ValueStringProperty =
            DependencyProperty.Register("ValueString", typeof(string), typeof(Loading), new PropertyMetadata(string.Empty));

        public Loading() {
            this.InitializeComponent();
        }

        private void BtnCancel_Click(object sender, RoutedEventArgs e) {
            if (CtsTokens == null) return;

            foreach (var token in CtsTokens) {
                token?.Cancel();
            }
        }

        private static void InitValue(DependencyObject d, DependencyPropertyChangedEventArgs e) {
            Loading instance = (Loading)d;
            instance.ValueString = $"0 / {(int)e.NewValue}";
        }

        private static void UpdateValue(DependencyObject d, DependencyPropertyChangedEventArgs e) {
            Loading instance = (Loading)d;
            instance.ValueString = $"{(int)e.NewValue} / {instance.TotalValue}";
        }
    }
}
