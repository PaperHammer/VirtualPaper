using System;
using System.Collections.Generic;
using System.Numerics;
using Microsoft.Graphics.Canvas;
using Microsoft.UI;
using Microsoft.UI.Input;
using VirtualPaper.Common.Extensions;
using Windows.Foundation;
using Windows.Graphics.Imaging;
using Workloads.Creation.StaticImg.Core.Brushes;
using Workloads.Creation.StaticImg.Core.UndoRedoCommand;
using Workloads.Creation.StaticImg.Core.Utils;
using Workloads.Creation.StaticImg.Debugging;
using Workloads.Creation.StaticImg.Events;

namespace Workloads.Creation.StaticImg.Core.Rendering {
    /// <summary>
    /// 2D 画布路径绘制器基类
    /// </summary>
    public abstract class CanvasPathDrawer : RenderBase {
        protected StrokeBase CurrentStroke { get; set; } = null!;
        protected CanvasRenderTarget TempRenderTarget { get; private set; } = null!;
        protected CanvasRenderTarget SnapshotRenderTarget { get; private set; } = null!;

        public override bool IsCanvasReady {
            get {
                if (!base.IsCanvasReady) return false;
                try {
                    if (TempRenderTarget == null || SnapshotRenderTarget == null || CurrentStroke == null) return false;
                    var test1 = TempRenderTarget.Device;
                    var test2 = SnapshotRenderTarget.Device;
                    return true;
                }
                catch {
                    return false;
                }
            }
        }

        protected void EnsurePathBuffersReady() {
            if (RenderTarget == null) return;

            var size = RenderTarget.Size;

            // 如果缓冲区不存在，或者尺寸/DPI与主画布不一致，则重新创建
            if (TempRenderTarget == null ||
                TempRenderTarget.Size != size ||
                TempRenderTarget.Dpi != RenderTarget.Dpi) {
                TempRenderTarget?.Dispose();
                TempRenderTarget = new CanvasRenderTarget(
                    RenderTarget.Device,
                    (float)size.Width,
                    (float)size.Height,
                    RenderTarget.Dpi,
                    RenderTarget.Format,
                    RenderTarget.AlphaMode);
            }

            if (SnapshotRenderTarget == null ||
                SnapshotRenderTarget.Size != size ||
                SnapshotRenderTarget.Dpi != RenderTarget.Dpi) {
                SnapshotRenderTarget?.Dispose();
                SnapshotRenderTarget = new CanvasRenderTarget(
                    RenderTarget.Device,
                    (float)size.Width,
                    (float)size.Height,
                    RenderTarget.Dpi,
                    RenderTarget.Format,
                    RenderTarget.AlphaMode);
            }
        }

        protected virtual void InitDrawState(Vector2 vec) {
            _isDrawing = true;
            _previousStrokeBounds = Rect.Empty;
            _strokeBounds = Rect.Empty;
            _useBlockCache = CanUseBlockCache(CurrentStroke);
            _isContinuationBlock = false;
            CurrentStroke.Points = [];
            CurrentStroke.Points.Add(vec);
            EnsurePathBuffersReady();
            ClearCommittedStrokeTiles();

            // 每条新笔画只在落笔时清空一次，避免上一条笔画的遮罩残留。
            using (var tempDs = TempRenderTarget.CreateDrawingSession())
                tempDs.Clear(Colors.Transparent);

            using var ds = SnapshotRenderTarget.CreateDrawingSession();
            ds.Clear(Colors.Transparent);
            ds.DrawImage(RenderTarget);
        }

        protected abstract void InitCurrentStroke(CanvasPointerEventArgs e);

        public override void HandlePressed(CanvasPointerEventArgs e) {
            if (e.PointerPos != PointerPosition.InsideCanvas) return;

            PointerPoint pointerPoint = e.Pointer;
            if (pointerPoint.Properties.IsMiddleButtonPressed) return;

            InitCurrentStroke(e);
            InitDrawState(pointerPoint.Position.ToVector2());
            RenderToTarget();
        }

