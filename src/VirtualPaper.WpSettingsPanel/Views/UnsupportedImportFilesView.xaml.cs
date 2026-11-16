using System;
using System.Collections.Generic;
using System.IO;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using VirtualPaper.Common;
using VirtualPaper.Common.Logging;
using VirtualPaper.Common.Utils.Files;
using VirtualPaper.UIComponent.Utils;

namespace VirtualPaper.WpSettingsPanel.Views;

public sealed partial class UnsupportedImportFilesView : UserControl {
    public IReadOnlyList<string> Files { get; }
    public double ListHeight => Math.Clamp(Files.Count * 64d, 64, 360);

    public UnsupportedImportFilesView(IReadOnlyList<string> files) {
        Files = files;
        InitializeComponent();
    }

    private async void RevealFile_Click(object sender, RoutedEventArgs args) {
        if (sender is not HyperlinkButton { Tag: string path } link) return;
        link.IsEnabled = false;
        try {
            if (!File.Exists(path)) {
                GlobalMessageUtil.ShowError(Constants.I18n.Text_File_Not_Available, isNeedLocalizer: true);
                return;
            }
            await FileUtil.OpenFolderAsync(path);
        }
        catch (Exception ex) {
            ArcLog.GetLogger<UnsupportedImportFilesView>().Error(ex);
            GlobalMessageUtil.ShowException(ex);
        }
        finally { link.IsEnabled = true; }
    }
}
