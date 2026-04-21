using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using VirtualPaper.Common;
using VirtualPaper.Common.Logging;
using VirtualPaper.Common.Utils.Storage;
using VirtualPaper.UIComponent;
using Workloads.Creation.StaticImg.Models.SerializableData;
using Workloads.Creation.StaticImg.Models.Specific.Strategies;
using Workloads.Creation.StaticImg.Utils;

namespace Workloads.Creation.StaticImg.Models.Specific.FileHandle {
    public class ImageFileStrategy : IInkFileStrategy {
        public async Task<bool> LoadAsync(InkCanvasData data) {
            try {
                var (width, height) = await ImgUtils.GetImagePixelSizeAsync(data.Session.DesignFileUtil.FilePath);
                if (width <= 0 || height <= 0) return false;

                data.CanvasSize = new ArcSize(width, height, 96, RebuildMode.None);

                // Update layers
                data.AllLayers.Clear();
                data.Layers.Clear();
                await data.AddLayerWithDataAsync(data.Session.DesignFileUtil.FilePath);

                // Select first layer if none selected
                if (data.SelectedLayer == null && data.ActiveLayers.Count > 0) {
                    data.SelectedLayer = data.ActiveLayers[0];
                }

                return true;
            }
            catch (Exception ex) {
                ArcLog.GetLogger<MainPage>().Error($"Load failed: {ex.Message}");
                return false;
            }
        }

        public async Task<(bool Success, string? FinalPath)> SaveAtEmergencyAsync(InkCanvasData data) {
            return await SaveAsync(data);
        }

        public async Task<(bool Success, string? FinalPath)> SaveAsync(InkCanvasData data) {
            try {
                // Prepare business data
                var businessData = new BusinessData();
                businessData.SetColors(data.CustomColors);
                businessData.SelectedLayerIndex = Math.Max(0, data.ActiveLayers.ToList().IndexOf(data.SelectedLayer));

                // Prepare layers
                var layers = new List<Layer>();
                foreach (var layerInfo in data.Layers) {
                    if (!layerInfo.IsDeleted && layerInfo.RenderData != null) {
                        var state = new LayerState() {
                            IsVisible = layerInfo.IsVisible,
                            ZIndex = layerInfo.ZIndex,
                        };
                        layers.Add(new Layer(layerInfo.Name, state, layerInfo.RenderData));
                    }
                }

                // Save through project utility
                (var flag, var filePath) = await data.Session.DesignFileUtil.SaveAsync(data.CanvasSize, businessData, layers);
                data.Session.UnReUtil.MarkAsSaved();

                return (flag, filePath);
            }
            catch (Exception ex) {
                ArcLog.GetLogger<MainPage>().Error($"Save failed: {ex.Message}");
                return (false, null);
            }
        }
    }
}