        public override void HandleMoved(CanvasPointerEventArgs e) {
            if (!IsCanvasReady || !_isDrawing || e.PointerPos != PointerPosition.InsideCanvas) {
                EndDrawing();
                return;
            }

            if (!StrokePointSampler.AddOrUpdate(CurrentStroke.Points, e.Pointer.Position.ToVector2()))
                return;

            UpdateStrokeBounds(CurrentStroke.GetBounds());
            RenderToTarget();
        }

        protected void EndDrawing() {
            if (!_isDrawing) return;

            _isDrawing = false;
            CaptureUndoRedoSnapshot();
            base.RequestOnceRender();
        }

        public override void HandleReleased(CanvasPointerEventArgs e) {
            EndDrawing();
        }

        public override void HandleExited(CanvasPointerEventArgs e) {
            base.HandleExited(e);
            EndDrawing();
        }

        protected void RenderToTarget() {
            if (!IsCanvasReady || !CurrentStroke.ShouldRender) return;

            try {
                _updatedStrokeTiles.Clear();
                // GPU 块提交必须位于设备丢失/资源释放异常保护范围内。
                CommitStableBlockIfNeeded();
                using var geometry = CurrentStroke.CreateStrokeGeometry(
                    RenderTarget!.Device,
                    CurrentStroke.Points,
                    _isContinuationBlock,
                    true);
                var bounds = CurrentStroke.GetBounds();
                var dirtyBounds = CombineBounds(_previousStrokeBounds, bounds);
                UpdateStrokeBounds(bounds);

                // *** TempRenderTarget 重用与增量绘制 ***
                using (var dsTemp = TempRenderTarget.CreateDrawingSession()) {
                    // 同时清理上一帧范围，覆盖采样器替换末端点后边界轻微收缩的情况。
                    dsTemp.Blend = CanvasBlend.Copy;
                    dsTemp.FillRectangle(dirtyBounds, Colors.Transparent);
                    CurrentStroke.RenderIncrement(dsTemp, geometry);
                }

                using var combinedForeground = _useBlockCache
                    ? CreateCombinedForeground(dirtyBounds)
                    : null;
                ICanvasImage foreground = combinedForeground is null
                    ? TempRenderTarget
                    : combinedForeground;

                // *** 合成并绘制到 RenderTarget ***
                using (var mergedImage = CurrentStroke.MergeImages(
                    foreground,
                    SnapshotRenderTarget,
                    RenderTarget!.Device
                )) {
                    // 将合成结果写入 RenderTarget
                    using (var dsTarget = RenderTarget.CreateDrawingSession()) {
                        // 因为 MergeImages 返回的 Effect 已经包含了 SnapshotRT 的内容，
                        // 所以用 Copy 模式替换 RenderTarget 的内容。
                        dsTarget.Blend = CanvasBlend.Copy;
                        // 效果链以完整画布坐标计算。通过裁剪层限制写回区域，不能把
                        // dirtyBounds 同时作为效果源矩形，否则部分效果会丢失背景源。
                        using var clipLayer = dsTarget.CreateLayer(1f, dirtyBounds);
                        dsTarget.DrawImage(mergedImage);
                    }
                }

                _previousStrokeBounds = bounds;
                HandleRender(new RenderTargetChangedEventArgs(
                    RenderMode.PartialRegion,
                    dirtyBounds,
                    CreateStrokeTileDebugInfo(dirtyBounds, bounds)));
            }
            catch (Exception ex) when (IsDeviceLost(ex)) {
                HandleDeviceLost();
            }
            catch (ObjectDisposedException) {
                // 渲染目标被意外清理，安全退出本次渲染
                EndDrawing();
            }
            catch (Exception ex) {
                ReportFatalError(ex);
                EndDrawing();
            }
        }

