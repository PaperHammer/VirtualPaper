using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.UI.Xaml.Media;
using VirtualPaper.Common;
using VirtualPaper.Common.Logging;
using VirtualPaper.Common.Runtime.PlayerWeb;
using VirtualPaper.Common.Utils.Files;
using VirtualPaper.Common.Utils.Storage;
using VirtualPaper.Common.Utils.Storage.Adapter;
using VirtualPaper.DataAssistor;
using VirtualPaper.Grpc.Client.Interfaces;
using VirtualPaper.ML.SuperResolution;
using VirtualPaper.ML.SuperResolution.Interfaces;
using VirtualPaper.Models.Cores.Interfaces;
using VirtualPaper.Models.Mvvm;
using VirtualPaper.UIComponent;
using VirtualPaper.UIComponent.Utils;
using VirtualPaper.WpSettingsPanel.Utils;
using Windows.Graphics.Imaging;
using Windows.Storage;

namespace VirtualPaper.WpSettingsPanel.ViewModels {
    public sealed record ScreenFitMonitor(string DeviceId, string Label, int Width, int Height);

    public sealed class ScreenFitViewModel : ObservableObject {
        private readonly IMonitorManagerClient _monitors;
        private readonly IWallpaperControlClient _wallpapers;
        private readonly IUserSettingsClient _settings;
        private readonly IStoragePicker _picker;
        private readonly Func<ISuperResolution> _createModel;
        public event Action<IWpBasicData>? Imported;
        public IReadOnlyList<ScreenFitMonitor> Monitors { get; }
        public ScreenFitViewModel(IMonitorManagerClient monitors, IWallpaperControlClient wallpapers,
            IUserSettingsClient settings, IStoragePicker picker, Func<ISuperResolution>? createModel = null) {
            _monitors = monitors; _wallpapers = wallpapers; _settings = settings; _picker = picker;
            // Own the inference session so disposing this operation cannot interrupt the existing task queue.
            _createModel = createModel ?? (() => new Realesrgan());
            Monitors = monitors.Monitors.Where(m => m.Bounds.Width > 0 && m.Bounds.Height > 0)
                .Select(m => new ScreenFitMonitor(m.DeviceId, $"{m.Content} · {m.Bounds.Width} × {m.Bounds.Height}", m.Bounds.Width, m.Bounds.Height)).ToList();
            _selectedMonitor = Monitors.FirstOrDefault(m => m.DeviceId == monitors.PrimaryMonitor?.DeviceId) ?? Monitors.FirstOrDefault();
        }

