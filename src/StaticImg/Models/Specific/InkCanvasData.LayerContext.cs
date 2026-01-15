using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using BuiltIn.Events;
using VirtualPaper.Common;
using VirtualPaper.Common.Utils;
using VirtualPaper.Common.Utils.UndoRedo;
using VirtualPaper.Models.Mvvm;
using VirtualPaper.UIComponent.Others;
using VirtualPaper.UIComponent.Utils;
using VirtualPaper.UIComponent.ViewModels;
using Windows.Foundation;

namespace Workloads.Creation.StaticImg.Models.Specific {
    // LayerContext part of InkCanvasData
    public partial class InkCanvasData : ObservableObject {
        public event EventHandler<RenderTargetChangedEventArgs>? RenderRequest;
        public event EventHandler? SeletcedLayerChanged;
        public event EventHandler? GetFocus;

        public IReadOnlyList<LayerInfo> ActiveLayers {
            get {
                if (_isActiveLayersDirty)
                    RebuildActiveLayersCache();
                return _cachedActiveLayers;
            }
        }
        public ObservableCollection<LayerInfo> Layers => _layers;

        private LayerInfo _selectedLayer;
        public LayerInfo SelectedLayer {
            get { return _selectedLayer; }
            set {
                if (_selectedLayer == value) return;
                _selectedLayer = value;
                OnPropertyChanged();

                if (value == null) return;
                SeletcedLayerChanged?.Invoke(this, EventArgs.Empty);
                if (value.IsVisible) GlobalMessageUtil.CloseAndRemoveMsg(ArcWindowManager.GetArcWindow(new(ArcWindowKey.Main)), nameof(Constants.I18n.Draft_SI_LayerLocked));
            }
        }

        public LayerInfo AddLayer(string? name = null, bool isBackground = false) {
            var layer = new LayerInfo {
                Name = name ?? $"Layer {_allLayers.Count + 1}",
                RenderData = new InkRenderData(CanvasSize, isBackground)
            };

            _allLayers.Add(layer);
            _layers.Add(layer);
            _layerStates[layer.Tag] = new LayerState {
                IsDeleted = false,
                IsVisible = true,
                ZIndex = _allLayers.Count - 1
            };
            layer.PropertyChanged += OnLayerPropertyChanged;
            UpdateLayerState(layer);

            MainPage.Instance.UnReUtil.RecordCommand(
                new AddLayerCommand(this, layer)
            );
            return layer;
        }

        public LayerInfo? CopyLayer(Guid layerId) {
            var originalLayer = _allLayers.FirstOrDefault(x => x.Tag == layerId);
            if (originalLayer == null) return null;

            var layer = new LayerInfo {
                Name = originalLayer.Name + " Copy",
                IsVisible = originalLayer.IsVisible,
                RenderData = originalLayer.RenderData?.Clone() ?? new InkRenderData(CanvasSize)
            };
            _allLayers.Add(layer);
            _layers.Add(layer);
            _layerStates[layer.Tag] = new LayerState {
                IsDeleted = false,
                IsVisible = layer.IsVisible,
                ZIndex = _allLayers.Count - 1
            };
            layer.PropertyChanged += OnLayerPropertyChanged;
            UpdateLayerState(layer);

            MainPage.Instance.UnReUtil.RecordCommand(
                new AddLayerCommand(this, layer)
            );
            return layer;
        }

        public void DeleteLayer(Guid layerId) {
            var layer = _allLayers.FirstOrDefault(x => x.Tag == layerId);
            if (layer == null || 
                !_layerStates.TryGetValue(layer.Tag, out var state) ||
                state.IsDeleted) return;

            layer.IsDeleted = true;
            _layers.Remove(layer);
            UpdateLayerState(layer);

            // 记录撤销命令
            MainPage.Instance.UnReUtil.RecordCommand(
                new DeleteLayerCommand(this, layer)
            );
        }

