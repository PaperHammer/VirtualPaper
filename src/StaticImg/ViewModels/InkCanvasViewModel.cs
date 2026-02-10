using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using VirtualPaper.Common;
using VirtualPaper.UIComponent.Context;
using Workloads.Creation.StaticImg.Core.Utils;
using Workloads.Creation.StaticImg.Models;
using Workloads.Creation.StaticImg.Models.Specific;

namespace Workloads.Creation.StaticImg.ViewModels {
    public partial class InkCanvasViewModel {
        public InkCanvasData Data { get; }

        public InkCanvasViewModel(InkProjectSession session, ArcPageContext context) {
            _session = session;
            Data = new InkCanvasData(session, context);
        }

        internal async Task SaveAsync() {
            await Data.SaveAsync(_session);
        }

        internal async Task LoadAsync() {
            switch (_session.RTFileType) {
                case FileType.FImage:
                    await LoadSttaicImageAsync();
                    break;
                case FileType.FDesign:
                    await LoadDesignAsync();
                    break;
                default:
                    break;
            }
        }

        private async Task LoadSttaicImageAsync() {
            // todo
        }

        private async Task LoadDesignAsync() {
            if (!File.Exists(_session.DesignFileUtil.FilePath)) {
                Data.InitData();
            }
            else {
                await Data.LoadAsync(_session);
            }
        }

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
        private readonly InkProjectSession _session = null!;
    }
}
