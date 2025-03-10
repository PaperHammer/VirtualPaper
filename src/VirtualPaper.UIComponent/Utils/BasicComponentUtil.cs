using System;
using System.Threading;
using Microsoft.UI.Xaml.Controls;
using VirtualPaper.Common;
using VirtualPaper.Common.Utils.Bridge.Base;
using VirtualPaper.UIComponent.ViewModels;

namespace VirtualPaper.UI.Utils {
    public class BasicComponentUtil : INoifyBridge {
        public BasicComponentUtil(LoadingViewModel loadingViewModel, GlobalMsgViewModel globalMsgViewModel) {
            _loadingViewModel = loadingViewModel;
            _globalMsgViewModel = globalMsgViewModel;
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
            _globalMsgViewModel.ShowMessge(isNeedLocalizer, msg, severity);
        }

        public void Loading(
            bool cancelEnable,
            bool progressbarEnable,
            CancellationTokenSource[] cts = null) {
            _loadingViewModel.Loading(
                cancelEnable,
                progressbarEnable,
                cts);
        }

        public void Loaded(CancellationTokenSource[] cts = null) {
            _loadingViewModel.Loaded(cts);
        }

        public void UpdateProgressbarValue(int curValue, int toltalValue) {
            _loadingViewModel.UpdateProgressbarValue(curValue, toltalValue);
        }

        private readonly LoadingViewModel _loadingViewModel;
        private readonly GlobalMsgViewModel _globalMsgViewModel;
    }
}