        public string SourcePath { get; private set; } = string.Empty;
        public string PreviewPath => !string.IsNullOrEmpty(SourcePath) ? SourcePath
            : _monitors.Monitors.FirstOrDefault(m => m.DeviceId == SelectedMonitor?.DeviceId && !m.IsStale)?.ThumbnailPath ?? string.Empty;
        private int _sourceWidth, _sourceHeight;
        private ScreenFitMonitor? _selectedMonitor;
        public ScreenFitMonitor? SelectedMonitor {
            get => _selectedMonitor;
            set {
                if (_selectedMonitor == value) return;
                _selectedMonitor = value; OnPropertyChanged(); UpdatePlan();
            }
        }
        private int _fitIndex = 1;
        public int FitIndex {
            get => _fitIndex;
            set { _fitIndex = value == 0 ? 0 : 1; OnPropertyChanged(); UpdatePlan(); }
        }
        private bool _useEnhancement;
        public bool UseEnhancement {
            get => _useEnhancement;
            set { _useEnhancement = value && NeedsEnhancement; OnPropertyChanged(); }
        }
        private bool _isBusy;
        public bool IsBusy {
            get => _isBusy;
            private set { _isBusy = value; OnPropertyChanged(); OnPropertyChanged(nameof(CanEdit)); OnPropertyChanged(nameof(CanConfigure)); OnPropertyChanged(nameof(CanApply)); }
        }
        public bool CanEdit => !IsBusy;
        public bool CanConfigure => CanEdit && !string.IsNullOrEmpty(SourcePath);
        public bool LayoutSupported => _settings.Settings.WallpaperArrangement == WallpaperArrangement.Per;
        public string LayoutMessage => LayoutSupported ? string.Empty : Text("ScreenFit_IndependentOnly");
        public ScreenFitPlan? Plan { get; private set; }
        public bool NeedsEnhancement => Plan?.NeedsEnhancement == true;
        public bool CanApply => !IsBusy && LayoutSupported && Plan != null && File.Exists(SourcePath);
        public Stretch PreviewStretch => FitIndex == 0 ? Stretch.Uniform : Stretch.UniformToFill;
        public double PreviewWidth => SelectedMonitor == null ? 460 : SelectedMonitor.Width * PreviewScale;
        public double PreviewHeight => SelectedMonitor == null ? 260 : SelectedMonitor.Height * PreviewScale;
        private double PreviewScale => Math.Min(460d / SelectedMonitor!.Width, 260d / SelectedMonitor.Height);
        public string PlanText => Plan == null ? string.Empty : string.Format(Text("ScreenFit_Plan"),
            _sourceWidth, _sourceHeight, Math.Round(Plan.VisibleWidth), Math.Round(Plan.VisibleHeight));
        public string EnhancementText => Plan == null ? string.Empty : NeedsEnhancement
            ? string.Format(Text("ScreenFit_EnhanceSize"), Plan.OutputWidth, Plan.OutputHeight)
            : Text("ScreenFit_NoEnhancementNeeded");
        private string _errorMessage = string.Empty;
        public string ErrorMessage { get => _errorMessage; private set { _errorMessage = value; OnPropertyChanged(); } }
        private string _progressText = string.Empty;
        public string ProgressText { get => _progressText; private set { _progressText = value; OnPropertyChanged(); } }

        internal void SetSource(string path, int width, int height) {
            SourcePath = path; _sourceWidth = width; _sourceHeight = height;
            OnPropertyChanged(nameof(SourcePath)); UseEnhancement = false; ErrorMessage = string.Empty; UpdatePlan();
        }
        private void UpdatePlan() {
            Plan = SelectedMonitor == null || _sourceWidth <= 0 || _sourceHeight <= 0 ? null
                : ScreenFitPlan.Calculate(_sourceWidth, _sourceHeight, SelectedMonitor.Width, SelectedMonitor.Height, FitIndex == 1);
            if (!NeedsEnhancement) UseEnhancement = false;
            foreach (var property in new[] { nameof(Plan), nameof(PlanText), nameof(NeedsEnhancement), nameof(EnhancementText),
                nameof(PreviewPath), nameof(PreviewStretch), nameof(PreviewWidth), nameof(PreviewHeight), nameof(CanConfigure), nameof(CanApply) }) OnPropertyChanged(property);
        }
        public async Task SelectImageAsync() {
            if (IsBusy) return;
            IsBusy = true;
            try {
                var files = await _picker.PickFilesAsync(WindowConsts.WindowHandle, [.. FileFilter.FileTypeToExtension[FileType.FImage]], false);
                if (files == null || files.Length == 0) return;
                var file = await StorageFile.GetFileFromPathAsync(files[0].Path);
                using var stream = await file.OpenReadAsync();
                var decoder = await BitmapDecoder.CreateAsync(stream);
                SetSource(file.Path, checked((int)decoder.OrientedPixelWidth), checked((int)decoder.OrientedPixelHeight));
            }
            catch (Exception ex) { ArcLog.GetLogger<ScreenFitViewModel>().Error(ex); ErrorMessage = Text("ScreenFit_ReadFailed"); }
            finally { IsBusy = false; }
        }