        private void CommitStableBlockIfNeeded() {
            if (!_useBlockCache || CurrentStroke.Points.Count < ActiveBlockPointLimit) return;

            List<Vector2> committedPoints = CurrentStroke.Points.GetRange(0, CommitPointCount);
            using var geometry = CurrentStroke.CreateStrokeGeometry(
                RenderTarget.Device,
                committedPoints,
                _isContinuationBlock,
                false);
            foreach (var tileKey in EnumerateStrokeTileKeys(
                committedPoints,
                _isContinuationBlock,
                false)) {
                CanvasRenderTarget tile = GetOrCreateCommittedStrokeTile(tileKey);
                if (StaticImgDebugSwitches.ShowStrokeTileOverlay)
                    _updatedStrokeTiles.Add(tileKey);
                using var ds = tile.CreateDrawingSession();
                ds.Units = CanvasUnits.Pixels;
                ds.Transform = Matrix3x2.CreateTranslation(
                    -tileKey.Column * StrokeTileSize,
                    -tileKey.Row * StrokeTileSize);
                CurrentStroke.RenderIncrement(ds, geometry);
            }

            CurrentStroke.Points.RemoveRange(0, CommitPointCount - BlockOverlapPointCount);
            _isContinuationBlock = true;
        }

        private StrokeTileDebugInfo? CreateStrokeTileDebugInfo(Rect dirtyBounds, Rect activeBounds) {
            if (!StaticImgDebugSwitches.ShowStrokeTileOverlay) return null;

            var allocatedTiles = new List<Rect>(_committedStrokeTiles.Count);
            foreach (var tileKey in _committedStrokeTiles.Keys)
                allocatedTiles.Add(GetTileBounds(tileKey));

            var updatedTiles = new List<Rect>(_updatedStrokeTiles.Count);
            foreach (var tileKey in _updatedStrokeTiles)
                updatedTiles.Add(GetTileBounds(tileKey));

            return new StrokeTileDebugInfo(
                dirtyBounds,
                activeBounds,
                allocatedTiles,
                updatedTiles,
                CurrentStroke.Points.Count);
        }

        private Rect GetTileBounds((int Column, int Row) key) {
            var canvasSize = RenderTarget.SizeInPixels;
            int left = key.Column * StrokeTileSize;
            int top = key.Row * StrokeTileSize;
            int width = Math.Min(StrokeTileSize, (int)canvasSize.Width - left);
            int height = Math.Min(StrokeTileSize, (int)canvasSize.Height - top);
            return new Rect(left, top, width, height);
        }

        private CanvasCommandList CreateCombinedForeground(Rect dirtyBounds) {
            var commandList = new CanvasCommandList(RenderTarget.Device);
            try {
                using var ds = commandList.CreateDrawingSession();
                ds.Units = CanvasUnits.Pixels;
                foreach (var tileKey in EnumerateTileKeys(dirtyBounds)) {
                    if (!_committedStrokeTiles.TryGetValue(tileKey, out var tile)) continue;
                    ds.DrawImage(
                        tile,
                        tileKey.Column * StrokeTileSize,
                        tileKey.Row * StrokeTileSize);
                }
                ds.DrawImage(TempRenderTarget);
                return commandList;
            }
            catch {
                commandList.Dispose();
                throw;
            }
        }

        private CanvasRenderTarget GetOrCreateCommittedStrokeTile((int Column, int Row) key) {
            if (_committedStrokeTiles.TryGetValue(key, out var tile)) return tile;

            var canvasSize = RenderTarget.SizeInPixels;
            int originX = key.Column * StrokeTileSize;
            int originY = key.Row * StrokeTileSize;
            int widthInPixels = Math.Min(StrokeTileSize, (int)canvasSize.Width - originX);
            int heightInPixels = Math.Min(StrokeTileSize, (int)canvasSize.Height - originY);
            float dipScale = 96f / RenderTarget.Dpi;
            tile = new CanvasRenderTarget(
                RenderTarget.Device,
                widthInPixels * dipScale,
                heightInPixels * dipScale,
                RenderTarget.Dpi,
                RenderTarget.Format,
                RenderTarget.AlphaMode);
            try {
                using (var ds = tile.CreateDrawingSession())
                    ds.Clear(Colors.Transparent);
                _committedStrokeTiles.Add(key, tile);
                return tile;
            }
            catch {
                tile.Dispose();
                throw;
            }
        }

