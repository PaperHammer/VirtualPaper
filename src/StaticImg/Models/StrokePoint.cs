using System;
using System.Runtime.InteropServices;
using Windows.Foundation;
using Windows.UI;

namespace Workloads.Creation.StaticImg.Models {
    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    class StrokePoint {
        public Point Position { get; }
        public bool IsDrawn { get; set; }
        public int Size { get; }
        public Color Color { get; }
        public DateTime CreationTime { get; }

        public StrokePoint(Point position, int size, Color color) {
            Position = position;
            Size = size;
            Color = color;
            IsDrawn = false;
            CreationTime = DateTime.UtcNow;
        }

        public Rect GetBounds() => new(
            Position.X - Size / 2,
            Position.Y - Size / 2,
            Size, Size);
    }
}
