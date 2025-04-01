using System;
using System.Threading;
using Microsoft.UI.Xaml.Controls;
using VirtualPaper.Common;
using VirtualPaper.Common.Utils.Bridge.Base;
using VirtualPaper.UIComponent.Models;
using VirtualPaper.UIComponent.ViewModels;

namespace VirtualPaper.UIComponent.Utils {
    public class BasicComponentUtil(
        LoadingViewModel loadingViewModel = null, GlobalMsgViewModel globalMsgViewModel = null) : INoifyBridge {
        public void ShowExp(Exception ex) {
            ShowMsg(false, ex.Message, InfoBarType.Error);
        }

        public void ShowWarn(string msg) {
            ShowMsg(true, msg, InfoBarType.Warning);
        }

        public void ShowCanceled() {
            ShowMsg(true, Constants.I18n.InfobarMsg_Cancel, InfoBarType.Informational);
        }

        public void ShowMsg(
           bool isNeedLocalizer,
           string msg,
           InfoBarType infoBarType,
           string extraMsg = "",
           string key = "",
           bool isAllowDuplication = true) {
            InfoBarSeverity severity = infoBarType switch {
                InfoBarType.Informational => InfoBarSeverity.Informational,
                InfoBarType.Warning => InfoBarSeverity.Warning,
                InfoBarType.Error => InfoBarSeverity.Error,
                InfoBarType.Success => InfoBarSeverity.Success,
                _ => InfoBarSeverity.Informational
            };

            var globalMsgInfo = new GlobalMsgInfo(key, isNeedLocalizer, msg, extraMsg, severity);
            _globalMsgViewModel.AddMsg(globalMsgInfo, isAllowDuplication);
        }

        public void CloseAndRemoveMsg(string key) {
            _globalMsgViewModel.CloseAndRemoveMsg(key);
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

        public readonly LoadingViewModel _loadingViewModel = loadingViewModel ?? new();
        public readonly GlobalMsgViewModel _globalMsgViewModel = globalMsgViewModel ?? new();
    }
}