        public void MoveLayer(LayerInfo? layer, int oldIndex, int newIndex) {
            if (oldIndex == newIndex 
                || layer == null 
                || !_layerStates.TryGetValue(layer.Tag, out var state) 
                || state.IsDeleted)
                return;

            UpdateLayerState(layer, newIndex);

            MainPage.Instance.UnReUtil.RecordCommand(
                new MoveLayerCommand(this, layer, oldIndex, newIndex)
            );
        }

        private void SetLayerVisibility(LayerInfo layer, bool isVisible) {
            if (layer == null ||
                !_layerStates.TryGetValue(layer.Tag, out var state) || 
                state.IsDeleted ||
                state.IsVisible == isVisible)
                return;

            layer.IsVisible = isVisible;
            UpdateLayerState(layer);

            MainPage.Instance.UnReUtil.RecordCommand(
                new SetVisibilityCommand(this, layer, !isVisible, isVisible)
            );
        }

        public async Task SetLayerNameAsync(Guid layerId) {
            var layer = _allLayers.FirstOrDefault(x => x.Tag == layerId);
            if (layer == null) return;

            try {
                string oldName = layer.Name;
                var viewModel = new RenameViewModel(oldName);
                var dialogRes = await GlobalDialogUtils.ShowDialogAsync(
                    new RenameView(viewModel),
                    LanguageUtil.GetI18n(nameof(Constants.I18n.Dialog_Title_Rename)),
                    LanguageUtil.GetI18n(Constants.I18n.Text_Confirm),
                    LanguageUtil.GetI18n(Constants.I18n.Text_Cancel));
                if (dialogRes != DialogResult.Primary || !ComplianceUtil.IsValidValueOnlyLength(viewModel.NewName)) return;
                layer.Name = viewModel.NewName;

                MainPage.Instance.UnReUtil.RecordCommand(
                    new SetLayerNameCommand(this, layer, oldName, viewModel.NewName)
                );
            }
            finally {
                // 使得控件在对话框关闭后立即获得焦点
                GetFocus?.Invoke(this, EventArgs.Empty);
            }
        }

        #region utils
        private void Render(RenderMode mode, Rect region = default) {
            RenderRequest?.Invoke(this, new RenderTargetChangedEventArgs(mode, region));
        }

        private void OnLayerPropertyChanged(object? sender, PropertyChangedEventArgs e) {
            if (sender is not LayerInfo layer) return;

            // 仅监视影响渲染的状态属性
            if (e.PropertyName is nameof(LayerInfo.IsVisible)) {
                SetLayerVisibility(layer, layer.IsVisible);
            }
            else if (e.PropertyName is nameof(LayerInfo.IsDeleted)) {
                DeleteLayer(layer.Tag);
            }
        }

        private void RebuildActiveLayersCache() {
            _cachedActiveLayers.Clear();

            // 预分配合理容量
            _cachedActiveLayers.Capacity = Math.Max(_cachedActiveLayers.Capacity, _layers.Count);

            // 一次性获取所有数据，避免重复字典查找
            var activeLayers = _layers
                .Select(layer => (Layer: layer, State: _layerStates.GetValueOrDefault(layer.Tag)))
                .Where(item => !item.State.IsDeleted && item.State.IsVisible)
                .OrderBy(item => item.State.ZIndex)
                .Select(item => item.Layer);

            _cachedActiveLayers.AddRange(activeLayers);
            _isActiveLayersDirty = false;
        }

        private void MarkActiveLayersDirty() {
            _isActiveLayersDirty = true;
        }

        private void UpdateLayerState(LayerInfo layer, int newZIndex = -1) {
            ref var state = ref CollectionsMarshal.GetValueRefOrNullRef(_layerStates, layer.Tag);
            if (!Unsafe.IsNullRef(ref state)) {
                state.IsVisible = layer.IsVisible;
                state.IsDeleted = layer.IsDeleted;
                state.ZIndex = newZIndex >= 0 ? newZIndex : state.ZIndex;
                MarkActiveLayersDirty();
                Render(RenderMode.FullRegion);
            }
        }
        #endregion

