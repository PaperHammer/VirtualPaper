using System;
using System.Threading.Tasks;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Controls;
using VirtualPaper.UIComponent.Dialogs;
using VirtualPaper.UIComponent.Utils;

namespace VirtualPaper.AppSettingsPanel.Views;

internal static class AiUseNoticeDialog {
    internal static async Task<bool> ShowAsync() {
        // Load the shipped notice before allowing acknowledgement. Reading it must not open a second modal.
        var fullNotice = await LegalDocumentDialog.CreateContentAsync("ai-processing-and-rights");
        var content = new StackPanel { Spacing = 16 };
        content.Children.Add(new TextBlock {
            Text = LanguageUtil.GetI18n("AiUseNotice_LocalProcessing"), TextWrapping = TextWrapping.Wrap
        });
        content.Children.Add(new TextBlock {
            Text = LanguageUtil.GetI18n("AiUseNotice_Rights"), TextWrapping = TextWrapping.Wrap
        });
        var readMore = new HyperlinkButton {
            Content = new TextBlock { Text = LanguageUtil.GetI18n("AiUseNotice_ReadMore"), TextWrapping = TextWrapping.Wrap },
            HorizontalAlignment = HorizontalAlignment.Left
        };
        AutomationProperties.SetAutomationId(readMore, "AiUseNotice.ReadMore");
        content.Children.Add(readMore);
        var owner = ArcWindowManager.MainWindow?.ContentHost.AppRoot
            ?? throw new InvalidOperationException("The main UI window is not available.");
        var dialog = new ArcContentDialog {
            Title = new TextBlock { Text = LanguageUtil.GetI18n("AiUseNotice_Title"), TextWrapping = TextWrapping.Wrap },
            Content = content, DialogWidth = 600,
            PrimaryButtonText = LanguageUtil.GetI18n("AiUseNotice_Continue"),
            CloseButtonText = LanguageUtil.GetI18n("Text_Cancel"),
            XamlRoot = owner.XamlRoot, RequestedTheme = owner.ActualTheme
        };
        AutomationProperties.SetAutomationId(dialog, "AiUseNotice.Dialog");
        readMore.Click += (_, _) => {
            content.Children.Add(fullNotice);
            readMore.Visibility = Visibility.Collapsed;
            dialog.DialogHeight = 680;
        };
        return await dialog.ShowAsync() == ContentDialogResult.Primary;
    }
}