        private IEnumerable<(int Column, int Row)> EnumerateStrokeTileKeys(
            List<Vector2> points,
            bool startsAtLeadingMidpoint,
            bool includesFinalSegment) {
            var keys = new HashSet<(int Column, int Row)>();
            float margin = CurrentStroke.BrushArgs.Thickness / 2f + StrokeBoundsPadding;
            Vector2 segmentStart = startsAtLeadingMidpoint
                ? (points[0] + points[1]) / 2
                : points[0];

            for (int i = 1; i < points.Count - 1; i++) {
                Vector2 segmentEnd = (points[i] + points[i + 1]) / 2;
                AddQuadraticTileKeys(keys, segmentStart, points[i], segmentEnd, margin, 0);
                segmentStart = segmentEnd;
            }

            if (includesFinalSegment)
                AddLineTileKeys(keys, segmentStart, points[^1], margin);
            return keys;
        }

        private void AddQuadraticTileKeys(
            HashSet<(int Column, int Row)> keys,
            Vector2 start,
            Vector2 control,
            Vector2 end,
            float margin,
            int depth) {
            float controlPolygonLength = Vector2.Distance(start, control) +
                Vector2.Distance(control, end);
            if (controlPolygonLength <= TileCurveSubdivisionLength ||
                depth >= MaxTileCurveSubdivisionDepth) {
                AddBoundsTileKeys(
                    keys,
                    MathF.Min(start.X, MathF.Min(control.X, end.X)) - margin,
                    MathF.Min(start.Y, MathF.Min(control.Y, end.Y)) - margin,
                    MathF.Max(start.X, MathF.Max(control.X, end.X)) + margin,
                    MathF.Max(start.Y, MathF.Max(control.Y, end.Y)) + margin);
                return;
            }

            Vector2 startControl = (start + control) / 2;
            Vector2 controlEnd = (control + end) / 2;
            Vector2 midpoint = (startControl + controlEnd) / 2;
            AddQuadraticTileKeys(keys, start, startControl, midpoint, margin, depth + 1);
            AddQuadraticTileKeys(keys, midpoint, controlEnd, end, margin, depth + 1);
        }

        private void AddLineTileKeys(
            HashSet<(int Column, int Row)> keys,
            Vector2 start,
            Vector2 end,
            float margin) {
            float length = Vector2.Distance(start, end);
            int segmentCount = Math.Max(1, (int)Math.Ceiling(length / TileCurveSubdivisionLength));
            Vector2 previous = start;
            for (int i = 1; i <= segmentCount; i++) {
                Vector2 current = Vector2.Lerp(start, end, i / (float)segmentCount);
                AddBoundsTileKeys(
                    keys,
                    MathF.Min(previous.X, current.X) - margin,
                    MathF.Min(previous.Y, current.Y) - margin,
                    MathF.Max(previous.X, current.X) + margin,
                    MathF.Max(previous.Y, current.Y) + margin);
                previous = current;
            }
        }

        private void AddBoundsTileKeys(
            HashSet<(int Column, int Row)> keys,
            float left,
            float top,
            float right,
            float bottom) {
            foreach (var key in EnumerateTileKeys(new Rect(left, top, right - left, bottom - top)))
                keys.Add(key);
        }

        private IEnumerable<(int Column, int Row)> EnumerateTileKeys(Rect bounds) {
            var canvasSize = RenderTarget.SizeInPixels;
            int columnCount = ((int)canvasSize.Width + StrokeTileSize - 1) / StrokeTileSize;
            int rowCount = ((int)canvasSize.Height + StrokeTileSize - 1) / StrokeTileSize;
            int firstColumn = Math.Max(0, (int)Math.Floor(bounds.Left / StrokeTileSize));
            int firstRow = Math.Max(0, (int)Math.Floor(bounds.Top / StrokeTileSize));
            int lastColumn = Math.Min(columnCount - 1, (int)Math.Ceiling(bounds.Right / StrokeTileSize) - 1);
            int lastRow = Math.Min(rowCount - 1, (int)Math.Ceiling(bounds.Bottom / StrokeTileSize) - 1);

            for (int row = firstRow; row <= lastRow; row++)
                for (int column = firstColumn; column <= lastColumn; column++)
                    yield return (column, row);
        }

