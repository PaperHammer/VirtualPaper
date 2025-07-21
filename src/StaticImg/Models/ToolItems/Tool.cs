using System;
using System.Collections.Generic;
using Microsoft.Graphics.Canvas;
using Microsoft.UI.Input;
using VirtualPaper.Common.Utils.UnReUtil;
using VirtualPaper.UIComponent.Services;
using Windows.Foundation;
using Windows.UI;
using Workloads.Creation.StaticImg.Models.EventArg;
using Workloads.Creation.StaticImg.Models.ToolItems.Utils;

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
        protected virtual void CommitContentChange(IEnumerable<StrokeSegment> strokeSegments, CanvasRenderTarget target) {
            var rect = CalculateTotalAffectedRegion(strokeSegments);
            MainPage.Instance.UnReUtil.RecordCommand(
                execute: () => {
                    foreach (var segment in strokeSegments) {
                        segment.ApplyToRenderTarget(target, UndoRedoOPType.Redo);
                    }
                    RenderRequest?.Invoke(this, new RenderTargetChangedEventArgs(RenderMode.PartialRegion, rect));
                },
                undo: () => {
                    foreach (var segment in strokeSegments) {
                        segment.ApplyToRenderTarget(target, UndoRedoOPType.Undo);
                    }
                    RenderRequest?.Invoke(this, new RenderTargetChangedEventArgs(RenderMode.PartialRegion, rect));
                },
                opType: SI_UndoRedo_OP_Type.Region
            );
        }

        // 计算所有线段合并的脏矩形区域
        private static Rect CalculateTotalAffectedRegion(IEnumerable<StrokeSegment> segments) {
            int left = int.MaxValue, top = int.MaxValue;
            int right = 0, bottom = 0;

            foreach (var segment in segments) {
                foreach (var point in segment.Points) {
                    left = Math.Min(left, point.Left);
                    top = Math.Min(top, point.Top);
                    right = Math.Max(right, point.Left + point.Width);
                    bottom = Math.Max(bottom, point.Top + point.Height);
                }
            }

            return new Rect(left, top, right - left, bottom - top);
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
    }

    public partial class StrokeSegment {
        public List<StrokePoint> Points { get; } = [];

        public StrokeSegment(StrokePoint point) {
            Points.Add(point);
        }

        public void ApplyToRenderTarget(CanvasRenderTarget target, UndoRedoOPType oPType) {
            using (var ds = target.CreateDrawingSession()) {
                foreach (var point in Points) {
                    target.SetPixelBytes(
                        oPType == UndoRedoOPType.Undo
                            ? point.OldPixels
                            : point.NewPixels,
                        point.Left, point.Top, point.Width, point.Height
                    );
                }
            }
        }
    }

    public class StrokePoint {
        public int Left, Top, Width, Height;
        public byte[] OldPixels;
        public byte[] NewPixels;
        public float Thickness;
        public Point StartPoint => new(Left, Top);

        public StrokePoint(int left, int top, int width, int height, byte[] oldPixels, float thickness) {
            Left = left; Top = top; Width = width; Height = height;
            OldPixels = oldPixels;
            Thickness = thickness;
        }
    }
}
