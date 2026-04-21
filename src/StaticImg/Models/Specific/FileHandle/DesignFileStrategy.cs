using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using VirtualPaper.Common;
using VirtualPaper.Common.Logging;
using VirtualPaper.Common.Utils.Storage;
using VirtualPaper.Models.Mvvm;
using VirtualPaper.UIComponent;
using Workloads.Creation.StaticImg.Models.SerializableData;
using Workloads.Creation.StaticImg.Models.Specific.Strategies;

namespace Workloads.Creation.StaticImg.Models.Specific.FileHandle {
    public class DesignFileStrategy : IInkFileStrategy {
        public async Task<bool> LoadAsync(InkCanvasData data) {
            try {
                var (header, businessData, layers) = await data.Session.DesignFileUtil.LoadAsync(data.Session);

                if (header.HasValue) {
                    data.CanvasSize = new ArcSize(header.Value.CanvasWidth, header.Value.CanvasHeight, header.Value.Dpi, RebuildMode.None);
                }

                // Update colors
                data.CustomColors.Clear();
                if (businessData?.Colors != null) {
                    data.CustomColors.AddRange(businessData.Colors);
                }

                // Update layers
                data.AllLayers.Clear();
                data.Layers.Clear();
                if (layers != null) {
                    foreach (var layer in layers) {
                        var layerInfo = new LayerInfo {
                            Name = layer.Name,
                            RenderData = layer.RenderData.Clone(),
                            IsVisible = layer.State.IsVisible,
                            ZIndex = layer.State.ZIndex,
                        };
                        layerInfo.RenderData.IsReady.SetResult(true);
                        layerInfo.PropertyChanged += data.OnLayerPropertyChanged;
                        data.AllLayers.Add(layerInfo);
                        data.Layers.Add(layerInfo);
                    }
                }
                else {
                    data.AddLayer(isBackground: true, needRecord: false);
                }

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
            if (!data.Session.DesignFileUtil.IsValidFile) {
                string desktopPath = Environment.GetFolderPath(Environment.SpecialFolder.Desktop);
                string timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
                string fileName = $"virtualpaperdesign_{timestamp}{FileExtension.FE_Design}";
                var path = Path.Combine(desktopPath, fileName);
                data.Session.DesignFileUtil.SetFilePath(path);
            }

            return await SaveAsync(data);
        }

        public async Task<(bool Success, string? FinalPath)> SaveAsync(InkCanvasData data) {
            try {
                if (!data.Session.DesignFileUtil.IsValidFile) {
                    var suggestedName = Path.GetFileNameWithoutExtension(data.Session.DesignFileUtil.FilePath);
                    var saveFile = await WindowsStoragePickers.PickSaveFileAsync(
                        WindowConsts.WindowHandle,
                        suggestedName,
                        new Dictionary<string, string[]>() {
                            ["Virtual Paper Design"] = [FileExtension.FE_Design]
                        }
                    );
                    if (saveFile == null || string.IsNullOrEmpty(saveFile.Path))
                        return (false, null);

                    data.Session.DesignFileUtil.SetFilePath(saveFile.Path);
                }

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
