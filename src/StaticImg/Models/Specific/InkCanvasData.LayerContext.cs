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
        public List<LayerInfo> AllLayers => _allLayers;

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

        internal async Task<LayerInfo> AddLayerWithDataAsync(string filePath, string? name = null, bool isBackground = false, Guid? layerId = null) {
            throw new NotImplementedException();
            //int insertIndex = _layers.Count;

            //if (layerId.HasValue) {
            //    var targetLayer = _layers.FirstOrDefault(x => x.Tag == layerId.Value);
            //    if (targetLayer != null) {
            //        insertIndex = _layers.IndexOf(targetLayer) + 1;
            //    }
            //}

            //var layer = new LayerInfo {
            //    Name = name ??
            //        $"{LanguageUtil.GetI18n(nameof(Constants.I18n.Project_StaticImg_Text_LayerName))} " +
            //        $"{LanguageUtil.GetI18n(nameof(Constants.I18n.Project_StaticImg_Text_LayerNew))} " +
            //        $"{++_newLayerCount}",
            //    RenderData = new InkRenderData(_session, CanvasSize, isBackground),
            //    IsDeleted = false,
            //    IsVisible = true,
            //    ZIndex = insertIndex,
            //};
            //layer.RenderData.IsReady.SetResult(true);
            //layer.PropertyChanged += OnLayerPropertyChanged;

            //_allLayers.Add(layer);
            //if (insertIndex < _layers.Count) {
            //    _layers.Insert(insertIndex, layer);
            //    RefreshZIndices();
            //}
            //else {
            //    _layers.Add(layer);
            //}
            //OnAnyLayerStateChanged();

            //_session.UnReUtil.RecordCommand(
            //    new AddLayerCommand(this, layer, insertIndex)
            //);
            //return layer;
        }

        public LayerInfo AddLayer(string? name = null, bool isBackground = false, Guid? layerId = null, bool needRecord = true) {
            int insertIndex = _layers.Count;

            if (layerId.HasValue) {
                var targetLayer = _layers.FirstOrDefault(x => x.Tag == layerId.Value);
                if (targetLayer != null) {
                    insertIndex = _layers.IndexOf(targetLayer) + 1;
                }
            }

            var layer = new LayerInfo {
                Name = name ??
                    $"{LanguageUtil.GetI18n(nameof(Constants.I18n.Project_StaticImg_Text_LayerName))} " +
                    $"{LanguageUtil.GetI18n(nameof(Constants.I18n.Project_StaticImg_Text_LayerNew))} " +
                    $"{++_newLayerCount}",
                RenderData = new InkRenderData(_session, CanvasSize, isBackground),
                IsDeleted = false,
                IsVisible = true,
                ZIndex = insertIndex,
            };
            layer.RenderData.IsReady.SetResult(true);
            layer.PropertyChanged += OnLayerPropertyChanged;

            _allLayers.Add(layer);
            if (insertIndex < _layers.Count) {
                _layers.Insert(insertIndex, layer);
                RefreshZIndices();
            }
            else {
                _layers.Add(layer);
            }
            OnAnyLayerStateChanged();

            if (needRecord) {
                _session.UnReUtil.RecordCommand(
                    new AddLayerCommand(this, layer, insertIndex)
                );
            }
            return layer;
        }

        public LayerInfo? CopyLayer(Guid layerId) {
            var originalLayer = _allLayers.FirstOrDefault(x => x.Tag == layerId);
            if (originalLayer == null || originalLayer.RenderData == null) return null;

            // 计算插入位置：原图层的正上方 (Index + 1)
            int insertIndex = _layers.IndexOf(originalLayer) + 1;
            var layer = new LayerInfo {
                Name = $"{originalLayer.Name} {LanguageUtil.GetI18n(nameof(Constants.I18n.Project_StaticImg_Text_LayerCopy))} {++_copyLayerCount}",
                IsVisible = originalLayer.IsVisible,
                RenderData = originalLayer.RenderData.Clone(),
                ZIndex = insertIndex,
            };
            layer.RenderData.IsReady.SetResult(true);
            layer.PropertyChanged += OnLayerPropertyChanged;
            _allLayers.Add(layer);
            if (insertIndex < _layers.Count) {
                _layers.Insert(insertIndex, layer);
            }
            else {
                _layers.Add(layer);
            }
            OnAnyLayerStateChanged();

            _session.UnReUtil.RecordCommand(
                new AddLayerCommand(this, layer, layer.ZIndex)
            );
            return layer;
        }

        public void DeleteLayer(Guid layerId) {
            var layer = _allLayers.FirstOrDefault(x => x.Tag == layerId && !x.IsDeleted);
            if (layer == null) return;

            int originalIndex = _layers.IndexOf(layer);
            layer.IsDeleted = true;
            layer.PropertyChanged -= OnLayerPropertyChanged;
            _layers.Remove(layer);
            OnAnyLayerStateChanged();

            _session.UnReUtil.RecordCommand(
                new DeleteLayerCommand(this, layer, originalIndex) // 传入原本的索引位置，以便撤销时插回原位
            );
        }

        public void MoveLayer(LayerInfo? layer, int oldIndex, int newIndex) {
            if (layer == null || layer.IsDeleted) return;
            if (oldIndex == newIndex) return;
            if (oldIndex < 0 || oldIndex >= _layers.Count) return;
            if (newIndex < 0 || newIndex >= _layers.Count) return;

            RefreshZIndices();
            OnAnyLayerStateChanged();

            _session.UnReUtil.RecordCommand(
                new MoveLayerCommand(this, layer, oldIndex, newIndex)
            );
        }

        private void OnLayerVisibilityChanged(LayerInfo layer, bool isVisible) {
            if (layer == null || layer.IsDeleted) return;
            if (_session.UnReUtil.IsUndoingOrRedoing) return;

            OnAnyLayerStateChanged();

            _session.UnReUtil.RecordCommand(
                new SetVisibilityCommand(this, layer, !isVisible, isVisible)
            );
        }

        public async Task SetLayerNameAsync(Guid layerId) {
            var layer = _allLayers.FirstOrDefault(x => x.Tag == layerId && !x.IsDeleted);
            if (layer == null) return;

            try {
                string oldName = layer.Name;
                var viewModel = new RenameViewModel(oldName);
                var dialogRes = await GlobalDialogUtils.ShowDialogAsync(
                    new RenameView(viewModel),
                    LanguageUtil.GetI18n(nameof(Constants.I18n.Dialog_Title_Rename)),
                    LanguageUtil.GetI18n(Constants.I18n.Text_Confirm),
                    LanguageUtil.GetI18n(Constants.I18n.Text_Cancel));
                if (dialogRes != DialogResult.Primary
                    || !ComplianceUtil.IsValidValueOnlyLength(viewModel.NewName)
                    || string.Equals(oldName, viewModel.NewName)) {
                    return;
                }

                layer.Name = viewModel.NewName!;

                _session.UnReUtil.RecordCommand(
                    new SetLayerNameCommand(this, layer, oldName, viewModel.NewName!)
                );
            }
            finally {
                // 使得控件在对话框关闭后立即获得焦点
                GetFocus?.Invoke(this, EventArgs.Empty);
            }
        }

        #region utils
        private void RefreshZIndices() {
            for (int i = 0; i < _layers.Count; i++) {
                _layers[i].ZIndex = i;
            }
        }

        private void Render(RenderMode mode, Rect region = default) {
            RenderRequest?.Invoke(this, new RenderTargetChangedEventArgs(mode, region));
        }

        internal void OnLayerPropertyChanged(object? sender, PropertyChangedEventArgs e) {
            // 如果正在执行 Undo/Redo，不要记录新命令
            if (_session.UnReUtil.IsUndoingOrRedoing || sender is not LayerInfo layer) return;

            // 仅监视影响渲染的状态属性
            if (e.PropertyName is nameof(LayerInfo.IsVisible)) {
                OnLayerVisibilityChanged(layer, layer.IsVisible);
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
                .Where(layer => !layer.IsDeleted && layer.IsVisible && layer.RenderData?.RenderTarget != null)
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
        private int _newLayerCount;
        private int _copyLayerCount;

        #region command        
        record AddLayerCommand : LayerCommandBase {
            public AddLayerCommand(InkCanvasData canvas, LayerInfo layer, int originalIndex) : base(canvas, layer) {
                Description = $"Add Layer '{layer.Name}'";
                _originalIndex = originalIndex;
            }

            public override Task ExecuteAsync() {
                Layer.IsDeleted = false;
                Layer.PropertyChanged += Canvas.OnLayerPropertyChanged;
                if (!Canvas.Layers.Contains(Layer)) {
                    if (_originalIndex >= 0 && _originalIndex < Canvas.Layers.Count) {
                        Canvas.Layers.Insert(_originalIndex, Layer);
                    }
                    else {
                        Canvas.Layers.Add(Layer);
                    }
                }
                Canvas.OnAnyLayerStateChanged();
                return Task.CompletedTask;
            }

            public override Task UndoAsync() {
                Layer.IsDeleted = true;
                Layer.PropertyChanged -= Canvas.OnLayerPropertyChanged;
                Canvas.Layers.Remove(Layer);
                Canvas.OnAnyLayerStateChanged();
                return Task.CompletedTask;
            }

            private readonly int _originalIndex;
        }

        record DeleteLayerCommand : LayerCommandBase {
            public DeleteLayerCommand(InkCanvasData canvas, LayerInfo layer, int originalIndex) : base(canvas, layer) {
                Description = $"Delete Layer '{layer.Name}'";
                _originalIndex = originalIndex;
            }

            public override Task ExecuteAsync() {
                Layer.IsDeleted = true;
                Layer.PropertyChanged -= Canvas.OnLayerPropertyChanged;
                Canvas.Layers.Remove(Layer);
                Canvas.OnAnyLayerStateChanged();
                return Task.CompletedTask;
            }

            public override Task UndoAsync() {
                Layer.IsDeleted = false;
                Layer.PropertyChanged += Canvas.OnLayerPropertyChanged;
                if (!Canvas.Layers.Contains(Layer)) {
                    if (_originalIndex >= 0 && _originalIndex <= Canvas.Layers.Count) {
                        Canvas.Layers.Insert(_originalIndex, Layer);
                    }
                    else {
                        Canvas.Layers.Add(Layer);
                    }
                }
                Canvas.OnAnyLayerStateChanged();
                return Task.CompletedTask;
            }

            private readonly int _originalIndex;
        }

        record MoveLayerCommand : LayerCommandBase {
            public MoveLayerCommand(InkCanvasData canvas, LayerInfo layer, int oldIndex, int newIndex) : base(canvas, layer) {
                Description = $"Move Layer \'{layer.Name}\' from {_oldIndex} to {_newIndex}";
                _oldIndex = oldIndex;
                _newIndex = newIndex;
            }

            public override Task ExecuteAsync() {
                if (IsIndexValid(_oldIndex) && IsIndexValid(_newIndex)) {
                    Canvas.Layers.Move(_oldIndex, _newIndex);
                    Canvas.RefreshZIndices();
                    Canvas.OnAnyLayerStateChanged();
                }
                return Task.CompletedTask;
            }

            public override Task UndoAsync() {
                if (IsIndexValid(_oldIndex) && IsIndexValid(_newIndex)) {
                    Canvas.Layers.Move(_newIndex, _oldIndex);
                    Canvas.RefreshZIndices();
                    Canvas.OnAnyLayerStateChanged();
                }
                return Task.CompletedTask;
            }

            private bool IsIndexValid(int index) {
                return index >= 0 && index < Canvas.Layers.Count;
            }

            private readonly int _oldIndex;
            private readonly int _newIndex;
        }

        record SetVisibilityCommand : LayerCommandBase {
            private readonly bool _oldVisibility;
            private readonly bool _newVisibility;

            public SetVisibilityCommand(InkCanvasData canvas, LayerInfo layer, bool oldVisibility, bool newVisibility)
                : base(canvas, layer) {
                _oldVisibility = oldVisibility;
                _newVisibility = newVisibility;
                Description = $"{(newVisibility ? "Show" : "Hide")} Layer";
            }

            public override Task ExecuteAsync() {
                if (Layer.IsVisible != _newVisibility) {
                    Layer.IsVisible = _newVisibility;
                    Canvas.OnAnyLayerStateChanged();
                }
                return Task.CompletedTask;
            }

            public override Task UndoAsync() {
                if (Layer.IsVisible != _oldVisibility) {
                    Layer.IsVisible = _oldVisibility;
                    Canvas.OnAnyLayerStateChanged();
                }
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
                Layer.Name = _newName;
                return Task.CompletedTask;
            }

            public override Task UndoAsync() {
                Layer.Name = _oldName;
                return Task.CompletedTask;
            }
        }
        #endregion
    }

    abstract record LayerCommandBase : IUndoableCommand {
        protected InkCanvasData Canvas { get; }
        protected LayerInfo Layer { get; }
        public string Description { get; protected set; }

        protected LayerCommandBase(InkCanvasData canvas, LayerInfo layer, string desc = "") {
            Canvas = canvas;
            Layer = layer;
            Description = desc;
        }

        public abstract Task ExecuteAsync();
        public abstract Task UndoAsync();
    }
}
