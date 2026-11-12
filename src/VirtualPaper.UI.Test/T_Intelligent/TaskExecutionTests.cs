using System.Collections.Concurrent;
using System.Drawing;
using System.Drawing.Imaging;
using Microsoft.Extensions.DependencyInjection;
using VirtualPaper.Common;
using VirtualPaper.Common.Utils.DI;
using VirtualPaper.IntelligentPanel.Models;
using VirtualPaper.IntelligentPanel.ViewModels;
using VirtualPaper.ML.StyleTransfer.Interfaces;
using VirtualPaper.ML.SuperResolution.Interfaces;

namespace VirtualPaper.UI.Test.T_Intelligent;

[TestClass]
[DoNotParallelize]
public class TaskExecutionTests {
    [TestMethod]
    [DataRow(false, 2)]
    [DataRow(true, 3)]
    public async Task Queue_RespectsConcurrencyLimit_AndPublishesReadableResults(bool style, int limit) {
        await using var fixture = new Fixture(style, hold: true);
        var items = Enumerable.Range(0, limit + 2).Select(_ => fixture.Add()).ToArray();
        await WaitAsync(() => fixture.Engine.Active == limit);
        Assert.IsTrue(items.Skip(limit).All(item => item.IsPending));
        fixture.Engine.Release();
        await Task.WhenAll(items.Select(item => item.Execution)).WaitAsync(TimeSpan.FromSeconds(8));
        Assert.AreEqual(limit, fixture.Engine.MaximumActive);
        Assert.AreEqual(items.Length, fixture.Engine.Runs);
        foreach (var item in items) {
            Assert.IsTrue(item.IsCompleted);
            Assert.IsTrue(item.IsTemporaryResult);
            string path = Fixture.Result(item)!;
            Assert.IsTrue(File.Exists(path));
            using var output = new Bitmap(path);
            Assert.AreEqual(style ? 2 : 4, output.Width);
            Assert.AreEqual(style ? 2 : 4, output.Height);
            CollectionAssert.AreEqual(fixture.InputBytes, File.ReadAllBytes(Fixture.Source(item)));
            if (style) await WaitAsync(() => !File.Exists(Path.Combine(Path.GetDirectoryName(path)!, "styled.png")));
        }
    }

    [TestMethod]
    [DataRow(false, 2)]
    [DataRow(true, 3)]
    public async Task RemovingQueuedTask_DoesNotRunItOrLeakAPermit(bool style, int limit) {
        await using var fixture = new Fixture(style, hold: true);
        var items = Enumerable.Range(0, limit + 1).Select(_ => fixture.Add()).ToArray();
        await WaitAsync(() => fixture.Engine.Active == limit);
        var queued = items[^1];
        fixture.Remove(queued);
        await queued.Execution.WaitAsync(TimeSpan.FromSeconds(5));
        await WaitAsync(() => !fixture.Contains(queued));
        Assert.AreEqual(TaskStatus.Canceled, Fixture.Status(queued));
        Assert.IsFalse(fixture.Engine.Directories.ContainsKey(Fixture.Source(queued)));
        var next = fixture.Add();
        fixture.Engine.Release();
        await Task.WhenAll(items.Append(next).Select(item => item.Execution)).WaitAsync(TimeSpan.FromSeconds(8));
        Assert.AreEqual(limit + 1, fixture.Engine.Runs);
        Assert.IsTrue(next.IsCompleted);
        Assert.IsTrue(fixture.Engine.MaximumActive <= limit);
    }

    [TestMethod]
    [DataRow(false, 2)]
    [DataRow(true, 3)]
    public async Task RemovingRunningTask_CleansItsFilesAndLetsQueuedWorkStart(bool style, int limit) {
        await using var fixture = new Fixture(style, hold: true);
        var items = Enumerable.Range(0, limit + 1).Select(_ => fixture.Add()).ToArray();
        await WaitAsync(() => fixture.Engine.Active == limit);
        string directory = fixture.Engine.Directories[Fixture.Source(items[0])];
        fixture.Remove(items[0]);
        await items[0].Execution.WaitAsync(TimeSpan.FromSeconds(5));
        await WaitAsync(() => !fixture.Contains(items[0]) && fixture.Engine.Runs == limit + 1);
        Assert.AreEqual(TaskStatus.Canceled, Fixture.Status(items[0]));
        Assert.IsFalse(Directory.Exists(directory));
        fixture.Engine.Release();
        await Task.WhenAll(items.Select(item => item.Execution)).WaitAsync(TimeSpan.FromSeconds(8));
        Assert.IsTrue(items[^1].IsCompleted);
        Assert.IsTrue(fixture.Engine.MaximumActive <= limit);
    }

