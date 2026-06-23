using Microsoft.Graphics.Canvas.Effects;
using System.Runtime.CompilerServices;

namespace VirtualPaper.Shader.Core {
    /// <summary>
    /// ColorMatrixEffect matrix builder.
    /// <para/>R' = Src.R*M11 + Src.G*M21 + Src.B*M31 + Src.A*M41 + M51
    /// <para/>G' = Src.R*M12 + Src.G*M22 + Src.B*M32 + Src.A*M42 + M52
    /// <para/>B' = Src.R*M13 + Src.G*M23 + Src.B*M33 + Src.A*M43 + M53
    /// <para/>A' = Src.R*M14 + Src.G*M24 + Src.B*M34 + Src.A*M44 + M54
    /// </summary>
    public static class Matrix5x4Extension {
        const float Gray = 1f / 3f;

        public static readonly Matrix5x4 Identity = new() {
            M11 = 1, M22 = 1, M33 = 1, M44 = 1,
        };

        public static readonly Matrix5x4 Invert = new() {
            M11 = -1, M22 = -1, M33 = -1, M44 = 1,
            M51 = 1, M52 = 1, M53 = 1,
        };

        public static readonly Matrix5x4 Grayscale = new() {
            M11 = Gray, M12 = Gray, M13 = Gray,
            M21 = Gray, M22 = Gray, M23 = Gray,
            M31 = Gray, M32 = Gray, M33 = Gray,
            M44 = 1,
        };

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Matrix5x4 Exposure(float exposure = 1) => new() {
            M11 = exposure, M22 = exposure, M33 = exposure, M44 = 1,
        };

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Matrix5x4 Brightness(float brightness = 0) => new() {
            M11 = 1, M22 = 1, M33 = 1, M44 = 1,
            M51 = brightness, M52 = brightness, M53 = brightness,
        };

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Matrix5x4 Saturation(float saturation = 1) {
            float white = (1 + saturation + saturation) / 3;
            float black = (1 - saturation) / 3;
            return new Matrix5x4 {
                M11 = white, M12 = black, M13 = black,
                M21 = black, M22 = white, M23 = black,
                M31 = black, M32 = black, M33 = white,
                M44 = 1,
            };
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Matrix5x4 HueRotate(float angle = 0) {
            float x = Math.Clamp(Math.Abs(angle - 1.5f) - 0.5f, 0, 1);
            float y = 1 - Math.Clamp(Math.Abs(angle - 1), 0, 1);
            float z = 1 - Math.Clamp(Math.Abs(angle - 2), 0, 1);
            return new Matrix5x4 {
                M11 = x, M12 = z, M13 = y,
                M21 = y, M22 = x, M23 = z,
                M31 = z, M32 = y, M33 = x,
                M44 = 1,
            };
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Matrix5x4 HSB(float angle = 0, float saturation = 1, float brightness = 0) {
            float x = Math.Clamp(Math.Abs(angle - 1.5f) - 0.5f, 0, 1);
            float y = 1 - Math.Clamp(Math.Abs(angle - 1), 0, 1);
            float z = 1 - Math.Clamp(Math.Abs(angle - 2), 0, 1);

            float white = (1 + saturation + saturation) / 3;
            float black = (1 - saturation) / 3;

            float xs = x * white + (1 - x) * black;
            float ys = y * white + (1 - y) * black;
            float zs = z * white + (1 - z) * black;

            return new Matrix5x4 {
                M11 = xs, M12 = zs, M13 = ys,
                M21 = ys, M22 = xs, M23 = zs,
                M31 = zs, M32 = ys, M33 = xs,
                M44 = 1,
                M51 = brightness, M52 = brightness, M53 = brightness,
            };
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Matrix5x4 Contrast(float contrast = 1) {
            float offset = 0.5f - contrast / 2;
            return new Matrix5x4 {
                M11 = contrast, M22 = contrast, M33 = contrast, M44 = 1,
                M51 = offset, M52 = offset, M53 = offset,
            };
        }
    }
}
