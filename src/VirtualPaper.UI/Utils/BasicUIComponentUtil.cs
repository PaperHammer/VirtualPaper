using System;
using System.Threading;
using Microsoft.UI.Xaml.Controls;
using VirtualPaper.Common;
using VirtualPaper.Common.Utils.Bridge.Base;
using VirtualPaper.UI.ViewModels;

namespace VirtualPaper.UI.Utils
{
    internal class BasicUIComponentUtil : INoifyBridge {
        public BasicUIComponentUtil(MainWindowViewModel mainWindowViewModel) {
            _mainWindowViewModel = mainWindowViewModel;
        }

        public void ShowExp(Exception ex) {
            ShowMsg(false, ex.Message, InfoBarType.Error);
        }

        public void ShowCanceled() {
            ShowMsg(true, Constants.I18n.InfobarMsg_Cancel, InfoBarType.Informational);
        }

        public void ShowMsg(bool isNeedLocalizer, string msg, InfoBarType infoBarType) {
            InfoBarSeverity severity =
                infoBarType switch {
                    InfoBarType.Informational => InfoBarSeverity.Informational,
                    InfoBarType.Warning => InfoBarSeverity.Warning,
                    InfoBarType.Error => InfoBarSeverity.Error,
                    InfoBarType.Success => InfoBarSeverity.Success,
                    _ => InfoBarSeverity.Informational
                };
            _mainWindowViewModel.ShowMessge(isNeedLocalizer, msg, severity);
        }

        public void Loading(
            bool cancelEnable,
            bool progressbarEnable,
            CancellationTokenSource[] cts = null) {
            _mainWindowViewModel.Loading(
                cancelEnable,
                progressbarEnable,
                cts);
        }

        public void Loaded(CancellationTokenSource[] cts = null) {
            _mainWindowViewModel.Loaded(cts);
        }

        public void UpdateProgressbarValue(int curValue, int toltalValue) {
            _mainWindowViewModel.UpdateProgressbarValue(curValue, toltalValue);
        }

        private readonly MainWindowViewModel _mainWindowViewModel;
    }
}
