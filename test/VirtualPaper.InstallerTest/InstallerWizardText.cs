using System.Text.RegularExpressions;

namespace VirtualPaper.InstallerTest;

// Labels from the pinned Inno Setup English, Simplified Chinese and French language files.
internal static class InstallerWizardText {
    internal static string Normalize(string text) => Regex.Replace(text.Replace("&", ""), @"\([A-Za-z]\)", "")
        .Trim().Trim('<', '>').Trim().ToLowerInvariant();

    internal static bool IsLicenseAcceptance(string text) => Normalize(text) is "i accept the agreement" or "我同意此协议"
        or "je comprends et j'accepte les termes du contrat de licence";

    internal static string? InstallAction(string text) => Normalize(text) switch {
        "finish" or "完成" or "terminer" => "finish",
        "install" or "安装" or "installer" => "install",
        "next" or "下一步" or "suivant" => "next",
        _ => null
    };

    internal static string? DialogAction(string text) {
        if (text.Contains("delete user settings", StringComparison.OrdinalIgnoreCase) || text.Contains("删除用户配置信息", StringComparison.Ordinal)
            || text.Contains("delete data folder", StringComparison.OrdinalIgnoreCase) || text.Contains("删除数据文件夹", StringComparison.Ordinal)
            || text.Contains("Supprimer les paramètres utilisateur", StringComparison.OrdinalIgnoreCase)) return "keep-user-data";
        if (text.Contains("completely remove", StringComparison.OrdinalIgnoreCase) || text.Contains("完全移除", StringComparison.Ordinal)
            || text.Contains("désinstaller", StringComparison.OrdinalIgnoreCase) && text.Contains("complètement", StringComparison.OrdinalIgnoreCase)) return "confirm-uninstall";
        if (text.Contains("successfully removed", StringComparison.OrdinalIgnoreCase) || text.Contains("已顺利从您的电脑中移除", StringComparison.Ordinal)
            || text.Contains("a été correctement désinstallé", StringComparison.OrdinalIgnoreCase)) return "acknowledge-uninstall";
        return null;
    }
}
