using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Controls;
using VirtualPaper.Common;
using VirtualPaper.UIComponent.Utils;

namespace VirtualPaper.IntelligentPanel.Services {
    public sealed class IntelligentTaskDialogs : IIntelligentTaskDialogs {
        public async Task<TaskCloseDecision> ConfirmCloseAsync(int activeCount, int temporaryCount, bool allowDontRemind) {
            var lines = new List<string>();
            if (activeCount > 0) lines.Add(string.Format(LanguageUtil.GetI18n(Constants.I18n.Intelligent_Close_Active), activeCount));
            if (temporaryCount > 0) lines.Add(string.Format(LanguageUtil.GetI18n(Constants.I18n.Intelligent_Close_Temporary), temporaryCount));
            var content = new StackPanel { Spacing = 16 };
            content.Children.Add(new TextBlock { Text = string.Join(Environment.NewLine + Environment.NewLine, lines), TextWrapping = TextWrapping.Wrap });
            var dontRemind = new CheckBox {
                Content = new TextBlock {
                    Text = LanguageUtil.GetI18n(Constants.I18n.Intelligent_Close_DontRemind),
                    TextWrapping = TextWrapping.Wrap
                },
                HorizontalContentAlignment = HorizontalAlignment.Stretch,
                Visibility = allowDontRemind ? Visibility.Visible : Visibility.Collapsed
            };
            AutomationProperties.SetAutomationId(dontRemind, "Intelligent.Close.DontRemind");
            content.Children.Add(dontRemind);
            var dialog = GlobalDialogUtils.CreateDialog(content,
                LanguageUtil.GetI18n(Constants.I18n.Intelligent_Close_Title),
                LanguageUtil.GetI18n(Constants.I18n.Intelligent_Close_Return),
                LanguageUtil.GetI18n(Constants.I18n.Intelligent_Close_Confirm))!;
            dialog.DialogMaxWidth = 640;
            AutomationProperties.SetAutomationId(dialog, "Intelligent.Close.Dialog");
            bool close = await dialog.ShowAsync() == ContentDialogResult.Secondary;
            return new(close, close && allowDontRemind && dontRemind.IsChecked == true);
        }

        public async Task<bool> ConfirmRemoveAsync() {
            var dialog = GlobalDialogUtils.CreateDialog(
                LanguageUtil.GetI18n(Constants.I18n.Intelligent_Remove_Temporary),
                LanguageUtil.GetI18n(Constants.I18n.Intelligent_Remove_Title),
                LanguageUtil.GetI18n(Constants.I18n.Text_Cancel),
                LanguageUtil.GetI18n(Constants.I18n.Text_Delete))!;
            dialog.DialogMaxWidth = 520;
            AutomationProperties.SetAutomationId(dialog, "Intelligent.Remove.Dialog");
            return await dialog.ShowAsync() == ContentDialogResult.Secondary;
        }
    }
}
