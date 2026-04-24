using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Graphics.Canvas;
using Microsoft.UI;
using VirtualPaper.Common;
using VirtualPaper.Common.Utils.DI;
using VirtualPaper.Grpc.Client.Interfaces;
using VirtualPaper.UIComponent.Context;
using Windows.Foundation;
using Workloads.Creation.StaticImg.Core.Utils;
using Workloads.Creation.StaticImg.Models;
using Workloads.Creation.StaticImg.Models.SerializableData;
using Workloads.Creation.StaticImg.Models.Specific;

namespace Workloads.Creation.StaticImg.ViewModels {
    public partial class InkCanvasViewModel {
        public InkCanvasData Data { get; }
        public InkProjectSession Session => _session;

        public InkCanvasViewModel(InkProjectSession session, ArcPageContext context) {
            _session = session;
            _userSettings = AppServiceLocator.Services.GetRequiredService<IUserSettingsClient>();
            Data = new InkCanvasData(session, context);
        }

        internal async Task<bool> SaveAsync(bool isEmergency = false) {
            (var flag, var filePath) = isEmergency ? await Data.SaveAtEmergencyAsync(_session) : await Data.SaveAsync(_session);
            if (flag) {
                await _userSettings.UpdateRecentUsedAsync(filePath!);
            }
            return flag;
        }

        internal async Task UpdateRecentUsedAsync(string filePath) {
            if (!string.IsNullOrEmpty(filePath)) {
                await _userSettings.UpdateRecentUsedAsync(filePath);
            }
        }

        internal async Task LoadAsync() {
            string inputPath = _session.DesignFileUtil.FilePath;
            if (Path.GetExtension(inputPath) == FileExtension.FE_Design) {
                await Data.LoadAsync(_session);
            } else {
                Data.InitData();

                // Prepare business data
                var businessData = new BusinessData();
                businessData.SetColors(Data.CustomColors);
                businessData.SelectedLayerIndex = Math.Max(0, Data.ActiveLayers.ToList().IndexOf(Data.SelectedLayer));

                // Prepare layers
                var layers = new List<Layer>();
                foreach (var layerInfo in Data.ActiveLayers) {
                    if (!layerInfo.IsDeleted && layerInfo.RenderData != null) {
                        var state = new LayerState() {
                            IsVisible = layerInfo.IsVisible,
                            ZIndex = layerInfo.ZIndex,
                        };
                        layers.Add(new Layer(layerInfo.Name, state, layerInfo.RenderData));
                    }
                }

                await _session.DesignFileUtil.InitCacheAsync(Data.CanvasSize, businessData, layers);

                if (File.Exists(inputPath)) {
                    await SetRenderDataAsync(inputPath, Data.SelectedLayer);
                }
            }
        }

        private async Task SetRenderDataAsync(string filePath, LayerInfo layerInfo) {
            var renderTarget = layerInfo.RenderData.RenderTarget;
            var device = renderTarget.Device;
            var bitmap = await CanvasBitmap.LoadAsync(device, filePath);
            using (var ds = renderTarget.CreateDrawingSession()) {
                ds.Clear(Colors.Transparent);
                ds.DrawImage(
                    bitmap,
                    new Rect(0, 0, renderTarget.SizeInPixels.Width, renderTarget.SizeInPixels.Height),
                    new Rect(0, 0, bitmap.SizeInPixels.Width, bitmap.SizeInPixels.Height),
                    1.0f,
                    CanvasImageInterpolation.HighQualityCubic);
            }
            layerInfo.RenderData.HandleOnceRenderCompleted();
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
            new() { Type = ToolType.Selection, ToolName = "Project_StaticImg_ToolName_Selection", Glyph = "\uE8B0", },
            new() { Type = ToolType.PaintBrush, ToolName = "Project_StaticImg_ToolName_PaintBrush", Glyph = "\uEE56", },
            new() { Type = ToolType.Fill, ToolName = "Project_StaticImg_ToolName_Fill", ImageSourceKey = "DraftPanel_FuncBar_ColorFill", },
            new() { Type = ToolType.Eraser, ToolName = "Project_StaticImg_ToolName_Eraser", Glyph = "\uE75C", },
            new() { Type = ToolType.Crop, ToolName = "Project_StaticImg_ToolName_Crop", Glyph = "\uE7A8", },
            new() { Type = ToolType.CanvasSet, ToolName = "Project_StaticImg_ToolName_CanvasSet", Glyph = "\uE9E9", },
        ];
        private readonly InkProjectSession _session = null!;
        private readonly IUserSettingsClient _userSettings;
    }
}
