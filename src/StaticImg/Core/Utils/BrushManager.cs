using System;
using System.Collections.Generic;
using System.Numerics;
using Microsoft.Graphics.Canvas;
using Microsoft.Graphics.Canvas.Brushes;
using Microsoft.UI;
using Windows.UI;

namespace Workloads.Creation.StaticImg.Core.Utils {
    /// <summary>
    /// 笔刷参数记录类型
    /// </summary>
    public sealed record BrushGenerateArgs(
        Color BrushColor,
        BrushType Type,
        float Thickness = 1f,
        float Hardness = 1f,
        float Opacity = 1f,
        float Angle = 0f); // 仅书法笔需要角度

    /// <summary>
    /// 笔刷管理器（静态工具类）
    /// </summary>
    internal static class BrushManager {
        static BrushManager() {
            _baseTextures = LoadBaseTextures();
        }

        /// <summary>
        /// 获取或创建笔刷
        /// </summary>
        public static ICanvasBrush GetBrush(BrushGenerateArgs args, CanvasDevice device) {
            if (_brushCache.TryGetValue(args, out var cachedBrush))
                return cachedBrush;

            ICanvasBrush brush = args.Type switch {
                BrushType.General or BrushType.Eraser => CreateSolidColorBrush(args),
                BrushType.Calligraphy => CreateCalligraphyBrush(args, device),
                BrushType.Airbrush => CreateAirbrush(args, device),
                BrushType.Oil => CreateOilBrush(args, device),
                BrushType.Watercolor => CreateWatercolorBrush(args, device),
                BrushType.Marker => CreateMarkerBrush(args, device),
                BrushType.Pencil => CreatePencilBrush(args, device),
                _ => throw new NotSupportedException($"Unsupported brush type: {args.Type}")
            };

            _brushCache[args] = brush;
            return brush;
        }

        /// <summary>
        /// 清空所有缓存
        /// </summary>
        public static void ClearCache() {
            foreach (var brush in _solidBrushCache.Values)
                brush.Dispose();
            foreach (var brush in _brushCache.Values)
                brush.Dispose();

            _solidBrushCache.Clear();
            _brushCache.Clear();
        }

        #region 核心笔刷创建方法
        private static CanvasSolidColorBrush CreateSolidColorBrush(BrushGenerateArgs args) {
            var key = (args.BrushColor, args.Opacity);
            if (!_solidBrushCache.TryGetValue(key, out var brush)) {
                brush = new CanvasSolidColorBrush(CanvasDevice.GetSharedDevice(), args.BrushColor) {
                    Opacity = args.Opacity
                };
                _solidBrushCache[key] = brush;
            }
            return brush;
        }

        private static CanvasImageBrush CreateCalligraphyBrush(BrushGenerateArgs args, CanvasDevice device) {
            var texture = _baseTextures[BrushType.Calligraphy];
            return new CanvasImageBrush(device, texture) {
                Transform = Matrix3x2.CreateRotation(args.Angle) *
                           Matrix3x2.CreateScale(args.Thickness / (float)texture.Size.Width),
                ExtendX = CanvasEdgeBehavior.Wrap,
                Opacity = args.Opacity,
                Interpolation = CanvasImageInterpolation.HighQualityCubic
            };
        }

        private static CanvasRadialGradientBrush CreateAirbrush(BrushGenerateArgs args, CanvasDevice device) {
            var stops = new[]
            {
                new CanvasGradientStop { Color = Color.FromArgb(0, args.BrushColor.R, args.BrushColor.G, args.BrushColor.B), Position = 0 },
                new CanvasGradientStop { Color = args.BrushColor, Position = 1 }
            };

            return new CanvasRadialGradientBrush(device, stops) {
                Center = new Vector2(args.Thickness / 2),
                RadiusX = args.Thickness / 2,
                RadiusY = args.Thickness / 2,
                Opacity = args.Opacity
            };
        }

        private static CanvasImageBrush CreateOilBrush(BrushGenerateArgs args, CanvasDevice device) {
            using var cmdList = new CanvasCommandList(device);
            using (var ds = cmdList.CreateDrawingSession()) {
                ds.FillCircle(args.Thickness / 2, args.Thickness / 2, args.Thickness / 2, args.BrushColor);
                ds.DrawImage(_baseTextures[BrushType.Oil]);
            }

            return new CanvasImageBrush(device, cmdList) {
                Transform = Matrix3x2.CreateScale(args.Thickness / 100f),
                ExtendX = CanvasEdgeBehavior.Mirror,
                Opacity = args.Hardness
            };
        }