        // 永久存储所有图层（只增不减）
        private readonly List<LayerInfo> _allLayers = [];
        // 渲染使用的图层（仅包含有效图层）
        private readonly List<LayerInfo> _cachedActiveLayers = [];
        // 绑定到 UI 列表
        private readonly ObservableCollection<LayerInfo> _layers = [];
        // 图层状态快照（用于脏检查）
        private readonly Dictionary<Guid, LayerState> _layerStates = [];
        private bool _isActiveLayersDirty = true;

        private struct LayerState {
            public bool IsVisible;
            public bool IsDeleted;
            public int ZIndex;
        }

        #region command
        record AddLayerCommand : LayerCommandBase {
            public AddLayerCommand(InkCanvasData canvas, LayerInfo layer) : base(canvas, layer) {
                Description = $"Add Layer '{layer.Name}'";
            }

            public override Task ExecuteAsync() {
                _layer.IsDeleted = false;
                Canvas.UpdateLayerState(_layer);
                return Task.CompletedTask;
            }

            public override Task UndoAsync() {
                _layer.IsDeleted = true;
                Canvas.UpdateLayerState(_layer);
                return Task.CompletedTask;
            }
        }

        record DeleteLayerCommand : LayerCommandBase {
            public DeleteLayerCommand(InkCanvasData canvas, LayerInfo layer) : base(canvas, layer) {
                Description = $"Delete Layer '{layer.Name}'";
            }

            public override Task ExecuteAsync() {
                _layer.IsDeleted = true;
                Canvas.UpdateLayerState(_layer);
                return Task.CompletedTask;
            }

            public override Task UndoAsync() {
                _layer.IsDeleted = false;
                Canvas.UpdateLayerState(_layer);
                return Task.CompletedTask;
            }
        }

        record MoveLayerCommand : LayerCommandBase {
            private readonly int _fromIndex;
            private readonly int _toIndex;

            public MoveLayerCommand(InkCanvasData canvas, LayerInfo layer, int fromIndex, int toIndex) : base(canvas, layer) {
                _fromIndex = fromIndex;
                _toIndex = toIndex;
                Description = $"Move Layer to position {toIndex}";
            }

            public override Task ExecuteAsync() {
                Canvas.UpdateLayerState(_layer, _toIndex);
                return Task.CompletedTask;
            }

            public override Task UndoAsync() {
                Canvas.UpdateLayerState(_layer, _fromIndex);
                return Task.CompletedTask;
            }
        }

        record SetVisibilityCommand : LayerCommandBase {
            private readonly bool _oldVisibility;
            private readonly bool _newVisibility;

            public SetVisibilityCommand(
                InkCanvasData canvas,
                LayerInfo layer,
                bool oldVisibility,
                bool newVisibility) : base(canvas, layer) {
                _oldVisibility = oldVisibility;
                _newVisibility = newVisibility;
                Description = $"{(newVisibility ? "Show" : "Hide")} Layer";
            }

            public override Task ExecuteAsync() {
                _layer.IsVisible = _newVisibility;
                Canvas.UpdateLayerState(_layer);
                return Task.CompletedTask;
            }

            public override Task UndoAsync() {
                _layer.IsVisible = _oldVisibility;
                Canvas.UpdateLayerState(_layer);
                return Task.CompletedTask;
            }
        }

        record SetLayerNameCommand : LayerCommandBase {
            private readonly string _oldName;
            private readonly string _newName;

            public SetLayerNameCommand(
                InkCanvasData canvas,
                LayerInfo layer,
                string oldName,
                string newName) : base(canvas, layer) {
                _oldName = oldName;
                _newName = newName;
            }

            public override Task ExecuteAsync() {
                _layer.Name = _newName;
                return Task.CompletedTask;
            }

            public override Task UndoAsync() {
                _layer.Name = _oldName;
                return Task.CompletedTask;
            }
        }
        #endregion
    }

    abstract record LayerCommandBase : IUndoableCommand {
        protected readonly InkCanvasData Canvas;
        public string Description { get; protected set; }

        protected LayerCommandBase(InkCanvasData canvas, LayerInfo layer, string desc = "") {
            Canvas = canvas;
            _layer = layer;
            Description = desc;
        }

        public abstract Task ExecuteAsync();
        public abstract Task UndoAsync();

        protected readonly LayerInfo _layer;
    }
}
