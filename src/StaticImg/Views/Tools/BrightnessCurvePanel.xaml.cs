using System;
using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Shapes;
using Windows.Foundation;

namespace Workloads.Creation.StaticImg.Views.Tools {
    public sealed partial class BrightnessCurvePanel : UserControl {
        public event EventHandler<double[]>? CurveChanged;

        public BrightnessCurvePanel() {
            this.InitializeComponent();

            var borderBrush = (SolidColorBrush)this.Resources["BorderBrush"];
            var gridLineBrush = (SolidColorBrush)this.Resources["GridLineBrush"];
            var curveStroke = (SolidColorBrush)this.Resources["CurveStrokeBrush"];

            var grayBorderBrush = (SolidColorBrush)this.Resources["ThumbGrayBorderBrush"];
            var tangentLineBrush = (SolidColorBrush)this.Resources["TangentLineBrush"];
            
            var blackFill = new SolidColorBrush(Colors.Black);
            var whiteFill = new SolidColorBrush(Colors.White);

            // 设置控件尺寸
            this.Width = 300;
            this.Height = 260;
            this.HorizontalAlignment = HorizontalAlignment.Center;
            this.VerticalAlignment = VerticalAlignment.Center;

            var rootGrid = new Grid();

            // 定义 2x2 网格
            rootGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
            rootGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            rootGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            rootGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

            // 创建左侧垂直标尺
            var leftRuler = CreateGradientRuler(Orientation.Vertical, borderBrush);
            Grid.SetRow(leftRuler, 0);
            Grid.SetColumn(leftRuler, 0);
            rootGrid.Children.Add(leftRuler);

            // 创建下方水平标尺
            var bottomRuler = CreateGradientRuler(Orientation.Horizontal, borderBrush);
            Grid.SetRow(bottomRuler, 1);
            Grid.SetColumn(bottomRuler, 1);
            rootGrid.Children.Add(bottomRuler);

            // 创建曲线容器
            var curveContainer = new Border {
                BorderBrush = borderBrush,
                BorderThickness = new Thickness(1),
            };

            _curveCanvas = new Canvas {
                Background = new SolidColorBrush(Colors.Transparent)
            };

            // 重新添加网格线逻辑
            for (int i = 1; i < 4; i++) {
                _curveCanvas.Children.Add(new Line {
                    Stroke = gridLineBrush,
                    StrokeThickness = 0.5,
                    Tag = $"V{i}"
                });
                _curveCanvas.Children.Add(new Line {
                    Stroke = gridLineBrush,
                    StrokeThickness = 0.5,
                    Tag = $"H{i}"
                });
            }

            _blackTangentLine = CreateTangentLine(tangentLineBrush);
            _whiteTangentLine = CreateTangentLine(tangentLineBrush);
            _curveCanvas.Children.Add(_blackTangentLine);
            _curveCanvas.Children.Add(_whiteTangentLine);

            _curvePath = new Path {
                Stroke = curveStroke,
                StrokeThickness = CurveThickness,
                Data = new PathGeometry()
            };
            _curveCanvas.Children.Add(_curvePath);

            _blackThumb = CreateThumb(blackFill, grayBorderBrush);
            _whiteThumb = CreateThumb(whiteFill, grayBorderBrush);
            _curveCanvas.Children.Add(_blackThumb);
            _curveCanvas.Children.Add(_whiteThumb);

            curveContainer.Child = _curveCanvas;

            Grid.SetRow(curveContainer, 0);
            Grid.SetColumn(curveContainer, 1);
            rootGrid.Children.Add(curveContainer);

            this.Content = rootGrid;

            _curveCanvas.PointerPressed += OnPointerPressed;
            _curveCanvas.PointerMoved += OnPointerMoved;
            _curveCanvas.PointerReleased += OnPointerReleased;
            _curveCanvas.SizeChanged += (_, _) => UpdateVisuals();
            DispatcherQueue.TryEnqueue(() => UpdateVisuals());
        }

        #region core

