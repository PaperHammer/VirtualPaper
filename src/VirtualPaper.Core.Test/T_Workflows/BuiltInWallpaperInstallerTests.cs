using VirtualPaper.Services;

namespace VirtualPaper.Core.Test.T_Workflows;

[TestClass]
public class BuiltInWallpaperInstallerTests {
    private string _root = null!;

    [TestInitialize]
    public void Initialize() => _root = Path.Combine(Path.GetTempPath(), $"VirtualPaper.MsixBuiltIns.{Guid.NewGuid():N}");

    [TestCleanup]
    public void Cleanup() {
        if (Directory.Exists(_root)) Directory.Delete(_root, true);
    }

    [TestMethod]
    public void InstallMissing_PreservesExistingFolderAndCopiesNewWallpaper() {
        string source = Path.Combine(_root, "package");
        string destination = Path.Combine(_root, "library");
        Directory.CreateDirectory(Path.Combine(source, "existing"));
        Directory.CreateDirectory(Path.Combine(source, "new", "media"));
        Directory.CreateDirectory(Path.Combine(destination, "existing"));
        File.WriteAllText(Path.Combine(source, "existing", "edited.txt"), "package original");
        File.WriteAllText(Path.Combine(source, "existing", "deleted.txt"), "must stay deleted");
        File.WriteAllText(Path.Combine(destination, "existing", "edited.txt"), "user edit");
        File.WriteAllText(Path.Combine(source, "new", "media", "content.txt"), "new wallpaper");

        BuiltInWallpaperInstaller.InstallMissing(source, destination);
        BuiltInWallpaperInstaller.InstallMissing(source, destination);

        Assert.AreEqual("user edit", File.ReadAllText(Path.Combine(destination, "existing", "edited.txt")));
        Assert.IsFalse(File.Exists(Path.Combine(destination, "existing", "deleted.txt")));
        Assert.AreEqual("new wallpaper", File.ReadAllText(Path.Combine(destination, "new", "media", "content.txt")));
        Assert.IsEmpty(Directory.GetDirectories(destination, ".msix-builtin-*"));
    }

    [TestMethod]
    public void InstallMissing_EmptyCollectionDoesNotRequireASourceDirectory() {
        BuiltInWallpaperInstaller.InstallMissing(Path.Combine(_root, "missing"), Path.Combine(_root, "library"));
        Assert.IsFalse(Directory.Exists(_root));
    }

    [TestMethod]
    public void InstallMissing_CopyFailureDoesNotPublishPartialFolderAndCanBeRetried() {
        string source = Path.Combine(_root, "package");
        string destination = Path.Combine(_root, "library");
        Directory.CreateDirectory(Path.Combine(source, "new", "media"));
        Directory.CreateDirectory(Path.Combine(destination, "user"));
        string userFile = Path.Combine(destination, "user", "content.txt");
        File.WriteAllText(userFile, "user wallpaper");
        File.WriteAllText(Path.Combine(source, "new", "metadata.json"), "metadata copied before media");
        string media = Path.Combine(source, "new", "media", "content.txt");
        File.WriteAllText(media, "new wallpaper");

        // A real sharing violation occurs after copying the root file into staging.
        using (File.Open(media, FileMode.Open, FileAccess.Read, FileShare.None)) {
            Assert.ThrowsExactly<IOException>(() => BuiltInWallpaperInstaller.InstallMissing(source, destination));
            Assert.IsFalse(Directory.Exists(Path.Combine(destination, "new")));
            Assert.IsEmpty(Directory.GetDirectories(destination, ".msix-builtin-*"));
            Assert.AreEqual("user wallpaper", File.ReadAllText(userFile));
        }

        BuiltInWallpaperInstaller.InstallMissing(source, destination);

        Assert.AreEqual("metadata copied before media", File.ReadAllText(Path.Combine(destination, "new", "metadata.json")));
        Assert.AreEqual("new wallpaper", File.ReadAllText(Path.Combine(destination, "new", "media", "content.txt")));
        Assert.AreEqual("new wallpaper", File.ReadAllText(media));
        Assert.AreEqual("user wallpaper", File.ReadAllText(userFile));
        Assert.IsEmpty(Directory.GetDirectories(destination, ".msix-builtin-*"));
    }

    [TestMethod]
    public void InstallMissing_DestinationFileCollisionPreservesUserFileAndCleansStaging() {
        string source = Path.Combine(_root, "package");
        string destination = Path.Combine(_root, "library");
        Directory.CreateDirectory(Path.Combine(source, "new"));
        Directory.CreateDirectory(destination);
        File.WriteAllText(Path.Combine(source, "new", "content.txt"), "new wallpaper");
        string collision = Path.Combine(destination, "new");
        File.WriteAllText(collision, "user file");

        Assert.ThrowsExactly<IOException>(() => BuiltInWallpaperInstaller.InstallMissing(source, destination));

        Assert.AreEqual("user file", File.ReadAllText(collision));
        Assert.IsFalse(Directory.Exists(collision));
        Assert.IsEmpty(Directory.GetDirectories(destination, ".msix-builtin-*"));
    }

    [TestMethod]
    public void InstallMissing_LaterCollectionAddsOnlyNewFolders() {
        string source = Path.Combine(_root, "package");
        string destination = Path.Combine(_root, "library");
        Directory.CreateDirectory(Path.Combine(source, "original"));
        File.WriteAllText(Path.Combine(source, "original", "content.txt"), "original wallpaper");
        BuiltInWallpaperInstaller.InstallMissing(source, destination);
        File.Delete(Path.Combine(destination, "original", "content.txt"));
        File.WriteAllText(Path.Combine(destination, "original", "user.txt"), "user addition");
        Directory.CreateDirectory(Path.Combine(source, "added"));
        File.WriteAllText(Path.Combine(source, "added", "content.txt"), "added in a later release");

        BuiltInWallpaperInstaller.InstallMissing(source, destination);

        Assert.IsFalse(File.Exists(Path.Combine(destination, "original", "content.txt")));
        Assert.AreEqual("user addition", File.ReadAllText(Path.Combine(destination, "original", "user.txt")));
        Assert.AreEqual("added in a later release", File.ReadAllText(Path.Combine(destination, "added", "content.txt")));
        Assert.IsEmpty(Directory.GetDirectories(destination, ".msix-builtin-*"));
    }
}
