using System.Text;
using Workloads.Creation.WebBackdrop.Models;

namespace WebBackdrop.Test.T_WebBackdrop;

public partial class WebEditorViewModelTests {
    [TestMethod]
    [DataRow("UTF-8", false)]
    [DataRow("UTF-8 BOM", false)]
    [DataRow("UTF-16 LE", false)]
    [DataRow("UTF-16 BE", false)]
    [DataRow("UTF-8", true)]
    [DataRow("UTF-8 BOM", true)]
    [DataRow("UTF-16 LE", true)]
    [DataRow("UTF-16 BE", true)]
    public async Task SaveAndSaveAs_PreserveExactEncodingBytesAndSourceState(string encodingName, bool saveAs) {
        string source = Path.Combine(_testRoot, "source.txt");
        File.WriteAllText(source, "original");
        await _viewModel.OpenFileAsync(source);
        WebEditorFile file = _viewModel.ActiveFile!;
        const string text = "你好 Привет café\r\nsecond line\n";
        file.Content = text;
        file.SetEncoding(encodingName);
        file.SetSavedState(false);
        string target = saveAs ? Path.Combine(_testRoot, "new directory", "copy.txt") : source;
        Directory.CreateDirectory(Path.GetDirectoryName(target)!);
        bool saved = saveAs ? await _viewModel.SaveFileAsAsync(file, target) : await _viewModel.SaveFileAsync(file);
        Assert.IsTrue(saved);
        Encoding encoding = encodingName switch {
            "UTF-8 BOM" => new UTF8Encoding(true),
            "UTF-16 LE" => Encoding.Unicode,
            "UTF-16 BE" => Encoding.BigEndianUnicode,
            _ => new UTF8Encoding(false),
        };
        CollectionAssert.AreEqual(encoding.GetPreamble().Concat(encoding.GetBytes(text)).ToArray(), File.ReadAllBytes(target));
        Assert.AreEqual(!saveAs, file.IsSaved);
        Assert.AreEqual(source, file.FilePath);
        Assert.AreSame(file, _viewModel.ActiveFile);
        Assert.AreEqual(text, file.Content);
        if (saveAs) Assert.AreEqual("original", File.ReadAllText(source));
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public async Task LockedSaveTarget_PreservesBothFilesAndUnsavedContent_AndCanRetry(bool saveAs) {
        string source = Path.Combine(_testRoot, "source.txt");
        File.WriteAllText(source, "original");
        await _viewModel.OpenFileAsync(source);
        var file = _viewModel.ActiveFile!;
        file.Content = "edited in memory";
        file.SetSavedState(false);
        string target = saveAs ? Path.Combine(_testRoot, "copy.txt") : source;
        if (saveAs) File.WriteAllText(target, "existing copy");
        byte[] originalTarget = File.ReadAllBytes(target);
        using (var locked = File.Open(target, FileMode.Open, FileAccess.Read, FileShare.Read)) {
            Assert.IsFalse(saveAs ? await _viewModel.SaveFileAsAsync(file, target) : await _viewModel.SaveFileAsync(file));
        }
        Assert.AreEqual("original", File.ReadAllText(source));
        CollectionAssert.AreEqual(originalTarget, File.ReadAllBytes(target));
        Assert.AreEqual("edited in memory", file.Content);
        Assert.IsFalse(file.IsSaved);
        Assert.IsFalse(_viewModel.IsAllSaved);
        Assert.IsTrue(saveAs ? await _viewModel.SaveFileAsAsync(file, target) : await _viewModel.SaveFileAsync(file));
        Assert.AreEqual("edited in memory", File.ReadAllText(target));
        Assert.AreEqual(!saveAs, file.IsSaved);
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public async Task SaveAs_RejectsBinaryOrLoadFailedFiles_WithoutChangingTheExistingDestination(bool binary) {
        string source = Path.Combine(_testRoot, binary ? "image.png" : "script.js");
        File.WriteAllText(source, "source bytes");
        string target = Path.Combine(_testRoot, "copy.txt");
        File.WriteAllText(target, "existing destination");
        await _viewModel.OpenFileAsync(source);
        var file = _viewModel.ActiveFile!;
        if (!binary) { file.Content = "partial read"; file.SetLoadFailed(); }
        Assert.IsFalse(await _viewModel.SaveFileAsAsync(file, target));
        Assert.AreEqual("source bytes", File.ReadAllText(source));
        Assert.AreEqual("existing destination", File.ReadAllText(target));
        Assert.AreSame(file, _viewModel.ActiveFile);
        if (!binary) Assert.IsTrue(file.IsLoadFailed);
    }
}
