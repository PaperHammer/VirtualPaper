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
using VirtualPaper.UIComponent.Utils;
using Workloads.Creation.StaticImg.Core.Utils;
using Workloads.Creation.StaticImg.Models.SerializableData;

namespace Workloads.Creation.StaticImg.Models.Specific {
    // IO part of InkCanvasData
    public partial class InkCanvasData {
        public async Task<bool> LoadAsync(InkProjectSession session) {
            try {
                var (header, businessData, layers) = await session.DesignFileUtil.LoadAsync(session);

                if (header.HasValue) {
                    CanvasSize = new ArcSize(header.Value.CanvasWidth, header.Value.CanvasHeight, header.Value.Dpi, RebuildMode.None);
                }

                // Update colors
                CustomColors.Clear();
                if (businessData?.Colors != null) {
                    CustomColors.AddRange(businessData.Colors);
                }

                // Update layers
                _allLayers.Clear();
                _layers.Clear();
                if (layers != null) {
                    //foreach (var layer in layers) {
                    //_allLayers.Add(new LayerInfo {
                    //    Name = layer.Name ?? string.Empty,
                    //    IsVisible = layer.State.IsVisible,
                    //    RenderData = layer.RenderData
                    //});
                    //_layers.Clear();
                    //_layers.AddRange(_allLayers);
                    //}

                    foreach (var layer in layers) {
                        var layerInfo = new LayerInfo {
                            Name = layer.Name,
                            RenderData = layer.RenderData.Clone(),
                            IsVisible = layer.State.IsVisible,
                            ZIndex = layer.State.ZIndex,
                        };

                        layerInfo.PropertyChanged += OnLayerPropertyChanged;
                        _allLayers.Add(layerInfo);
                        _layers.Add(layerInfo);
                    }
                }
                else {
                    AddLayer(isBackground: true);
                }

                // Select first layer if none selected
                if (SelectedLayer == null && ActiveLayers.Count > 0) {
                    SelectedLayer = ActiveLayers[0];
                }

                return true;
            }
            catch (Exception ex) {
                ArcLog.GetLogger<MainPage>().Error($"Load failed: {ex.Message}");
                return false;
            }
        }

        public async Task<(bool Success, string? FinalPath)> SaveAsync(InkProjectSession session) {
            try {
                if (!session.DesignFileUtil.IsValidFile) {
                    var saveFile = await WindowsStoragePickers.PickSaveFileAsync(
                        WindowConsts.WindowHandle,
                        Path.GetFileName(session.DesignFileUtil.FilePath),
                        new Dictionary<string, string[]>() {
                            ["Virtual Paper Design"] = [FileExtension.FE_Design]
                        }
                    );
                    if (saveFile == null || string.IsNullOrEmpty(saveFile.Path))
                        return (false, null);

                    session.DesignFileUtil.SetFilePath(saveFile.Path);
                }

                // Prepare business data
                var businessData = new BusinessData();
                businessData.SetColors(CustomColors);
                businessData.SelectedLayerIndex = Math.Max(0, ActiveLayers.ToList().IndexOf(SelectedLayer));

                // Prepare layers
                var layers = new List<Layer>();
                foreach (var layerInfo in Layers) {
                    if (!layerInfo.IsDeleted && layerInfo.RenderData != null) {
                        var state = new LayerState() {
                            IsVisible = layerInfo.IsVisible,
                            ZIndex = layerInfo.ZIndex,
                        };
                        layers.Add(new Layer(layerInfo.Name, state, layerInfo.RenderData));
                    }
                }

                // Save through project utility
                (var flag, var filePath) = await session.DesignFileUtil.SaveAsync(CanvasSize, businessData, layers);

                return (flag, filePath);
            }
            catch (Exception ex) {
                ArcLog.GetLogger<MainPage>().Error($"Save failed: {ex.Message}");
                return (false, null);
            }
        }

        // TODO：有差异且触发关闭或退出时做出拦截
        public bool CheckHasDiff(StaticImgDesignFileUtil designFileUtil) {
            return designFileUtil.HasDiff;
        }
    }
}