        private static CanvasImageBrush CreateWatercolorBrush(BrushGenerateArgs args, CanvasDevice device) {
            var texture = _baseTextures[BrushType.Watercolor];
            return new CanvasImageBrush(device, texture) {
                Transform = Matrix3x2.CreateScale(args.Thickness / (float)texture.Size.Width),
                Opacity = args.Opacity * 0.7f,
                ExtendX = CanvasEdgeBehavior.Wrap,
                Interpolation = CanvasImageInterpolation.MultiSampleLinear
            };
        }

        private static CanvasLinearGradientBrush CreateMarkerBrush(BrushGenerateArgs args, CanvasDevice device) {
            var stops = new[]
            {
                new CanvasGradientStop { Color = Color.FromArgb(150, args.BrushColor.R, args.BrushColor.G, args.BrushColor.B), Position = 0 },
                new CanvasGradientStop { Color = Color.FromArgb(50, args.BrushColor.R, args.BrushColor.G, args.BrushColor.B), Position = 1 }
            };

            return new CanvasLinearGradientBrush(device, stops) {
                StartPoint = new Vector2(0, 0),
                EndPoint = new Vector2(args.Thickness, 0),
                Opacity = args.Opacity
            };
        }

        private static CompositeBrush CreatePencilBrush(BrushGenerateArgs args, CanvasDevice device) {
            // 计算灰度颜色（RGB转灰度）
            byte gray = (byte)(args.BrushColor.R * 0.3 + args.BrushColor.G * 0.59 + args.BrushColor.B * 0.11);
            var baseColor = Color.FromArgb(args.BrushColor.A, gray, gray, gray);

            // 创建基础笔刷（灰度底色）
            var baseBrush = new CanvasSolidColorBrush(device, baseColor) {
                Opacity = args.Opacity * 0.8f  // 基础层占比80%
            };

            // 创建纹理笔刷（铅笔噪点）
            var textureBrush = new CanvasImageBrush(device, _baseTextures[BrushType.Pencil]) {
                Transform = Matrix3x2.CreateScale(args.Thickness / 50f),
                Opacity = args.Hardness * 0.5f,  // 纹理层占比50%
                ExtendX = CanvasEdgeBehavior.Wrap,
                Interpolation = CanvasImageInterpolation.HighQualityCubic
            };

            // 构建复合笔刷
            return new CompositeBrush(baseBrush, textureBrush) {
                // 全局控制参数
                Opacity = args.Opacity,  // 总透明度
                Transform = Matrix3x2.CreateRotation(args.Angle)  // 统一旋转
            };
        }

        #endregion

        #region 纹理资源管理

        private static IReadOnlyDictionary<BrushType, CanvasBitmap> LoadBaseTextures() {
            var device = CanvasDevice.GetSharedDevice();
            return new Dictionary<BrushType, CanvasBitmap> {
                [BrushType.Calligraphy] = GenerateCalligraphyTexture(device),
                [BrushType.Oil] = GenerateOilTexture(device),
                [BrushType.Watercolor] = GenerateWatercolorTexture(device),
                [BrushType.Pencil] = GeneratePencilTexture(device)
            };
        }

        private static CanvasBitmap GenerateCalligraphyTexture(CanvasDevice device) {
            var renderTarget = new CanvasRenderTarget(device, 256, 256, 96);
            using (var ds = renderTarget.CreateDrawingSession()) {
                // 毛笔笔触纹理（椭圆+噪点）
                ds.FillEllipse(128, 128, 100, 30, Colors.Black);

                // 添加笔触细节
                var rnd = new Random();
                for (int i = 0; i < 500; i++) {
                    float x = 128 + rnd.Next(-80, 80);
                    float y = 128 + rnd.Next(-20, 20);
                    ds.DrawCircle(x, y, rnd.Next(1, 3), Colors.Gray);
                }
            }
            return renderTarget;
        }

        private static CanvasBitmap GenerateOilTexture(CanvasDevice device) {
            var renderTarget = new CanvasRenderTarget(device, 512, 512, 96);
            using (var ds = renderTarget.CreateDrawingSession()) {
                // 油画颜料厚涂纹理
                var rnd = new Random();
                for (int i = 0; i < 200; i++) {
                    var color = Color.FromArgb(200,
                        (byte)rnd.Next(200, 255),
                        (byte)rnd.Next(200, 255),
                        (byte)rnd.Next(200, 255));

                    float x = rnd.Next(0, 512);
                    float y = rnd.Next(0, 512);
                    float w = rnd.Next(5, 20);
                    float h = rnd.Next(30, 100);

                    ds.FillRectangle(x, y, w, h, color);
                }
            }
            return renderTarget;
        }

