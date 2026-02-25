using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using VirtualPaper.Common;
using VirtualPaper.Common.Utils;
using VirtualPaper.Common.Utils.UndoRedo;
using VirtualPaper.Models.Mvvm;
using VirtualPaper.UIComponent.Others;
using VirtualPaper.UIComponent.Utils;
using VirtualPaper.UIComponent.ViewModels;
using Windows.Foundation;
using Workloads.Creation.StaticImg.Events;

namespace Workloads.Creation.StaticImg.Models.Specific {
    // LayerContext part of InkCanvasData
    public partial class InkCanvasData : ObservableObject {
        public event EventHandler<RenderTargetChangedEventArgs>? RenderRequest;
        public event EventHandler? SeletcedLayerChanged;
        public event EventHandler? GetFocus;

        public IReadOnlyList<LayerInfo> ActiveLayers {
            get {
                RebuildActiveLayers();
                return _activeLayers;
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
                Name = name ?? $"Layer New {_allLayers.Count + 1}",
                RenderData = new InkRenderData(_session, CanvasSize, isBackground),
                IsDeleted = false,
                IsVisible = true,
                ZIndex = _allLayers.Count - 1
            };
            layer.RenderData.IsReady.SetResult(true);
            layer.PropertyChanged += OnLayerPropertyChanged;
            _allLayers.Add(layer);
            _layers.Add(layer);
            OnAnyLayerStateChanged();

            _session.UnReUtil.RecordCommand(
                new AddLayerCommand(this, layer)
            );
            return layer;
        }

        public LayerInfo? CopyLayer(Guid layerId) {
            var originalLayer = _allLayers.FirstOrDefault(x => x.Tag == layerId);
            if (originalLayer == null || originalLayer.RenderData == null) return null;

            var layer = new LayerInfo {
                Name = $"{originalLayer.Name} Copy {_allLayers.Count + 1}",
                IsVisible = originalLayer.IsVisible,
                RenderData = originalLayer.RenderData.Clone(),
            };
            layer.RenderData.IsReady.SetResult(true);

            layer.PropertyChanged += OnLayerPropertyChanged;
            _allLayers.Add(layer);
            _layers.Add(layer);
            OnAnyLayerStateChanged();

            _session.UnReUtil.RecordCommand(
                new AddLayerCommand(this, layer)
            );
            return layer;
        }

        public void DeleteLayer(Guid layerId) {
            var layer = _allLayers.FirstOrDefault(x => x.Tag == layerId);
            if (layer == null ||
                layer.IsDeleted) return;

            layer.IsDeleted = true;
            layer.PropertyChanged -= OnLayerPropertyChanged;
            _layers.Remove(layer);
            OnAnyLayerStateChanged();

            // 记录撤销命令
            _session.UnReUtil.RecordCommand(
                new DeleteLayerCommand(this, layer)
            );
        }

        public void MoveLayer(LayerInfo? layer, int oldIndex, int newIndex) {
            if (oldIndex == newIndex
                || layer == null
                || layer.IsDeleted)
                return;

            layer.ZIndex = newIndex;
            OnAnyLayerStateChanged();

            _session.UnReUtil.RecordCommand(
                new MoveLayerCommand(this, layer, oldIndex, newIndex)
            );
        }

        private void SetLayerVisibility(LayerInfo layer, bool isVisible) {
            if (layer == null ||
                layer.IsDeleted)
                return;

            OnAnyLayerStateChanged();

            _session.UnReUtil.RecordCommand(
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

                _session.UnReUtil.RecordCommand(
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
        }

        private void OnAnyLayerStateChanged() {
            MarkActiveLayersDirty();
            Render(RenderMode.FullRegion);
        }

        private void RebuildActiveLayers() {
            if (Interlocked.Exchange(ref _dirtyStatus, 1) == 0) return;

            _activeLayers.Clear();
            _activeLayers.Capacity = Math.Max(_activeLayers.Capacity, _layers.Count);
            var activeLayers = _layers
                .Where(layer => !layer.IsDeleted && layer.IsVisible)
                .OrderBy(layer => layer.ZIndex)
                .Select(layer => layer);

            _activeLayers.AddRange(activeLayers);
            _dirtyStatus = 0;
        }

        private void MarkActiveLayersDirty() {
            _dirtyStatus = 1;
        }
        #endregion

        // 永久存储所有图层（只增不减），方便 undo/redo
        private readonly List<LayerInfo> _allLayers = [];
        // 渲染使用的图层（仅包含有效图层）
        private readonly List<LayerInfo> _activeLayers = [];
        // 绑定到 UI 列表
        private readonly ObservableCollection<LayerInfo> _layers = [];
        private volatile int _dirtyStatus = 1; // 1 为脏，0 为干净

        #region command
        record AddLayerCommand : LayerCommandBase {
            public AddLayerCommand(InkCanvasData canvas, LayerInfo layer) : base(canvas, layer) {
                Description = $"Add Layer '{layer.Name}'";
            }

            public override Task ExecuteAsync() {
                _layer.IsDeleted = false;
                Canvas.OnAnyLayerStateChanged();
                return Task.CompletedTask;
            }

            public override Task UndoAsync() {
                _layer.IsDeleted = true;
                Canvas.OnAnyLayerStateChanged();
                return Task.CompletedTask;
            }
        }

        record DeleteLayerCommand : LayerCommandBase {
            public DeleteLayerCommand(InkCanvasData canvas, LayerInfo layer) : base(canvas, layer) {
                Description = $"Delete Layer '{layer.Name}'";
            }

            public override Task ExecuteAsync() {
                _layer.IsDeleted = true;
                Canvas.OnAnyLayerStateChanged();
                return Task.CompletedTask;
            }

            public override Task UndoAsync() {
                _layer.IsDeleted = false;
                Canvas.OnAnyLayerStateChanged();
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
                _layer.ZIndex = _toIndex;
                Canvas.OnAnyLayerStateChanged();
                return Task.CompletedTask;
            }

            public override Task UndoAsync() {
                _layer.ZIndex = _fromIndex;
                Canvas.OnAnyLayerStateChanged();
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
                Canvas.OnAnyLayerStateChanged();
                return Task.CompletedTask;
            }

            public override Task UndoAsync() {
                _layer.IsVisible = _oldVisibility;
                Canvas.OnAnyLayerStateChanged();
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
