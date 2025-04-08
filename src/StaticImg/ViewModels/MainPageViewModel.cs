using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Microsoft.UI.Input;
using VirtualPaper.Common;
using VirtualPaper.Common.Utils.Storage;
using VirtualPaper.Models.Mvvm;
using VirtualPaper.UIComponent.Utils;
using VirtualPaper.UIComponent.Utils.ArcEventArgs;
using Workloads.Creation.StaticImg.Models;

namespace Workloads.Creation.StaticImg.ViewModels {
    internal partial class MainPageViewModel : ObservableObject {
        internal event EventHandler<double> OnCanvasZoomChanged;

        public string SIG_Text_AddLayer { get; set; }
        public string SIG_Text_CopyLayer { get; set; }
        public string SIG_Text_RenameLayer { get; set; }
        public string SIG_Text_DeleteLayer { get; set; }

        private double _canvasZoom; // 0.2 -- 7.0
        public double CanvasZoom {
            get { return _canvasZoom; }
            set {
                if (!Consts.IsZoomValid(value) || _canvasZoom == value) return;

                _canvasZoom = value;
                OnPropertyChanged();
                OnCanvasZoomChanged?.Invoke(this, value);
            }
        }

        public List<ToolItem> ToolItems { get; private set; }
        public LayerManagerData ManagerData { get; } // (entryFile)

        PaintBrushItem _seletcedBrush;
        public PaintBrushItem SelectedBrush {
            get { return _seletcedBrush; }
            set { if (_seletcedBrush == value) return; _seletcedBrush = value; OnPropertyChanged(); }
        }

        ToolItem _selectedToolItem;
        public ToolItem SelectedToolItem {
            get { return _selectedToolItem; }
            set { if (_selectedToolItem == value) return; _selectedToolItem = value; ManagerData.Cursor = value.Cursor; ManagerData.SelectedToolType = value.Type; OnPropertyChanged(); }
        }

        double _brushThickness = 5;
        public double BrushThickness {
            get { return _brushThickness; }
            set { if (_brushThickness == value) return; _brushThickness = value; ManagerData.BrushThickness = value; OnPropertyChanged(); }
        }
        
        double _brushOpacity = 100;
        public double BrushOpacity {
            get { return _brushOpacity; }
            set { if (_brushOpacity == value) return; _brushOpacity = value; ManagerData.BrushOpacity = value; OnPropertyChanged(); }
        }

        public MainPageViewModel(string entryFilePath, FileType rtFileType) {
            _entryFilePath = entryFilePath;
            _rtFileType = rtFileType;
            ManagerData = new(entryFilePath);

            InitText();
            InitToolItems();
        }

        #region init
        private void InitToolItems() {
            ToolItems = [
                new() {
                    ToolName = "移动",
                    Glyph = "\uE7C2",
                },
                new() {
                    ToolName = "选择",
                    Glyph = "\uE8B0",
                },
                new() {
                    Type = ToolType.PaintBrush,
                    Cursor = InputSystemCursor.Create(InputSystemCursorShape.Cross),
                    ToolName = "画笔",
                    Glyph = "\uEE56",
                },
                new() {
                    ToolName = "填充",
                    ImageSourceKey = "DraftPanel_FuncBar_ColorFill",
                },
                new() {
                    ToolName = "擦除",
                    Glyph = "\uE75C",

                },
                new() {
                    ToolName = "裁剪",
                    Glyph = "\uE7A8",
                },
                new() {
                    ToolName = "画布",
                    Glyph = "\uE9E9",
                },
            ];
        }

        private void InitText() {
            SIG_Text_AddLayer = LanguageUtil.GetI18n(nameof(Constants.I18n.SIG_Text_AddLayer));
            SIG_Text_CopyLayer = LanguageUtil.GetI18n(nameof(Constants.I18n.SIG_Text_CopyLayer));
            SIG_Text_RenameLayer = LanguageUtil.GetI18n(nameof(Constants.I18n.SIG_Text_RenameLayer));
            SIG_Text_DeleteLayer = LanguageUtil.GetI18n(nameof(Constants.I18n.SIG_Text_DeleteLayer));
        }
        #endregion

