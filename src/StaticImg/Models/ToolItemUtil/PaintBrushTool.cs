using System;
using System.Diagnostics;
using System.Threading.Tasks;
using Windows.Foundation;
using Windows.UI;
using Workloads.Creation.StaticImg.Models.EventArg;

namespace Workloads.Creation.StaticImg.Models.ToolItemUtil {
    class PaintBrushTool : ITool {
        public PaintBrushTool(LayerManagerData managerData) {
            _managerData = managerData;
            //_pathPoints = [];
        }

        public void OnPointerEntered(ToolItemEventArgs e) {
            _isDrawable = _managerData.SelectedLayerData.IsEnable;
        }

        public async void OnPointerPressed(ToolItemEventArgs e) {
            if (!_isDrawable || e.CurrentPointerPoint.Properties.IsMiddleButtonPressed)
                return;
            
            // 开始绘制
            _isDrawing = true;
            // 开始新的线条
            _blendedColor = BlendColor(e.CurrentPointerPoint.Properties.IsRightButtonPressed ?
                _managerData.BackgroundColor : _managerData.ForegroundColor, _managerData.BrushOpacity / 100);
            // 记录当前位置
            _lastPoint = e.CurrentPointerPoint.Position;
            // 在当前位置绘制一个点
            await DrawPixelAsync((int)_lastPoint.Value.X, (int)_lastPoint.Value.Y, _blendedColor);

            //var pathColor = new SolidColorBrush(UintColor.MixAlpha(color, _managerData.BrushOpacity / 100.0));
            //// 创建 Path 和 PathGeometry
            //_pathGeometry = new PathGeometry();
            //_currentPath = new Path {
            //    Stroke = pathColor,
            //    StrokeThickness = _managerData.BrushThickness,
            //    StrokeLineJoin = PenLineJoin.Round,
            //    StrokeStartLineCap = PenLineCap.Round,
            //    StrokeEndLineCap = PenLineCap.Round,
            //    Data = _pathGeometry
            //};

            //// 创建初始点
            //var startPoint = pointerPoint.Position;
            //var figure = new PathFigure { StartPoint = startPoint };
            //_pathGeometry.Figures.Add(figure);

            //// 创建线条数据模型
            //_currentDraw = new STADraw {
            //    StrokeColor = UintToSolidBrushConverter.ColorToHex(pathColor.Color),
            //    StrokeThickness = _managerData.BrushThickness,
            //    Points = [new PointF((float)pointerPoint.Position.X, (float)pointerPoint.Position.Y), new PointF((float)pointerPoint.Position.X, (float)pointerPoint.Position.Y)],
            //    ZTime = DateTime.Now.Ticks
            //};

            //_managerData.SelectedLayerData.AddDraw(_currentPath, _currentDraw);
        }

        public void OnPointerMoved(ToolItemEventArgs e) {
            if (!_isDrawable || !_isDrawing) return;           

            //// 更新 PathGeometry
            //var lineSegment = new LineSegment { Point = pointerPoint.Position };
            //_pathGeometry.Figures[0].Segments.Add(lineSegment);

            //// 更新数据模型
            //_currentDraw.Points.Add(new PointF((float)pointerPoint.Position.X, (float)pointerPoint.Position.Y));

            var currentPoint = e.CurrentPointerPoint.Position;
            _ = DrawLineAsync((int)_lastPoint.Value.X, (int)_lastPoint.Value.Y, (int)currentPoint.X, (int)currentPoint.Y, _blendedColor);
            _lastPoint = currentPoint;
        }

        public void OnPointerReleased(ToolItemEventArgs e) {
            EndDrawing();
        }

        public void OnPointerExited(ToolItemEventArgs e) {
            _lastPoint = null;
            EndDrawing();
        }

        /// <summary>
        /// 绘制单个像素
        /// </summary>
        private async Task DrawPixelAsync(int x, int y, Color color) {
            await _managerData.SelectedLayerData.RenderAsync(x, y, color, _managerData.BrushThickness);
        }

        /// <summary>
        /// 绘制一条线段
        /// </summary>
        private async Task DrawLineAsync(int x0, int y0, int x1, int y1, Color color) {
            int dx = Math.Abs(x1 - x0), sx = x0 < x1 ? 1 : -1;
            int dy = -Math.Abs(y1 - y0), sy = y0 < y1 ? 1 : -1;
            int err = dx + dy, e2;

            // 线性插值
            while (true) {
                await _managerData.SelectedLayerData.RenderAsync(x0, y0, color, _managerData.BrushThickness);
                if (x0 == x1 && y0 == y1) break;
                e2 = 2 * err;
                if (e2 >= dy) { err += dy; x0 += sx; }
                if (e2 <= dx) { err += dx; y0 += sy; }
            }
        }

        private void EndDrawing() {
            _isDrawing = false;
            //_currentPath = null;
            //_pathGeometry = null;
            //_currentDraw = null;

            //_managerData.SelectedLayerData.DrawsChanged();
        }

        private Color BlendColor(Color color, double brushOpacity) {
            byte blendedA = (byte)(color.A * brushOpacity);

            return Color.FromArgb(
                blendedA,
                color.R,
                color.G,
                color.B
            );
        }

        private Color _blendedColor;
        private bool _isDrawable = false, _isDrawing = false;
        private Point? _lastPoint;
        //private Path _currentPath; // 当前正在绘制的路径
        //private PathGeometry _pathGeometry; // 当前正在绘制的路径
        //private STADraw _currentDraw;  // 当前线条的数据模型
        //Point _curPoint;
        //private readonly List<ArcPoint> _pathPoints;
        private readonly LayerManagerData _managerData;
    }
}
