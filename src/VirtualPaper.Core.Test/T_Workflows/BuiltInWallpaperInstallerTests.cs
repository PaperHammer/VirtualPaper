using VirtualPaper.Services;

namespace VirtualPaper.Core.Test.T_Workflows;

[TestClass]
public class BuiltInWallpaperInstallerTests {
    [TestMethod]
    public void InstallMissing_PreservesExistingFolderAndCopiesNewWallpaper() {
        string root = Path.Combine(Path.GetTempPath(), $"VirtualPaper.MsixBuiltIns.{Guid.NewGuid():N}");
        try {
            string source = Path.Combine(root, "package");
            string destination = Path.Combine(root, "library");
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
        finally { if (Directory.Exists(root)) Directory.Delete(root, true); }
    }

    [TestMethod]
    public void InstallMissing_EmptyCollectionDoesNotRequireASourceDirectory() {
        string root = Path.Combine(Path.GetTempPath(), $"VirtualPaper.MsixBuiltIns.{Guid.NewGuid():N}");
        BuiltInWallpaperInstaller.InstallMissing(Path.Combine(root, "missing"), Path.Combine(root, "library"));
        Assert.IsFalse(Directory.Exists(root));
    }
}