    [TestMethod]
    [DataRow(false, "load")]
    [DataRow(false, "upscale")]
    [DataRow(true, "load")]
    [DataRow(true, "style")]
    [DataRow(true, "upscale")]
    public async Task ModelFailure_CleansPartialFiles_AndNextTaskStillCompletes(bool style, string stage) {
        await using var fixture = new Fixture(style);
        fixture.Engine.FailureStage = stage;
        var failed = fixture.Add();
        await failed.Execution.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.AreEqual(TaskStatus.Faulted, Fixture.Status(failed));
        Assert.IsFalse(failed.IsTemporaryResult);
        Assert.IsEmpty(Directory.GetDirectories(Constants.CommonPaths.TempDir), "Failed model work must not leave its partial outputs behind.");
        var next = fixture.Add();
        await next.Execution.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.IsTrue(next.IsCompleted, "A failed job must return its concurrency permit.");
        Assert.IsTrue(File.Exists(Fixture.Result(next)));
        CollectionAssert.AreEqual(fixture.InputBytes, File.ReadAllBytes(Fixture.Source(failed)));
    }

    [TestMethod]
    [DataRow(false, 2)]
    [DataRow(true, 3)]
    public async Task Dispose_CancelsRunningAndQueuedWorkAndCleansTemporaryDirectories(bool style, int limit) {
        await using var fixture = new Fixture(style, hold: true);
        var items = Enumerable.Range(0, limit + 2).Select(_ => fixture.Add()).ToArray();
        await WaitAsync(() => fixture.Engine.Active == limit);
        fixture.DisposeViewModels();
        await Task.WhenAll(items.Select(item => item.Execution)).WaitAsync(TimeSpan.FromSeconds(5));
        Assert.IsTrue(items.All(item => Fixture.Status(item) == TaskStatus.Canceled));
        Assert.IsEmpty(Directory.GetDirectories(Constants.CommonPaths.TempDir));
        Assert.AreEqual(0, fixture.Engine.Active);
    }

    private static async Task WaitAsync(Func<bool> condition) {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        while (!condition()) await Task.Delay(10, timeout.Token);
    }

    private sealed class Fixture : IAsyncDisposable {
        private readonly bool _style;
        private readonly string _root = Directory.CreateDirectory(Path.Combine(Path.GetTempPath(),
            "VirtualPaper.TaskTests-" + Guid.NewGuid().ToString("N"))).FullName;
        private readonly string _oldRoot = Constants.CommonPaths.TestRootDir;
        private readonly bool _oldMode = Constants.IsTestMode;
        private readonly IServiceProvider? _previous = PreviousServices();
        private readonly ServiceProvider _services;
        private readonly StyleTranferViewModel _styles = new();
        private readonly SuperResolutionViewModel _super = new();
        private readonly List<IntelligentTaskItem> _items = [];
        private int _next;
        public Engine Engine { get; }
        public byte[] InputBytes { get; }

        public Fixture(bool style, bool hold = false) {
            _style = style;
            Constants.IsTestMode = true;
            Constants.CommonPaths.TestRootDir = Path.Combine(_root, "profile");
            string input = Path.Combine(_root, "source.png");
            using (var bitmap = new Bitmap(2, 2)) {
                bitmap.SetPixel(0, 0, Color.Red); bitmap.SetPixel(1, 1, Color.Blue);
                bitmap.Save(input, ImageFormat.Png);
            }
            InputBytes = File.ReadAllBytes(input);
            Engine = new Engine(hold);
            _services = new ServiceCollection().AddSingleton<IStyleTransfer>(Engine)
                .AddSingleton<ISuperResolution>(Engine).BuildServiceProvider();
            AppServiceLocator.Services = _services;
        }

