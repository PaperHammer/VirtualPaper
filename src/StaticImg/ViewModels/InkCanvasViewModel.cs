using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using VirtualPaper.Common;
using VirtualPaper.Models.Mvvm;
using Workloads.Creation.StaticImg.Models;

namespace Workloads.Creation.StaticImg.ViewModels {
    internal partial class InkCanvasViewModel : ObservableObject {
        internal TaskCompletionSource<bool> BasicDataLoaded => _basicDataLoaded;
        internal TaskCompletionSource<bool> RenderDataLoaded => _renderDataLoaded;

        private InkCanvasConfigData _configData;
        public InkCanvasConfigData ConfigData {
            get { return _configData; }
            set { _configData = value; }
        }

        public InkCanvasViewModel(string entryFilePath, FileType fileType) {
            _fileType = fileType;
            _entryFilePath = entryFilePath;
            ConfigData = new InkCanvasConfigData(entryFilePath);
        }

        internal async Task SaveAsync() {
            await ConfigData.SaveBasicAsync();
            await ConfigData.SaveRenderDataAsync();
        }

        internal async Task LoadBasicOrInit() {
            switch (_fileType) {
                case FileType.FImage:
                    break;
                case FileType.FProject:
                    await LoadBasicDataAsync();
                    break;
                default:
                    break;
            }
        }

        private async Task LoadBasicDataAsync() {          
            if (!File.Exists(_entryFilePath)) {
                await ConfigData.InitDataAsync();
                await ConfigData.SaveRenderDataAsync();
            }
            await ConfigData.LoadBasicDataAsync();
            BasicDataLoaded.TrySetResult(true);
        }

        internal async Task LoadRenderDataAsync() {
            await ConfigData.LoadRenderDataAsync();
            RenderDataLoaded.TrySetResult(true);
        }

        private readonly string _entryFilePath;
        private readonly FileType _fileType;
        private readonly TaskCompletionSource<bool> _basicDataLoaded = new(), _renderDataLoaded = new();
        internal readonly List<AspectRatioItem> _aspectRatios = [
            new(displayText: "16:9", borderWidth: 48, borderHeight: 27 ),
            new(displayText: "5:3", borderWidth: 40, borderHeight: 24),
            new(displayText : "3:2", borderWidth : 39, borderHeight : 26),
            new(displayText : "4:3", borderWidth : 40, borderHeight : 30),
            new(displayText : "1:1", borderWidth : 30, borderHeight : 30),
            new(displayText : "9:16", borderWidth : 27, borderHeight : 48)
        ];
        internal readonly List<ToolItem> _toolItems = [
            new() { Type = ToolType.Selection, ToolName = "选择", Glyph = "\uE8B0", },
            new() { Type = ToolType.PaintBrush, ToolName = "画笔", Glyph = "\uEE56", },
            new() { Type = ToolType.Fill, ToolName = "填充", ImageSourceKey = "DraftPanel_FuncBar_ColorFill", },
            new() { Type = ToolType.Eraser, ToolName = "擦除", Glyph = "\uE75C", },
            new() { Type = ToolType.Crop, ToolName = "裁剪", Glyph = "\uE7A8", },
            new() { Type = ToolType.CanvasSet, ToolName = "画布", Glyph = "\uE9E9", },
        ];
    }
}