        public async Task SaveAsync() {
            try {
                await JsonSaver.SaveAsync(_entryFilePath, ManagerData, LayerManagerDataContext.Default);
                foreach (var item in ManagerData.LayersData) {
                    await item.SaveAsync();
                }
            }
            catch (Exception ex) {
                MainPage.Instance.Bridge.Log(LogType.Error, ex);
                MainPage.Instance.Bridge.GetNotify().ShowExp(ex);
            }
        }

        public async Task LoadAsync() {
            try {
                MainPage.Instance.Bridge.GetNotify().Loading(false, false);

                switch (_rtFileType) {
                    case FileType.FImage:
                        break;
                    case FileType.FProject:
                        await LoadProjectAsync();
                        break;
                    default:
                        break;
                }
            }
            catch (Exception ex) {
                MainPage.Instance.Bridge.Log(LogType.Error, ex);
                MainPage.Instance.Bridge.GetNotify().ShowExp(ex);
            }
            finally {
                MainPage.Instance.Bridge.GetNotify().Loaded();
            }
        }

        private async Task LoadProjectAsync() {
            if (!File.Exists(_entryFilePath)) {
                await ManagerData.InitDataAsync();
            }
            await ManagerData.LoadAsync();
        }

        internal async Task AddLayerAsync() {
            try {
                MainPage.Instance.Bridge.GetNotify().Loading(false, false);
                await ManagerData.AddLayerAsync();
            }
            catch (Exception ex) {
                MainPage.Instance.Bridge.Log(LogType.Error, ex);
                MainPage.Instance.Bridge.GetNotify().ShowExp(ex);
            }
            finally {
                MainPage.Instance.Bridge.GetNotify().Loaded();
            }
        }

        internal async Task CopyLayerAsync(long itemTag) {
            try {
                MainPage.Instance.Bridge.GetNotify().Loading(false, false);
                await ManagerData.CopyLayerAsync(itemTag);
            }
            catch (Exception ex) {
                MainPage.Instance.Bridge.GetNotify().ShowExp(ex);
            }
            finally {
                MainPage.Instance.Bridge.GetNotify().Loaded();
            }
        }

        internal async Task RenameAsync(long itemTag) {
            try {
                MainPage.Instance.Bridge.GetNotify().Loading(false, false);
                await ManagerData.RenameAsync(itemTag);
            }
            catch (Exception ex) {
                MainPage.Instance.Bridge.GetNotify().ShowExp(ex);
            }
            finally {
                MainPage.Instance.Bridge.GetNotify().Loaded();
            }
        }

        internal async Task DeleteAsync(long itemTag) {
            try {
                MainPage.Instance.Bridge.GetNotify().Loading(false, false);
                await ManagerData.DeleteAsync(itemTag);
            }
            catch (Exception ex) {
                MainPage.Instance.Bridge.GetNotify().ShowExp(ex);
            }
            finally {
                MainPage.Instance.Bridge.GetNotify().Loaded();
            }
        }

        internal async Task UpdateCustomColorsAsync(ColorChnageEventArgs e) {
            await ManagerData.UpdateCustomColorsAsync(e);
        }

        internal async Task UpdateForegroundColorsAsync(ColorChnageEventArgs e) {
            await ManagerData.UpdateForegroundColorsAsync(e);
        }

        internal async Task UpdateBackgroundColorsAsync(ColorChnageEventArgs e) {
            await ManagerData.UpdateBackgroundColorsAsync(e);
        }

        private readonly FileType _rtFileType;
        private readonly string _entryFilePath = string.Empty;
        internal readonly string[] _comboZoomFactors = ["700%", "600%", "500%", "400%", "300%", "200%", "100%", "75%", "50%", "25%"];
    }
}
