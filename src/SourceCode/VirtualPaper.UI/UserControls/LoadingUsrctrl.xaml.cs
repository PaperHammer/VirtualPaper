using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System;
using System.Threading;
using WinUI3Localizer;

// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.

namespace VirtualPaper.UI.UserControls
{
    public sealed partial class LoadingUsrctrl : UserControl
    {
        public bool CancelEnable
        {
            get { return (bool)GetValue(CancelEnableProperty); }
            set { SetValue(CancelEnableProperty, value); }
        }
        public static readonly DependencyProperty CancelEnableProperty =
            DependencyProperty.Register("CancelEnable", typeof(bool), typeof(LoadingUsrctrl), new PropertyMetadata(true));

        public bool ProgressbarEnable
        {
            get { return (bool)GetValue(ProgressbarEnableProperty); }
            set { SetValue(ProgressbarEnableProperty, value); }
        }
        public static readonly DependencyProperty ProgressbarEnableProperty =
            DependencyProperty.Register("ProgressbarEnable", typeof(bool), typeof(LoadingUsrctrl), new PropertyMetadata(true));

        public CancellationTokenSource[] CtsTokens
        {
            get { return (CancellationTokenSource[])GetValue(CtsTokensProperty); }
            set { SetValue(CtsTokensProperty, value); }
        }
        public static readonly DependencyProperty CtsTokensProperty =
            DependencyProperty.Register("CtsTokens", typeof(CancellationTokenSource[]), typeof(LoadingUsrctrl), new PropertyMetadata(Array.Empty<CancellationTokenSource>()));

        public int TotalValue
        {
            get { return (int)GetValue(ImportTotalCntProperty); }
            set { SetValue(ImportTotalCntProperty, value); }
        }
        public static readonly DependencyProperty ImportTotalCntProperty =
            DependencyProperty.Register("TotalValue", typeof(int), typeof(LoadingUsrctrl), new PropertyMetadata(0, InitValue));

        public int CurValue
        {
            get { return (int)GetValue(ImportValueProperty); }
            set { SetValue(ImportValueProperty, value); }
        }
        public static readonly DependencyProperty ImportValueProperty =
            DependencyProperty.Register("CurValue", typeof(int), typeof(LoadingUsrctrl), new PropertyMetadata(0, UpdateValue));

        public string ValueString
        {
            get { return (string)GetValue(ValueStringProperty); }
            set { SetValue(ValueStringProperty, value); }
        }
        public static readonly DependencyProperty ValueStringProperty =
            DependencyProperty.Register("ValueString", typeof(string), typeof(LoadingUsrctrl), new PropertyMetadata(string.Empty));

        public LoadingUsrctrl()
        {
            this.InitializeComponent();
            InitText();
        }

        private void BtnCancel_Click(object sender, RoutedEventArgs e)
        {
            foreach (var token in CtsTokens)
            {
                token?.Cancel();
            }
        }

        private void InitText()
        {
            _localizer = Localizer.Get();

            Text_Cancel = _localizer.GetLocalizedString("WpSettings_Text_Cancel");
            Text_Loading = _localizer.GetLocalizedString("WpSettings_Text_Loading");
        }

        private static void InitValue(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            LoadingUsrctrl instance = (LoadingUsrctrl)d;
            instance.ValueString = $"0 / {(int)e.NewValue}";
        }

        private static void UpdateValue(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            LoadingUsrctrl instance = (LoadingUsrctrl)d;
            instance.ValueString = $"{(int)e.NewValue} / {instance.TotalValue}";
        }

        private ILocalizer _localizer;
        internal string Text_Loading;
        internal string Text_Cancel;
    }
}