        private static CanvasBitmap GenerateWatercolorTexture(CanvasDevice device) {
            var renderTarget = new CanvasRenderTarget(device, 512, 512, 96);
            using (var ds = renderTarget.CreateDrawingSession()) {
                // 水彩湿边效果
                var rnd = new Random();
                for (int i = 0; i < 1000; i++) {
                    var color = Color.FromArgb(
                        (byte)rnd.Next(30, 80),
                        (byte)rnd.Next(200, 240),
                        (byte)rnd.Next(200, 240),
                        (byte)rnd.Next(200, 240));

                    float x = rnd.Next(0, 512);
                    float y = rnd.Next(0, 512);
                    float r = rnd.Next(10, 30);

                    ds.FillCircle(x, y, r, color);
                }
            }
            return renderTarget;
        }

        private static CanvasBitmap GeneratePencilTexture(CanvasDevice device) {
            var renderTarget = new CanvasRenderTarget(device, 256, 256, 96);
            using (var ds = renderTarget.CreateDrawingSession()) {
                // 铅笔噪点纹理
                var rnd = new Random();
                for (int i = 0; i < 5000; i++) {
                    byte gray = (byte)rnd.Next(150, 250);
                    float x = rnd.Next(0, 256);
                    float y = rnd.Next(0, 256);
                    float r = rnd.NextSingle() * 1.5f;

                    ds.DrawCircle(x, y, r, Color.FromArgb(255, gray, gray, gray));
                }
            }
            return renderTarget;
        }

        #endregion

        #region 辅助类

        /// <summary>
        /// 复合笔刷（支持透明度与变换）
        /// </summary>
        private class CompositeBrush : ICanvasBrush {
            private readonly ICanvasBrush _baseBrush;
            private readonly ICanvasBrush _textureBrush;
            private float _opacity = 1f;
            private Matrix3x2 _transform = Matrix3x2.Identity;

            public CanvasDevice Device => _baseBrush.Device;

            /// <summary>
            /// 组合笔刷的透明度（0-1）
            /// </summary>
            public float Opacity {
                get => _opacity;
                set {
                    _opacity = Math.Clamp(value, 0f, 1f);
                    // 同步更新子笔刷透明度（按比例分配）
                    if (_baseBrush is ICanvasBrush baseBrush)
                        baseBrush.Opacity = _opacity * 0.7f; // 基础层70%
                    if (_textureBrush is ICanvasBrush textureBrush)
                        textureBrush.Opacity = _opacity * 0.3f; // 纹理层30%
                }
            }

            /// <summary>
            /// 组合笔刷的变换矩阵
            /// </summary>
            public Matrix3x2 Transform {
                get => _transform;
                set {
                    _transform = value;
                    // 同步应用变换到子笔刷
                    if (_baseBrush is ICanvasBrush baseBrush)
                        baseBrush.Transform = value;
                    if (_textureBrush is ICanvasBrush textureBrush)
                        textureBrush.Transform = value * Matrix3x2.CreateScale(1.1f); // 纹理层略微放大
                }
            }

            public CompositeBrush(ICanvasBrush baseBrush, ICanvasBrush textureBrush) {
                _baseBrush = baseBrush ?? throw new ArgumentNullException(nameof(baseBrush));
                _textureBrush = textureBrush ?? throw new ArgumentNullException(nameof(textureBrush));

                // 初始化默认状态
                Opacity = 1f;
                Transform = Matrix3x2.Identity;
            }

            public void Dispose() {
                _baseBrush.Dispose();
                _textureBrush.Dispose();
            }
        }
        #endregion

        private static readonly IReadOnlyDictionary<BrushType, CanvasBitmap> _baseTextures;
        private static readonly Dictionary<(Color, float Opacity), CanvasSolidColorBrush> _solidBrushCache = [];
        private static readonly Dictionary<BrushGenerateArgs, ICanvasBrush> _brushCache = [];
    }

    /// <summary>
    /// 笔刷类型枚举
    /// </summary>
    public enum BrushType {
        General,
        Calligraphy,
        Airbrush,
        Oil,
        Watercolor,
        Marker,
        Pencil,
        Eraser,
    }
}
