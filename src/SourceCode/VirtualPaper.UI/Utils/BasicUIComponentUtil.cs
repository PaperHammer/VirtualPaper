using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml.Controls;
using System;
using System.Threading;
using VirtualPaper.UI.ViewModels;

namespace VirtualPaper.UI.Utils {
    internal class BasicUIComponentUtil {
        static BasicUIComponentUtil() {
            _mainWindowViewModel = App.Services.GetRequiredService<MainWindowViewModel>();
        }

        public static void ShowExp(Exception ex) {
            ShowMessge(false, ex.Message, InfoBarSeverity.Error);
        }

        public static void ShowCanceled() {
            ShowMsg(true, "InfobarMsg_Cancel", InfoBarSeverity.Informational);
        }

        public static void ShowMsg(bool isNeedLocalizer, string msg, InfoBarSeverity infoBarSeverity) {
            ShowMessge(isNeedLocalizer, msg, infoBarSeverity);
        }

        private static void ShowMessge(
            bool isNeedLocalizer,
            string msg,
            InfoBarSeverity infoBarSeverity) {
            _mainWindowViewModel.ShowMessge(
                isNeedLocalizer,
                msg,
                infoBarSeverity);
        }

        public static void Loading(
            bool cancelEnable,
            bool progressbarEnable,
            CancellationTokenSource[] cts) {
            _mainWindowViewModel.Loading(
                cancelEnable,
                progressbarEnable,
                cts);
        }

        public static void Loaded(CancellationTokenSource[] cts) {
            _mainWindowViewModel.Loaded(cts);
        }

        public static void UpdateProgressbarValue(int curValue, int toltalValue) {
            _mainWindowViewModel.UpdateProgressbarValue(curValue, toltalValue);
        }

        private readonly static MainWindowViewModel _mainWindowViewModel;
    }
}
