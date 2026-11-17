using VirtualPaper.InstallerTest;

namespace VirtualPaper.Core.Test.T_Common;

[TestClass]
public class InstallerLanguageTests {
    [TestMethod]
    [DataRow("&Next >", "next")]
    [DataRow("下一步(&N) >", "next")]
    [DataRow("&Suivant >", "next")]
    [DataRow("&Installer", "install")]
    [DataRow("&Terminer", "finish")]
    [DataRow("Unknown", null)]
    public void WizardButtonsResolveWithoutUsingTheOsLanguage(string label, string? action) =>
        Assert.AreEqual(action, InstallerWizardText.InstallAction(label));

    [TestMethod]
    [DataRow("I &accept the agreement")]
    [DataRow("我同意此协议(&A)")]
    [DataRow("Je comprends et j'&accepte les termes du contrat de licence")]
    public void AcceptanceLabelsResolve(string label) => Assert.IsTrue(InstallerWizardText.IsLicenseAcceptance(label));

    [TestMethod]
    [DataRow("Supprimer les paramètres utilisateur ?", "keep-user-data")]
    [DataRow("Voulez-vous vraiment désinstaller complètement Virtual Paper ?", "confirm-uninstall")]
    [DataRow("Virtual Paper a été correctement désinstallé de cet ordinateur.", "acknowledge-uninstall")]
    [DataRow("An unrelated warning", null)]
    public void FrenchUninstallRetainsUserDataAndClassifiesOnlyKnownPrompts(string text, string? action) =>
        Assert.AreEqual(action, InstallerWizardText.DialogAction(text));

    [TestMethod]
    [DataRow("english", true)]
    [DataRow("chinese", true)]
    [DataRow("french", true)]
    [DataRow("unsupported", false)]
    public void ProbeAcceptsOnlyImplementedWizardLanguages(string language, bool accepted) =>
        Assert.AreEqual(accepted, InstallerOptions.TryParse(["--help", "--language", language], out _, out _));
}
