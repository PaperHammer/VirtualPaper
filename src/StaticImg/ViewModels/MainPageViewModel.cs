using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using VirtualPaper.Common;
using VirtualPaper.Models.Mvvm;
using VirtualPaper.UIComponent.Utils;
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

        private bool _isEnable;
        public bool IsEanble {
            get { return _isEnable; }
            set { _isEnable = value; OnPropertyChanged(); }
        }

        public List<ToolItem> ToolItems { get; private set; }
        public List<AspectRatioItem> AspectRatios { get; private set; }

        public MainPageViewModel() {
            InitText();
            InitToolItems();
            InitAspectTemplates();
        }

        #region init
        private void InitAspectTemplates() {
            AspectRatios = [
                new(displayText: "16:9", borderWidth: 48, borderHeight: 27 ),
                new(displayText: "5:3", borderWidth: 40, borderHeight: 24),
                new(displayText : "3:2", borderWidth : 39, borderHeight : 26),
                new(displayText : "4:3", borderWidth : 40, borderHeight : 30),
                new(displayText : "1:1", borderWidth : 30, borderHeight : 30),
                new(displayText : "9:16", borderWidth : 27, borderHeight : 48)
            ];
        }

        private void InitToolItems() {
            ToolItems = [
                new() { Type = ToolType.Selection, ToolName = "选择", Glyph = "\uE8B0", },
                new() { Type = ToolType.PaintBrush, ToolName = "画笔", Glyph = "\uEE56", },
                new() { Type = ToolType.Fill, ToolName = "填充", ImageSourceKey = "DraftPanel_FuncBar_ColorFill", },
                new() { Type = ToolType.Eraser, ToolName = "擦除", Glyph = "\uE75C", },
                new() { Type = ToolType.Crop, ToolName = "裁剪", Glyph = "\uE7A8", },
                new() { Type = ToolType.CanvasSet, ToolName = "画布", Glyph = "\uE9E9", },
            ];
        }

        private void InitText() {
            SIG_Text_AddLayer = LanguageUtil.GetI18n(nameof(Constants.I18n.SIG_Text_AddLayer));
            SIG_Text_CopyLayer = LanguageUtil.GetI18n(nameof(Constants.I18n.SIG_Text_CopyLayer));
            SIG_Text_RenameLayer = LanguageUtil.GetI18n(nameof(Constants.I18n.SIG_Text_RenameLayer));
            SIG_Text_DeleteLayer = LanguageUtil.GetI18n(nameof(Constants.I18n.SIG_Text_DeleteLayer));
        }
        #endregion

        internal readonly string[] _comboZoomFactors = ["700%", "600%", "500%", "400%", "300%", "200%", "100%", "75%", "50%", "25%"];
    }
}
