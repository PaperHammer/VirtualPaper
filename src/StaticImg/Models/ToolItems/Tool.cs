using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using Microsoft.Graphics.Canvas;
using Microsoft.UI;
using Microsoft.UI.Input;
using VirtualPaper.Common.Extensions;
using VirtualPaper.UIComponent.Services;
using Windows.Foundation;
using Windows.UI;
using Workloads.Creation.StaticImg.Models.EventArg;
using Workloads.Creation.StaticImg.Models.ToolItems.Base;

namespace Workloads.Creation.StaticImg.Models.ToolItems {
    public abstract class Tool : ICursorService, IDisposable {
        public event EventHandler<CursorChangedEventArgs>? SystemCursorChangeRequested;
        public event EventHandler<RenderTargetChangedEventArgs>? RenderRequest;

        protected Rect Viewport { get; private set; } = Rect.Empty;
        protected virtual bool HandlesPointerOutsideContentArea => false;
        protected virtual CanvasRenderTarget? RenderTarget {
            get => _renderTarget;
            set {
                if (_renderTarget == value) return;

                _renderTarget = value;
                UpdateRelatedVariables();
            }
        }

        public virtual void OnPointerEntered(CanvasPointerEventArgs e) {
            RenderTarget = e.RenderData.RenderTarget;
            SystemCursorChangeRequested?.Invoke(this, new(InputSystemCursor.Create(InputSystemCursorShape.Cross)));
        }
        public virtual void OnPointerPressed(CanvasPointerEventArgs e) { }
        public virtual void OnPointerMoved(CanvasPointerEventArgs e) { }
        public virtual void OnPointerReleased(CanvasPointerEventArgs e) { }
        public virtual void OnPointerExited(CanvasPointerEventArgs e) {
            SystemCursorChangeRequested?.Invoke(this, new CursorChangedEventArgs(null));
        }

        protected void ChangeCursor(InputSystemCursor cursor) {
            SystemCursorChangeRequested?.Invoke(this, new CursorChangedEventArgs(cursor));
        }

        private void UpdateRelatedVariables() {
            if (RenderTarget == null) return;

            Viewport = new Rect(
                0, 0,
                RenderTarget.SizeInPixels.Width,
                RenderTarget.SizeInPixels.Height);
        }

        protected static Color BlendColor(Color color, double brushOpacity) {
            byte blendedA = (byte)(color.A * brushOpacity);

            return Color.FromArgb(
                blendedA,
                color.R,
                color.G,
                color.B
            );
        }

        public virtual void RequestCursorChange(InputCursor cursor) { }

        protected virtual void OnRendered(RenderTargetChangedEventArgs e) {
            RenderRequest?.Invoke(this, e);
        }

        /// <summary>
        /// 将内容变更提交到 Undo/Redo 系统
        /// </summary>
        protected virtual void Record(CanvasBlend blend, IEnumerable<StrokeBase> strokes, CanvasRenderTarget target) {
            _abandonStrokes.Clear();
            _renderableStrokes.Push(strokes);
            MainPage.Instance.UnReUtil.RecordCommand(
                execute: () => {
                    var rect = Rect.Empty;
                    using (var ds = target.CreateDrawingSession()) {
                        ds.Blend = blend;
                        if (_abandonStrokes.TryPop(out var strokeSegments)) {
                            _renderableStrokes.Push(strokeSegments);
                            foreach (var stroke in strokeSegments) {
                                stroke.Draw(ds);
                                rect = stroke.GetAffectedArea();
                            }                            
                        }
                    }
                    RenderRequest?.Invoke(this, new RenderTargetChangedEventArgs(RenderMode.PartialRegion, rect));
                },
                undo: () => {
                    var rect = Rect.Empty;
                    var mode = RenderMode.FullRegion;
                    using (var ds = target.CreateDrawingSession()) {
                        if (_renderableStrokes.TryPop(out var abandonStrokeSegments)) {
                            _abandonStrokes.Push(abandonStrokeSegments);
                            if (_renderableStrokes.IsEmpty) {
                                
                            }
                            else {
                                mode = RenderMode.PartialRegion;
                                foreach (var strokeSegment in _renderableStrokes) {
                                    foreach (var stroke in strokeSegment) {
                                        stroke.Draw(ds);
                                        rect = rect.UnionRect(stroke.GetAffectedArea());
                                    }
                                }
                            }                            
                        }
                    }
                    RenderRequest?.Invoke(this, new RenderTargetChangedEventArgs(mode, rect));
                }
            );
        }

        public virtual void Dispose() {
            SystemCursorChangeRequested = null;
            RenderRequest = null;
            GC.SuppressFinalize(this);
        }

        protected static bool IsDeviceLost(Exception ex) {
            return ex.HResult == unchecked((int)0x8899000C); // DXGI_ERROR_DEVICE_REMOVED
        }

        private CanvasRenderTarget? _renderTarget;
        private readonly ConcurrentStack<IEnumerable<StrokeBase>> _renderableStrokes = [];
        private readonly ConcurrentStack<IEnumerable<StrokeBase>> _abandonStrokes = [];
    }
}
