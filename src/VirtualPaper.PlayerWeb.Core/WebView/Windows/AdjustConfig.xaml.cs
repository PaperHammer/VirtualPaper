using System;
using System.IO;
using System.Text.Json;
using Microsoft.UI.Xaml;
using VirtualPaper.Common;
using VirtualPaper.Common.Events;
using VirtualPaper.Common.Logging;
using VirtualPaper.Common.Runtime.PlayerWeb;
using VirtualPaper.Common.Utils.Storage;
using VirtualPaper.Models.Cores;
using VirtualPaper.PlayerWeb.Core.Interfaces;
using VirtualPaper.PlayerWeb.Core.Utils.Interfaces;
using VirtualPaper.PlayerWeb.Core.WebView.Pages;
using VirtualPaper.UIComponent.Templates;
using VirtualPaper.UIComponent.Utils;
using VirtualPaper.UIComponent.Utils.Extensions;

// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.

namespace VirtualPaper.PlayerWeb.Core.WebView.Windows {
    /// <summary>
    /// An empty window that can be used on its own or navigated to within a Frame.
    /// </summary>
    public sealed partial class AdjustConfig : ArcWindow, IApplyService, IEffectService {
        public event EventHandler<EffectValueChangedBase>? EffectChanged;
        public event EventHandler<ApplyEventArgs>? Applied;

        public override ArcWindowHost ContentHost => this.MainHost;
        public override ArcWindowManagerKey Key => _windowKey;

        public AdjustConfig(string jsonString) {
            _startArgs = JsonSerializer.Deserialize<StartArgsWeb>(jsonString);
            _windowKey = new ArcWindowManagerKey(ArcWindowKey.PlayerWebCoreAdjust, _startArgs.FilePath + _startArgs.RuntimeType);
            this.InitializeComponent();
            UpdateWindowTitle();
            InitializeWindow();

            LanguageUtil.LanguageUpdated += OnLanguageUpdated;
            Closed += (_, _) => LanguageUtil.LanguageUpdated -= OnLanguageUpdated;

            AfterFeReady();
        }

        private void NaviContent_Loaded(object sender, RoutedEventArgs e) {
            try {
                var payload = new FrameworkPayload() {
                    [NaviPayloadKey.StartArgs.ToString()] = _startArgs,
                    [NaviPayloadKey.IWpBasicData.ToString()] = _wpBasicData,
                    [NaviPayloadKey.AvailableConfigTab.ToString()] = DataConfigTab.GeneralEffect | DataConfigTab.GeneralInfo,
                    [NaviPayloadKey.ApplyService.ToString()] = this,
                    [NaviPayloadKey.IEffectService.ToString()] = this,
                };
                NaviContent.Navigate(typeof(PageOnlyDataConfig), payload);
            }
            catch (Exception ex) {
                ArcLog.GetLogger<AdjustConfig>().Error(ex);
            }
        }

        private async void AfterFeReady() {
            _wpBasicData ??= await JsonSaver.LoadAsync<WpBasicData>(_startArgs.WpBasicDataFilePath, WpBasicDataContext.Default);
            UpdateWindowTitle();
        }

        private void OnLanguageUpdated(object? sender, EventArgs args) => UpdateWindowTitle();

        private void UpdateWindowTitle() {
            string name = !string.IsNullOrEmpty(_wpBasicData?.Title) ? _wpBasicData.Title :
                (!string.IsNullOrEmpty(_startArgs.FilePath) ? Path.GetFileName(_startArgs.FilePath) : "Virtual Paper PlayerWeb");
            string windowTitle = $"{name} ({LanguageUtil.GetI18n(Constants.I18n.Text_Adjust)})";
            this.Title = this.MainHost.Title = windowTitle;
        }

        public void OnApply(ApplyEventArgs args) {
            Applied?.Invoke(this, args);
        }

        public void UpdateEffectValue<T>(EffectValueChanged<T> e) {
            EffectChanged?.Invoke(this, e);
        }

        private readonly StartArgsWeb _startArgs = null!;
        private readonly ArcWindowManagerKey _windowKey;
        private WpBasicData? _wpBasicData;
    }
}