        public double[] GenerateLUT(int size = 256) {
            var lut = new double[size];
            double p0x = FixedBlackEnd.X, p1x = _blackPoint.X, p2x = _whitePoint.X, p3x = FixedWhiteEnd.X;
            double p0y = FixedBlackEnd.Y, p1y = _blackPoint.Y, p2y = _whitePoint.Y, p3y = FixedWhiteEnd.Y;

            for (int i = 0; i < size; i++) {
                double x = (double)i / (size - 1);
                double t = SolveBezierParameter(x, p0x, p1x, p2x, p3x);
                lut[i] = Math.Clamp(EvalCubicBezier(t, p0y, p1y, p2y, p3y), 0, 1);
            }
            return lut;
        }

        private static double SolveBezierParameter(double x, double p0, double p1, double p2, double p3) {
            double u = x;
            for (int i = 0; i < 8; i++) {
                double err = EvalCubicBezier(u, p0, p1, p2, p3) - x;
                double d = CubicBezierDerivative(u, p0, p1, p2, p3);
                if (Math.Abs(d) < 1e-12) break;
                u = Math.Clamp(u - err / d, 0, 1);
            }
            return u;
        }

        private static double EvalCubicBezier(double t, double p0, double p1, double p2, double p3) {
            double mt = 1 - t;
            return mt * mt * mt * p0 + 3 * mt * mt * t * p1 + 3 * mt * t * t * p2 + t * t * t * p3;
        }

        private static double CubicBezierDerivative(double t, double p0, double p1, double p2, double p3) {
            double mt = 1 - t;
            return 3 * mt * mt * (p1 - p0) + 6 * mt * t * (p2 - p1) + 3 * t * t * (p3 - p2);
        }

        #endregion

        #region UI 渲染

        private void UpdateVisuals() {
            double w = _curveCanvas.ActualWidth;
            double h = _curveCanvas.ActualHeight;
            if (w <= 0 || h <= 0) return;

            foreach (var child in _curveCanvas.Children) {
                if (child is Line line && line.Tag is string tag) {
                    double ratio = int.Parse(tag[1..].ToString()) / 4.0;
                    if (tag.StartsWith('V')) { line.X1 = line.X2 = ratio * w; line.Y1 = 0; line.Y2 = h; }
                    else { line.Y1 = line.Y2 = ratio * h; line.X1 = 0; line.X2 = w; }
                }
            }

            Point ToUI(Point tone) => new(tone.X * w, (1.0 - tone.Y) * h);

            var uiP0 = ToUI(FixedBlackEnd);
            var uiP3 = ToUI(FixedWhiteEnd);

            var uiCP1 = ToUI(_blackPoint);
            var uiCP2 = ToUI(_whitePoint);

            var figure = new PathFigure { StartPoint = uiP0, IsClosed = false, IsFilled = false };
            figure.Segments.Add(new BezierSegment { Point1 = uiCP1, Point2 = uiCP2, Point3 = uiP3 });
            var geo = (PathGeometry)_curvePath.Data;
            geo.Figures.Clear();
            geo.Figures.Add(figure);

            SetThumbPos(_blackThumb, uiCP1);
            SetThumbPos(_whiteThumb, uiCP2);

            SetLine(_blackTangentLine, uiP0, uiCP1);
            SetLine(_whiteTangentLine, uiP3, uiCP2);

            CurveChanged?.Invoke(this, GenerateLUT());
        }

        private static void SetThumbPos(Ellipse thumb, Point center) {
            Canvas.SetLeft(thumb, center.X - ThumbRadius);
            Canvas.SetTop(thumb, center.Y - ThumbRadius);
        }

        private static void SetLine(Line line, Point from, Point to) {
            line.X1 = from.X; line.Y1 = from.Y;
            line.X2 = to.X; line.Y2 = to.Y;
        }

        private static Ellipse CreateThumb(Brush fill, Brush stroke) => new() {
            Width = ThumbRadius * 2,
            Height = ThumbRadius * 2,
            Fill = fill,
            Stroke = stroke,
            StrokeThickness = 2
        };