        public async Task<bool> ApplyAsync(CancellationToken ct) {
            if (!LayoutSupported) { ErrorMessage = Text("ScreenFit_DisplayChanged"); return false; }
            if (!CanApply) return false;
            var selected = SelectedMonitor!;
            var plan = Plan!;
            int scaling = (int)(FitIndex == 0 ? ObjectFit.Contain : ObjectFit.Cover);
            string source = SourcePath;
            bool enhance = UseEnhancement && plan.NeedsEnhancement;
            string? tempDir = null;
            IsBusy = true; ErrorMessage = string.Empty;
            try {
                ct.ThrowIfCancellationRequested();
                if (enhance) {
                    ProgressText = Text("ScreenFit_Enhancing");
                    tempDir = Path.Combine(Constants.CommonPaths.TempDir, "screen-fit-" + Guid.NewGuid().ToString("N"));
                    Directory.CreateDirectory(tempDir);
                    string output = Path.Combine(tempDir, Path.GetFileNameWithoutExtension(source) + "-enhanced.png");
                    await Task.Run(() => {
                        using var model = _createModel();
                        model.LoadModel(); ct.ThrowIfCancellationRequested();
                        model.RunAndSave(source, output, (uint)plan.OutputWidth, (uint)plan.OutputHeight, ct);
                    }, ct);
                    source = output;
                    enhance = false;
                }
                ct.ThrowIfCancellationRequested();
                // Recheck after inference: display topology or layout may have changed in the meantime.
                var monitor = ResolveMonitor(selected);
                if (monitor == null) {
                    ErrorMessage = Text("ScreenFit_DisplayChanged"); return false;
                }
                ProgressText = Text("ScreenFit_Applying");
                var imported = await _wallpapers.CreateBasicDataAsync(source, FileType.FImage, ct);
                if (imported == null) throw new InvalidOperationException("Wallpaper import failed.");
                var data = DataAssist.GrpcToBasicData(imported);
                if (!data.IsAvailable()) throw new InvalidOperationException("Wallpaper metadata is invalid.");
                Imported?.Invoke(data);
                ct.ThrowIfCancellationRequested();
                var json = await _wallpapers.GetPlayerStartArgsAsync(data, RuntimeType.RImage, null, ct);
                var args = JsonSerializer.Deserialize(json, StartArgsWebContext.Default.StartArgsWeb)
                    ?? throw new InvalidOperationException("Wallpaper start arguments are missing.");
                var properties = JsonNodeUtil.GetWritableJson(args.WpEffectFilePathUsing!);
                properties[nameof(PictureAndGifCostumise.Scaling)]!["Value"] = scaling;
                foreach (var path in new[] { args.WpEffectFilePathUsing, args.WpEffectFilePathTemplate, args.WpEffectFilePathTemporary }.Distinct()) {
                    JsonNodeUtil.Write(path!, properties);
                }
                ct.ThrowIfCancellationRequested();
                monitor = ResolveMonitor(selected);
                if (monitor == null) { ErrorMessage = Text("ScreenFit_DisplayChanged"); return false; }
                var response = await _wallpapers.SetWallpaperAsync(monitor, data, RuntimeType.RImage, true, null,
                    args.WpEffectFilePathUsing, args.WpEffectFilePathTemplate, args.WpEffectFilePathTemporary, ct);
                if (!response.IsFinished) throw new InvalidOperationException("Wallpaper apply failed.");
                return true;
            }
            catch (Exception ex) when (ex is OperationCanceledException || ct.IsCancellationRequested) {
                ErrorMessage = Text("ScreenFit_Canceled"); return false;
            }
            catch (Exception ex) {
                ArcLog.GetLogger<ScreenFitViewModel>().Error(ex);
                ErrorMessage = Text(enhance ? "ScreenFit_EnhanceFailed" : "ScreenFit_ApplyFailed"); return false;
            }
            finally {
                if (tempDir != null && Directory.Exists(tempDir)) {
                    try { Directory.Delete(tempDir, recursive: true); }
                    catch (IOException ex) { ArcLog.GetLogger<ScreenFitViewModel>().Warn(ex.Message); }
                    catch (UnauthorizedAccessException ex) { ArcLog.GetLogger<ScreenFitViewModel>().Warn(ex.Message); }
                }
                ProgressText = string.Empty; IsBusy = false;
            }
        }
        private IMonitor? ResolveMonitor(ScreenFitMonitor selected) => !LayoutSupported ? null
            : _monitors.Monitors.FirstOrDefault(m => m.DeviceId == selected.DeviceId && !m.IsStale
                && m.Bounds.Width == selected.Width && m.Bounds.Height == selected.Height);
        private static string Text(string key) => LanguageUtil.GetI18n(key);
    }
}
