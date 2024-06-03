using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Shapes;
using System.Windows.Threading;

namespace VirtualPaper.ScreenSaver.Effects
{
    public class Bubble
    {
        public Bubble(Window window,  Canvas canvas)
        {
            _window = window;
            _canvas = canvas;

            _dispatcherTimer = new()
            {
                Interval = TimeSpan.FromSeconds(3),
            };
            _dispatcherTimer.Tick += DispatcherTimer_Tick;            

            _random = new();
        }

        public void Start()
        {
            _dispatcherTimer.Start();
        }

        private void DispatcherTimer_Tick(object? sender, EventArgs e)
        {
            if (_cnt++ == 20)
            {
                _dispatcherTimer.Stop();
                _dispatcherTimer.Tick -= DispatcherTimer_Tick;
                return;
            }
            GenerateBubbles();
        }

        private void GenerateBubbles()
        {
            LinearGradientBrush brush = new()
            {
                StartPoint = new System.Windows.Point(0, 0), // 左上
                EndPoint = new System.Windows.Point(1, 1), // 右下
            };
            System.Windows.Media.Color color = System.Windows.Media.Color.FromArgb(
                255,
                (byte)_random.Next(1, 255),
                (byte)_random.Next(1, 255),
                (byte)_random.Next(1, 255));

            Ellipse bubble = new()
            {
                Width = 130,
                Height = 130,
                Fill = brush,
                Stroke = new SolidColorBrush(color),
            };

            Ellipse highlight1 = new()
            {
                Width = 18,
                Height = 18,
                Fill = System.Windows.Media.Brushes.White,
                Margin = new Thickness(-50, -65, 0, 0),
                Opacity = 0.7,
            };

            Ellipse highlight2 = new()
            {
                Width = 12,
                Height = 12,
                Fill = System.Windows.Media.Brushes.White,
                Margin = new Thickness(-70, -35, 0, 0),
                Opacity = 0.7,
            };

            Grid container = new();

            container.Children.Add(bubble);
            container.Children.Add(highlight1);
            container.Children.Add(highlight2);

            color.A = 185;
            brush.GradientStops.Add(new GradientStop(color, 0));

            color.A = 115;
            brush.GradientStops.Add(new GradientStop(color, 0.25));

            color.A = 75;
            brush.GradientStops.Add(new GradientStop(color, 0.5));

            color.A = 25;
            brush.GradientStops.Add(new GradientStop(color, 0.75));

            color.A = 0;
            brush.GradientStops.Add(new GradientStop(color, 1));

            double startX = 0;
            double startY = _canvas.ActualHeight - bubble.Height;

            // 不要使用 AutoReverse 与 RepeatBehavior = RepeatBehavior.Forever
            DoubleAnimation animX = new()
            {
                From = startX,
                To = _canvas.ActualWidth - bubble.Height,
                Duration = TimeSpan.FromSeconds(_random.Next(5, 10)),
            };

            DoubleAnimation animY = new()
            {
                From = startY,
                To = 0,
                Duration = TimeSpan.FromSeconds(_random.Next(5, 10)),
            };

            Storyboard.SetTarget(animX, container);
            Storyboard.SetTargetProperty(animX, new PropertyPath("(Canvas.Left)"));

            Storyboard.SetTarget(animY, container);
            Storyboard.SetTargetProperty(animY, new PropertyPath("(Canvas.Top)"));

            Storyboard sx = new(), sy = new();

            animX.Completed += (object? sender, EventArgs e) =>
            {
                double currentLeft = Canvas.GetLeft(container);

                if (currentLeft <= 0 || currentLeft >= _canvas.ActualWidth - bubble.Width)
                {
                    animX.To = currentLeft <= 0 ? _canvas.ActualWidth - bubble.Width : 0;
                    animX.From = currentLeft;
                }

                sx = new();
                sx.Children.Add(animX);
                sx.Begin(_window);
            };

            animY.Completed += (object? sender, EventArgs e) =>
            {
                double currentTop = Canvas.GetTop(container);

                if (currentTop <= 0 || currentTop >= _canvas.ActualHeight - bubble.Width)
                {
                    animY.To = currentTop <= 0 ? _canvas.ActualHeight - bubble.Width : 0;
                    animY.From = currentTop;
                }

                sy = new();
                sy.Children.Add(animY);
                sy.Begin(_window);
            };

            sx.Children.Add(animX);
            sy.Children.Add(animY);

            _canvas.Children.Add(container);

            sx.Begin(_window);
            sy.Begin(_window);
        }

        private DispatcherTimer _dispatcherTimer;
        private int _cnt;
        private Random _random;
        private Window _window;
        private Canvas _canvas;
    }
}
