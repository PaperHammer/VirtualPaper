using System.Runtime.InteropServices;
using System.Runtime.InteropServices.Marshalling;

namespace BuiltIn.Utils {
    public static partial class NativeRnder {
        // 基本类型定义
        [StructLayout(LayoutKind.Sequential)]
        public struct ColorF {
            public float r;
            public float g;
            public float b;
            public float a;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct PointF {
            public float x;
            public float y;
        }

        // 创建和销毁渲染器
        [LibraryImport("D2DRender.dll", EntryPoint = "CreateRenderer")]
        internal static partial IntPtr CreateRenderer(int width, int height);

        [LibraryImport("D2DRender.dll", EntryPoint = "DestroyRenderer")]
        internal static partial void DestroyRenderer(IntPtr renderer);

        // 基本绘制操作
        [LibraryImport("D2DRender.dll", EntryPoint = "Clear")]
        internal static partial void Clear(IntPtr renderer, ColorF color);

        [LibraryImport("D2DRender.dll", EntryPoint = "DrawRectangle")]
        internal static partial void DrawRectangle(
            IntPtr renderer,
            PointF topLeft,
            PointF size,
            ColorF color,
            float strokeWidth);

        [LibraryImport("D2DRender.dll", EntryPoint = "FillRectangle")]
        internal static partial void FillRectangle(
            IntPtr renderer,
            PointF topLeft,
            PointF size,
            ColorF color);

        // 擦除路径操作
        [LibraryImport("D2DRender.dll", EntryPoint = "BeginErasePath")]
        internal static partial void BeginErasePath(IntPtr renderer, PointF start);

        [LibraryImport("D2DRender.dll", EntryPoint = "AddEraseLine")]
        internal static partial void AddEraseLine(IntPtr renderer, PointF end);

        [LibraryImport("D2DRender.dll", EntryPoint = "EndErasePath")]
        internal static partial void EndErasePath(IntPtr renderer);

        // 像素数据操作
        [LibraryImport("D2DRender.dll", EntryPoint = "GetPixels")]
        internal static partial void GetPixels(
            IntPtr renderer,
            [MarshalUsing(CountElementName = nameof(bufferSize))]
            byte[] output,
            int bufferSize);

        // 调整大小
        [LibraryImport("D2DRender.dll", EntryPoint = "Resize")]
        internal static partial void Resize(IntPtr renderer, int width, int height);
    }
}
