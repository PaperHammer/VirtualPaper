using System;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Shapes;
using VirtualPaper.Shader.Models;
using Windows.Foundation;

namespace Workloads.Creation.StaticImg.Views.Tools.Effects {
    public sealed partial class BrightnessEffectPanel : EffectPanelBase {
        private readonly float[] _rTable = new float[256];
        private readonly float[] _gTable = new float[256];
        private readonly float[] _bTable = new float[256];

        private Point _blackPoint = new(0, 0);
        private Point _whitePoint = new(1, 1);
        private bool _isDraggingBlack;
        private bool _isDraggingWhite;

        private static readonly Point FixedBlackEnd = new(0, 0);
        private static readonly Point FixedWhiteEnd = new(1, 1);

        public BrightnessEffectPanel() {
            this.InitializeComponent();
            ResetToIdentity();
            DispatcherQueue.TryEnqueue(() => UpdateVisuals());
        }

        public override EffectParams Params => new() {
            RedTable = _rTable,
            GreenTable = _gTable,
            BlueTable = _bTable,
            AlphaTable = null,
            Dpi = 96f,
        };

        private void ResetToIdentity() {
            for (int i = 0; i < 256; i++) {
                float v = i / 255f;
                _rTable[i] = v;
                _gTable[i] = v;
                _bTable[i] = v;
            }
        }

        #region Bezier Calculations

        private double[] GenerateLUT(int size = 256) {
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

        #region UI Update

        private void UpdateVisuals() {
            double w = CurveCanvas.ActualWidth;
            double h = CurveCanvas.ActualHeight;
            if (w <= 0 || h <= 0) return;

            // Update grid lines
            UpdateGridLine(VLine1, 1, w, h, isVertical: true);
            UpdateGridLine(HLine1, 1, w, h, isVertical: false);
            UpdateGridLine(VLine2, 2, w, h, isVertical: true);
            UpdateGridLine(HLine2, 2, w, h, isVertical: false);
            UpdateGridLine(VLine3, 3, w, h, isVertical: true);
            UpdateGridLine(HLine3, 3, w, h, isVertical: false);

            Point ToUI(Point tone) => new(tone.X * w, (1.0 - tone.Y) * h);

            var uiP0 = ToUI(FixedBlackEnd);
            var uiP3 = ToUI(FixedWhiteEnd);
            var uiCP1 = ToUI(_blackPoint);
            var uiCP2 = ToUI(_whitePoint);

            // Update curve path
            var figure = new PathFigure { StartPoint = uiP0, IsClosed = false, IsFilled = false };
            figure.Segments.Add(new BezierSegment { Point1 = uiCP1, Point2 = uiCP2, Point3 = uiP3 });
            var geo = (PathGeometry)CurvePath.Data;
            geo.Figures.Clear();
            geo.Figures.Add(figure);

            // Update thumbs
            SetThumbPos(BlackThumb, uiCP1);
            SetThumbPos(WhiteThumb, uiCP2);

            // Update tangent lines
            SetLine(BlackTangentLine, uiP0, uiCP1);
            SetLine(WhiteTangentLine, uiP3, uiCP2);

            // Update auxiliary lines
            BlackHorizontalLine.X1 = 0;
            BlackHorizontalLine.X2 = w;
            BlackHorizontalLine.Y1 = uiCP1.Y;
            BlackHorizontalLine.Y2 = uiCP1.Y;

            WhiteVerticalLine.X1 = uiCP2.X;
            WhiteVerticalLine.X2 = uiCP2.X;
            WhiteVerticalLine.Y1 = 0;
            WhiteVerticalLine.Y2 = h;

            // Update value texts
            BlackValueText.Text = ((int)Math.Round((1 - _blackPoint.Y) * 255)).ToString();
            WhiteValueText.Text = ((int)Math.Round(_whitePoint.X * 255)).ToString();

            // Update LUT and raise params changed
            var lut = GenerateLUT();
            for (int i = 0; i < 256; i++) {
                _rTable[i] = (float)lut[i];
                _gTable[i] = (float)lut[i];
                _bTable[i] = (float)lut[i];
            }
            RaiseParamsChanged();
        }

        private static void UpdateGridLine(Line line, int index, double w, double h, bool isVertical) {
            double ratio = index / 4.0;
            if (isVertical) {
                line.X1 = line.X2 = ratio * w;
                line.Y1 = 0;
                line.Y2 = h;
            }
            else {
                line.Y1 = line.Y2 = ratio * h;
                line.X1 = 0;
                line.X2 = w;
            }
        }

        private static void SetThumbPos(Ellipse thumb, Point center) {
            const double radius = 7;
            Canvas.SetLeft(thumb, center.X - radius);
            Canvas.SetTop(thumb, center.Y - radius);
        }

        private static void SetLine(Line line, Point from, Point to) {
            line.X1 = from.X; line.Y1 = from.Y;
            line.X2 = to.X; line.Y2 = to.Y;
        }

        #endregion

        #region Pointer Events

        private void OnPointerPressed(object sender, PointerRoutedEventArgs e) {
            var pos = e.GetCurrentPoint(CurveCanvas).Position;
            double w = CurveCanvas.ActualWidth, h = CurveCanvas.ActualHeight;
            if (w <= 0 || h <= 0) return;

            Point ToTone(Point p) => new(p.X / w, 1.0 - p.Y / h);
            var tone = ToTone(pos);
            double dB = Distance(tone, _blackPoint);
            double dW = Distance(tone, _whitePoint);
            double threshold = 20.0 / Math.Min(w, h);

            if (dB < threshold && dB <= dW) {
                _isDraggingBlack = true;
                CurveCanvas.CapturePointer(e.Pointer);
            }
            else if (dW < threshold) {
                _isDraggingWhite = true;
                CurveCanvas.CapturePointer(e.Pointer);
            }
        }

        private void OnPointerMoved(object sender, PointerRoutedEventArgs e) {
            if (!_isDraggingBlack && !_isDraggingWhite) return;
            var pos = e.GetCurrentPoint(CurveCanvas).Position;
            double w = CurveCanvas.ActualWidth, h = CurveCanvas.ActualHeight;
            if (w <= 0 || h <= 0) return;

            var tone = new Point(Math.Clamp(pos.X / w, 0, 1), Math.Clamp(1.0 - pos.Y / h, 0, 1));

            if (_isDraggingBlack) {
                tone.X = Math.Min(tone.X, _whitePoint.X);
                tone.Y = Math.Min(tone.Y, _whitePoint.Y);
                _blackPoint = tone;
            }

            if (_isDraggingWhite) {
                tone.X = Math.Max(tone.X, _blackPoint.X);
                tone.Y = Math.Max(tone.Y, _blackPoint.Y);
                _whitePoint = tone;
            }

            UpdateVisuals();
        }

        private void OnPointerReleased(object sender, PointerRoutedEventArgs e) {
            _isDraggingBlack = _isDraggingWhite = false;
            CurveCanvas.ReleasePointerCaptures();
        }

        private void OnCanvasSizeChanged(object sender, SizeChangedEventArgs e) {
            UpdateVisuals();
        }

        private static double Distance(Point a, Point b) => Math.Sqrt(Math.Pow(a.X - b.X, 2) + Math.Pow(a.Y - b.Y, 2));

        #endregion
    }
}
