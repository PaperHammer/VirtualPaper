using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using VirtualPaper.Common.Logging;
using VirtualPaper.Models.Mvvm;
using Workloads.Creation.StaticImg.Core.Utils;
using Workloads.Creation.StaticImg.Models.SerializableData;

namespace Workloads.Creation.StaticImg.Models.Specific {
    // IO part of InkCanvasData
    public partial class InkCanvasData {
        public async Task<bool> LoadAsync(InkProjectSession session) {
            try {
                var (header, contentAbstract, layers) = await session.DesignFileUtil.LoadAsync(session);

                if (header.HasValue) {
                    CanvasSize = new ArcSize(header.Value.CanvasWidth, header.Value.CanvasHeight, header.Value.Dpi, RebuildMode.None);
                }

                // Update colors
                CustomColors.Clear();
                if (contentAbstract?.Colors != null) {
                    CustomColors.AddRange(contentAbstract.Colors);
                }

                // Update layers
                _allLayers.Clear();
                if (layers != null) {
                    foreach (var layer in layers) {
                        _allLayers.Add(new LayerInfo {
                            Name = layer.Name ?? string.Empty,
                            IsVisible = layer.IsVisible,
                            RenderData = layer.RenderData
                        }); 
                        _layers.Clear();
                        _layers.AddRange(_allLayers);
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

        public async Task<bool> SaveAsync(InkProjectSession session) {
            try {
                // Prepare business data
                var businessData = new BusinessData();
                businessData.SetColors(CustomColors);

                // Prepare layers
                var layers = new List<Layer>();
                foreach (var layerInfo in ActiveLayers) {
                    if (!layerInfo.IsDeleted && layerInfo.RenderData != null) {
                        layers.Add(new Layer(layerInfo.Name, layerInfo.IsVisible, layerInfo.RenderData));
                    }
                }

                // Save through project utility
                await session.DesignFileUtil.SaveAsync(
                    CanvasSize.Width,
                    CanvasSize.Height,
                    CanvasSize.Dpi,
                    businessData,
                    layers
                );

                return true;
            }
            catch (Exception ex) {
                ArcLog.GetLogger<MainPage>().Error($"Save failed: {ex.Message}");
                return false;
            }
        }

        // TODO：有差异且触发关闭或退出时做出拦截
        public bool CheckHasDiff(StaticImgDesignFileUtil designFileUtil) {
            return designFileUtil.HasDiff;
        }
    }
}
