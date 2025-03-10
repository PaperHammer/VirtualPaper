using System.Threading.Tasks;
using Microsoft.UI;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Shapes;
using VirtualPaper.DraftPanel.Model.Interfaces;
using VirtualPaper.DraftPanel.Model.Runtime;
using VirtualPaper.DraftPanel.Utils;

// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.

namespace VirtualPaper.DraftPanel.Panels {
    /// <summary>
    /// An empty page that can be used on its own or navigated to within a Frame.
    /// </summary>
    public sealed partial class StaticImg : Page, IRuntime {
        public StaticImg(string folderPath) {
            this.InitializeComponent();
            this._folderPath = folderPath;

            _staticImgMd = new();
        }

        private void OnPointerPressed(object sender, PointerRoutedEventArgs e) {
            // 开始新的线条
            var pointerPoint = e.GetCurrentPoint(drawingCanvas);
            currentLine = new Polyline() {
                Stroke = new SolidColorBrush(Colors.Black),
                StrokeThickness = 2,
                StrokeLineJoin = PenLineJoin.Round,
                StrokeStartLineCap = PenLineCap.Round,
                StrokeEndLineCap = PenLineCap.Round
            };
            currentLine.Points.Add(pointerPoint.Position);
            drawingCanvas.Children.Add(currentLine);

            _staticImgMd.Draws.Add(new Draw() { Path = [TypeConvertUtil.Point2Array(pointerPoint.Position)], StrokeColor = TypeConvertUtil.Color2Hex(Colors.Black), StrokeThickness = 2 });
            currentLineIndex = _staticImgMd.Draws.Count - 1; // 记录当前线条在Draws列表中的索引

            isDrawing = true;
        }

        private void OnPointerMoved(object sender, PointerRoutedEventArgs e) {
            if (!isDrawing) return;

            // 继续当前线条
            var pointerPoint = e.GetCurrentPoint(drawingCanvas);
            currentLine.Points.Add(pointerPoint.Position);
            _staticImgMd.Draws[currentLineIndex].Path.Add(TypeConvertUtil.Point2Array(pointerPoint.Position));
        }

        private void OnPointerReleased(object sender, PointerRoutedEventArgs e) {
            if (isDrawing) {
                // 结束当前线条
                isDrawing = false;
            }
        }

        public async Task SaveAsync() {
           await _staticImgMd.SaveAsync(_folderPath);
        }

        public async Task LoadAsync() {
            await _staticImgMd.LoadAsync(_folderPath);
        }

        private bool isDrawing = false;
        private Polyline currentLine;
        private readonly StaticImgMetadata _staticImgMd;
        private int currentLineIndex = 0;
        private readonly string _folderPath;
    }
}