        private void ClearCommittedStrokeTiles() {
            foreach (CanvasRenderTarget tile in _committedStrokeTiles.Values)
                tile.Dispose();
            _committedStrokeTiles.Clear();
            _updatedStrokeTiles.Clear();
        }

        private static bool CanUseBlockCache(StrokeBase stroke) {
            if (stroke.IsEraser) return true;

            return stroke.BrushArgs.Type == BrushType.General &&
                stroke.BrushArgs.Opacity >= 0.999f &&
                stroke.BrushArgs.BrushColor.A == byte.MaxValue;
        }

        private void UpdateStrokeBounds(Rect bounds) {
            _strokeBounds = CombineBounds(_strokeBounds, bounds);
        }

        private static Rect CombineBounds(Rect previous, Rect current) {
            if (previous == Rect.Empty) return current;

            double left = Math.Min(previous.Left, current.Left);
            double top = Math.Min(previous.Top, current.Top);
            double right = Math.Max(previous.Right, current.Right);
            double bottom = Math.Max(previous.Bottom, current.Bottom);
            return new Rect(left, top, right - left, bottom - top);
        }

        private void CaptureUndoRedoSnapshot() {
            if (CurrentStroke == null) return;

            var rawBounds = _strokeBounds == Rect.Empty ? CurrentStroke.GetBounds() : _strokeBounds;
            var dirtyRect = EnlargeToIntegerBounds(rawBounds, RenderTarget.SizeInPixels);
            int x = (int)dirtyRect.Left;
            int y = (int)dirtyRect.Top;
            int w = (int)dirtyRect.Width;
            int h = (int)dirtyRect.Height;

            if (w <= 0 || h <= 0) return;

            byte[] originalPixels = SnapshotRenderTarget.GetPixelBytes(x, y, w, h).CompressPixels();
            byte[] currentPixels = RenderTarget.GetPixelBytes(x, y, w, h).CompressPixels();
            var command = new RegionPixelSnapshotCommand(
                LayerId,
                ViewModel.Data,
                dirtyRect,
                originalPixels,
                currentPixels,
                true,
                "Path Drawer",
                (region) => HandleRender(new RenderTargetChangedEventArgs(RenderMode.PartialRegion, region))
            );

            ViewModel.Session.UnReUtil.RecordCommand(command);
        }

        private static Rect EnlargeToIntegerBounds(Rect rect, BitmapSize maxBounds) {
            int left = (int)Math.Floor(rect.Left);
            int top = (int)Math.Floor(rect.Top);
            int right = (int)Math.Ceiling(rect.Right);
            int bottom = (int)Math.Ceiling(rect.Bottom);

            left = Math.Max(0, left);
            top = Math.Max(0, top);
            right = Math.Min((int)maxBounds.Width, right);
            bottom = Math.Min((int)maxBounds.Height, bottom);

            return new Rect(left, top, right - left, bottom - top);
        }

        public override void Dispose() {
            base.Dispose();
            TempRenderTarget?.Dispose();
            SnapshotRenderTarget?.Dispose();
            ClearCommittedStrokeTiles();
            GC.SuppressFinalize(this);
        }

        private bool _isDrawing = false;
        private bool _useBlockCache;
        private bool _isContinuationBlock;
        private Rect _previousStrokeBounds = Rect.Empty;
        private readonly Dictionary<(int Column, int Row), CanvasRenderTarget> _committedStrokeTiles = [];
        private readonly HashSet<(int Column, int Row)> _updatedStrokeTiles = [];
        private Rect _strokeBounds = Rect.Empty;
        private const int ActiveBlockPointLimit = 34;
        private const int CommitPointCount = 32;
        private const int BlockOverlapPointCount = 2;
        private const int StrokeTileSize = 256;
        private const float StrokeBoundsPadding = 5f;
        private const float TileCurveSubdivisionLength = StrokeTileSize / 2f;
        private const int MaxTileCurveSubdivisionDepth = 16;
    }
}
