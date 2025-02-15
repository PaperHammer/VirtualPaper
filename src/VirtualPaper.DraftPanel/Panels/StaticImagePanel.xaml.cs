using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Shapes;

// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.

namespace VirtualPaper.DraftPanel.Panels {
    public sealed partial class StaticImagePanel : Page {
        public StaticImagePanel() {
            this.InitializeComponent();
        }

        //private void OnPointerPressed(object sender, PointerRoutedEventArgs e) {
        //    // ��ʼ�µ�����
        //    var pointerPoint = e.GetCurrentPoint(drawingCanvas);
        //    currentLine = new Polyline() {
        //        Stroke = new SolidColorBrush(Colors.Black),
        //        StrokeThickness = 2,
        //        StrokeLineJoin = PenLineJoin.Round,
        //        StrokeStartLineCap = PenLineCap.Round,
        //        StrokeEndLineCap = PenLineCap.Round
        //    };
        //    currentLine.Points.Add(pointerPoint.Position);
        //    drawingCanvas.Children.Add(currentLine);

        //    isDrawing = true;
        //}

        //private void OnPointerMoved(object sender, PointerRoutedEventArgs e) {
        //    if (!isDrawing) return;

        //    // ������ǰ����
        //    var pointerPoint = e.GetCurrentPoint(drawingCanvas);
        //    currentLine.Points.Add(pointerPoint.Position);
        //}

        //private void OnPointerReleased(object sender, PointerRoutedEventArgs e) {
        //    if (isDrawing) {
        //        // ������ǰ����
        //        isDrawing = false;
        //    }
        //}

        private bool isDrawing = false;
        private Polyline currentLine;
    }
}