        private static Line CreateTangentLine(Brush stroke) => new() {
            Stroke = stroke,
            StrokeThickness = TangentThickness,
            StrokeDashArray = [3, 3]
        };

        #endregion

        #region 交互

        private void OnPointerPressed(object sender, PointerRoutedEventArgs e) {
            var pos = e.GetCurrentPoint(_curveCanvas).Position;
            double w = _curveCanvas.ActualWidth, h = _curveCanvas.ActualHeight;
            if (w <= 0 || h <= 0) return;

            Point ToTone(Point p) => new(p.X / w, 1.0 - p.Y / h);
            var tone = ToTone(pos);
            double dB = Distance(tone, _blackPoint);
            double dW = Distance(tone, _whitePoint);
            double threshold = 20.0 / Math.Min(w, h);

            if (dB < threshold && dB <= dW) { _isDraggingBlack = true; _curveCanvas.CapturePointer(e.Pointer); }
            else if (dW < threshold) { _isDraggingWhite = true; _curveCanvas.CapturePointer(e.Pointer); }
        }

        private void OnPointerMoved(object sender, PointerRoutedEventArgs e) {
            if (!_isDraggingBlack && !_isDraggingWhite) return;
            var pos = e.GetCurrentPoint(_curveCanvas).Position;
            double w = _curveCanvas.ActualWidth, h = _curveCanvas.ActualHeight;
            if (w <= 0 || h <= 0) return;

            var tone = new Point(Math.Clamp(pos.X / w, 0, 1), Math.Clamp(1.0 - pos.Y / h, 0, 1));
            if (_isDraggingBlack) _blackPoint = tone;
            if (_isDraggingWhite) _whitePoint = tone;
            UpdateVisuals();
        }

        private void OnPointerReleased(object sender, PointerRoutedEventArgs e) {
            _isDraggingBlack = _isDraggingWhite = false;
            _curveCanvas.ReleasePointerCaptures();
        }

        private static double Distance(Point a, Point b) => Math.Sqrt(Math.Pow(a.X - b.X, 2) + Math.Pow(a.Y - b.Y, 2));

        #endregion

        #region Helper Methods

        private FrameworkElement CreateGradientRuler(Orientation orientation, Brush borderBrush) {
            var container = new Border {
                Background = new LinearGradientBrush {
                    StartPoint = orientation == Orientation.Horizontal ? new Point(0, 0) : new Point(0, 1),
                    EndPoint = orientation == Orientation.Horizontal ? new Point(1, 0) : new Point(0, 0)
                },
                BorderBrush = borderBrush, // 使用传入的主题画刷
                BorderThickness = new Thickness(1),
            };

            var gradientStops = ((LinearGradientBrush)container.Background).GradientStops;
            gradientStops.Add(new GradientStop { Color = Colors.Black, Offset = 0.0 });
            gradientStops.Add(new GradientStop { Color = Colors.White, Offset = 1.0 });

            if (orientation == Orientation.Horizontal) {
                container.Height = 8;
                container.Margin = new Thickness(0, 6, 0, 0);
            }
            else {
                container.Width = 8;
                container.Margin = new Thickness(0, 0, 6, 0);
            }

            return container;
        }
        #endregion

        // 控制点初始位置与固定端点重合 → 默认直线（线性）
        private Point _blackPoint = new(0, 0);
        private Point _whitePoint = new(1, 1);
        private bool _isDraggingBlack;
        private bool _isDraggingWhite;

        private static readonly Point FixedBlackEnd = new(0, 0);
        private static readonly Point FixedWhiteEnd = new(1, 1);

        private readonly Canvas _curveCanvas;
        private readonly Path _curvePath;
        private readonly Line _blackTangentLine;
        private readonly Line _whiteTangentLine;
        private readonly Ellipse _blackThumb;
        private readonly Ellipse _whiteThumb;

        private const double ThumbRadius = 7;
        private const double CurveThickness = 2.0;
        private const double TangentThickness = 1.0;
    }
}