        public IntelligentTaskItem Add() {
            string path = Path.Combine(_root, $"source-{_next++}.png");
            File.WriteAllBytes(path, InputBytes);
            bool added = _style
                ? _styles.AddTask(new StyleTransferData(path, "100 B", ".png", 2, 2, path, "Test", "100 B", ".png"))
                : _super.AddTask(new SuperResolutionData(path, "100 B", ".png", 2, 2, EnhanceMode.SuperResolution, 2, 4, 4));
            Assert.IsTrue(added);
            IntelligentTaskItem item = _style ? _styles.Tasks[^1] : _super.Tasks[^1];
            _items.Add(item); return item;
        }
        public bool Contains(IntelligentTaskItem item) => _style ? _styles.Tasks.Contains((StyleTransferTaskItem)item) : _super.Tasks.Contains((SuperResolutionTaskItem)item);
        public void Remove(IntelligentTaskItem item) {
            if (item is StyleTransferTaskItem s) s.RemoveCommand!.Execute(null);
            else ((SuperResolutionTaskItem)item).RemoveCommand!.Execute(null);
        }
        public void DisposeViewModels() { _styles.Dispose(); _super.Dispose(); }
        public static string Source(IntelligentTaskItem item) => item is StyleTransferTaskItem s ? s.Data.SourceFilePath : ((SuperResolutionTaskItem)item).Data.SourceFilePath;
        public static string? Result(IntelligentTaskItem item) => item is StyleTransferTaskItem s ? s.Data.ResultFilePath : ((SuperResolutionTaskItem)item).Data.ResultFilePath;
        public static TaskStatus Status(IntelligentTaskItem item) => item is StyleTransferTaskItem s ? s.Status : ((SuperResolutionTaskItem)item).Status;

        public async ValueTask DisposeAsync() {
            Engine.Release(); DisposeViewModels();
            try { await Task.WhenAll(_items.Select(item => item.Execution)).WaitAsync(TimeSpan.FromSeconds(5)); }
            finally {
                _services.Dispose(); Engine.Dispose();
                AppServiceLocator.Services = _previous!;
                Constants.CommonPaths.TestRootDir = _oldRoot; Constants.IsTestMode = _oldMode;
                Directory.Delete(_root, recursive: true);
            }
        }

        private static IServiceProvider? PreviousServices() {
            try { return AppServiceLocator.Services; }
            catch (InvalidOperationException) { return null; }
        }
    }

    private sealed class Engine(bool hold) : IStyleTransfer, ISuperResolution {
        private readonly ManualResetEventSlim _release = new(!hold);
        private int _active, _maximum, _runs, _failed;
        private bool _disposed;
        public string ModelPath => "controlled-test-model";
        public int Active => Volatile.Read(ref _active);
        public int MaximumActive => Volatile.Read(ref _maximum);
        public int Runs => Volatile.Read(ref _runs);
        public string? FailureStage { get; set; }
        public ConcurrentDictionary<string, string> Directories { get; } = new(StringComparer.OrdinalIgnoreCase);
        public void Release() => _release.Set();
        public void LoadModel(string? path = null) => FailIfRequested("load", null);
        public string RunAndSave(string input, string style, string output, float alpha = 1, int contentSize = 512, int styleSize = 512, CancellationToken ct = default) {
            Directories[input] = Path.GetDirectoryName(output)!;
            FailIfRequested("style", output);
            ct.ThrowIfCancellationRequested(); File.Copy(input, output, true); return output;
        }
        public string RunAndSave(string input, string output, uint width, uint height, CancellationToken ct = default) {
            Directories[input] = Path.GetDirectoryName(output)!;
            int active = Interlocked.Increment(ref _active);
            int previous;
            do { previous = _maximum; if (previous >= active) break; }
            while (Interlocked.CompareExchange(ref _maximum, active, previous) != previous);
            Interlocked.Increment(ref _runs);
            try {
                FailIfRequested("upscale", output);
                _release.Wait(ct); ct.ThrowIfCancellationRequested();
                using var source = new Bitmap(input);
                using var scaled = new Bitmap(source, checked((int)width), checked((int)height));
                scaled.Save(output, ImageFormat.Png); return output;
            }
            finally { Interlocked.Decrement(ref _active); }
        }
        private void FailIfRequested(string stage, string? output) {
            if (FailureStage == stage && Interlocked.Exchange(ref _failed, 1) == 0) {
                if (output != null) File.WriteAllText(output, "partial model output");
                throw new IOException("Controlled model failure at " + stage);
            }
        }
        public void Dispose() { if (_disposed) return; _disposed = true; _release.Dispose(); }
    }
}
