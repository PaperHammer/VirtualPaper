using System.Collections.Concurrent;
using System.Numerics;
using Microsoft.Graphics.Canvas;
using Microsoft.Graphics.Canvas.Brushes;
using Microsoft.Graphics.Canvas.Effects;
using Microsoft.UI;
using Windows.Foundation;
using Windows.UI;

//namespace BuiltIn.InkSystem.Core.Services {
//public static class BrushManager {
//    static BrushManager() {
//        _baseTextures = LoadBaseTextures();
//    }

//    public static ICanvasImage GetImage(BrushGenerateArgs args, CanvasDevice device) {
//        if (args.Type == BrushType.General)
//            return GetSolidBrush(args.Color, args.Size, args.Shape, device);

//        return _texturedBrushCache.GetOrAdd(args, key => CreateTexturedBrush(key, device));
//    }

//    public static void ClearCache() {
//        DisposeAll(_solidBrushCache.Values);
//        DisposeAll(_texturedBrushCache.Values);
//        DisposeAll(_sharedTextureCache.Values);
//    }

//    #region Core brush generation
//    private static CanvasBitmap GetSolidBrush(Color color, float size, BrushShape shape, CanvasDevice device) {
//        return _solidBrushCache.GetOrAdd((color, size, shape), _ => {
//            var renderTarget = new CanvasRenderTarget(device, size, size, 96);
//            using (var ds = renderTarget.CreateDrawingSession()) {
//                ds.Clear(Colors.Transparent);
//                DrawShape(ds, shape, size, color);
//            }
//            return renderTarget;
//        });
//    }

//    private static CanvasBitmap CreateTexturedBrush(BrushGenerateArgs args, CanvasDevice device) {
//        return args.Type switch {
//            BrushType.Calligraphy => CreateCalligraphyBrush(args, device),
//            BrushType.Airbrush => CreateAirbrush(args, device),
//            BrushType.Watercolor => CreateWatercolorBrush(args, device),
//            BrushType.Oil => CreateOilBrush(args, device),
//            BrushType.Marker => CreateMarkerBrush(args, device),
//            BrushType.Pencil => CreatePencilBrush(args, device),
//            _ => GetSolidBrush(args.Color, args.Size, args.Shape, device)
//        };
//    }
//    #endregion

//    #region Various types of brushes are implemented
//    private static CanvasRenderTarget CreateCalligraphyBrush(BrushGenerateArgs args, CanvasDevice device) {
//        var renderTarget = new CanvasRenderTarget(device, args.Size, args.Size, 96);
//        using (var ds = renderTarget.CreateDrawingSession()) {
//            // 基础形状
//            if (args.Shape == BrushShape.Circle) {
//                float ratio = 0.3f + 0.4f * args.Hardness;
//                ds.FillEllipse(args.Size / 2, args.Size / 2,
//                              args.Size / 2 * ratio,
//                              args.Size / 2,
//                              args.Color);
//            }
//            else {
//                float cornerRadius = args.Size * 0.1f;
//                ds.FillRoundedRectangle(0, 0, args.Size, args.Size,
//                                       cornerRadius, cornerRadius,
//                                       args.Color);
//            }

//            // 毛笔纹理
//            using var noise = GetSharedTexture("calligraphy_noise");
//            using var brush = new CanvasImageBrush(device, noise) {
//                Opacity = 0.2f,
//                Transform = Matrix3x2.CreateScale(args.Size / 256f)
//            };
//            DrawShape(ds, args.Shape, args.Size * 0.9f, brush);
//        }
//        return renderTarget;
//    }

//    private static CanvasRenderTarget CreateAirbrush(BrushGenerateArgs args, CanvasDevice device) {
//        // 喷枪强制圆形
//        if (args.Shape != BrushShape.Circle)
//            args = args with { Shape = BrushShape.Circle };

//        var renderTarget = new CanvasRenderTarget(device, args.Size, args.Size, 96);
//        using (var ds = renderTarget.CreateDrawingSession()) {
//            var stops = new[]
//            {
//                new CanvasGradientStop { Color = Color.FromArgb(0, args.Color.R, args.Color.G, args.Color.B), Position = 0 },
//                new CanvasGradientStop { Color = Color.FromArgb(150, args.Color.R, args.Color.G, args.Color.B), Position = 0.7f },
//                new CanvasGradientStop { Color = args.Color, Position = 1 }
//            };

//            using var brush = new CanvasRadialGradientBrush(device, stops) {
//                Center = new Vector2(args.Size / 2),
//                RadiusX = args.Size / 2,
//                RadiusY = args.Size / 2,
//                Opacity = args.Flow
//            };
//            ds.FillCircle(args.Size / 2, args.Size / 2, args.Size / 2, brush);
//        }
//        return renderTarget;
//    }

//    private static CanvasRenderTarget CreateWatercolorBrush(BrushGenerateArgs args, CanvasDevice device) {
//        var renderTarget = new CanvasRenderTarget(device, args.Size, args.Size, 96);
//        using (var ds = renderTarget.CreateDrawingSession()) {
//            // 纸张纹理
//            using var paperTex = _baseTextures[BrushType.Watercolor];
//            using var paperBrush = new CanvasImageBrush(device, paperTex) {
//                SourceRectangle = new Rect(0, 0, args.Size, args.Size),
//                Transform = Matrix3x2.CreateScale(args.Size / 512f),
//                Opacity = 0.5f
//            };
//            DrawShape(ds, args.Shape, args.Size, paperBrush);

//            // 水彩色层
//            ds.Blend = CanvasBlend.SourceOver;
//            DrawShape(ds, args.Shape, args.Size * 0.8f, args.Color);
//        }
//        return ApplyWatercolorEffect(renderTarget, args.Flow);
//    }

//    private static CanvasRenderTarget CreateOilBrush(BrushGenerateArgs args, CanvasDevice device) {
//        var renderTarget = new CanvasRenderTarget(device, args.Size, args.Size, 96);
//        using (var ds = renderTarget.CreateDrawingSession()) {
//            // 基础颜料层
//            DrawShape(ds, args.Shape, args.Size, args.Color);

//            // 笔触纹理
//            using var strokeTex = _baseTextures[BrushType.Oil];
//            using var strokeBrush = new CanvasImageBrush(device, strokeTex) {
//                Opacity = 0.3f * args.Hardness,
//                Transform = Matrix3x2.CreateRotation(Random.Shared.NextSingle() * MathF.PI) *
//                           Matrix3x2.CreateScale(args.Size / 128f)
//            };
//            ds.Blend = CanvasBlend.SourceOver;
//            DrawShape(ds, args.Shape, args.Size * 0.9f, strokeBrush);
//        }
//        return renderTarget;
//    }

//    private static CanvasRenderTarget CreateMarkerBrush(BrushGenerateArgs args, CanvasDevice device) {
//        var renderTarget = new CanvasRenderTarget(device, args.Size, args.Size, 96);
//        using (var ds = renderTarget.CreateDrawingSession()) {
//            byte alpha = (byte)(args.Color.A * args.Flow * 0.7f);
//            var color = Color.FromArgb(alpha, args.Color.R, args.Color.G, args.Color.B);
//            DrawShape(ds, args.Shape, args.Size, color);

//            // 边缘锐化
//            using var effect = new EdgeDetectionEffect {
//                Source = renderTarget,
//                Amount = 0.2f,
//                BlurAmount = 0.5f
//            };
//            ds.DrawImage(effect);
//        }
//        return renderTarget;
//    }

//    private static CanvasRenderTarget CreatePencilBrush(BrushGenerateArgs args, CanvasDevice device) {
//        var renderTarget = new CanvasRenderTarget(device, args.Size, args.Size, 96);
//        using (var ds = renderTarget.CreateDrawingSession()) {
//            // 灰度转换
//            byte gray = (byte)(args.Color.R * 0.3 + args.Color.G * 0.59 + args.Color.B * 0.11);
//            var baseColor = Color.FromArgb(args.Color.A, gray, gray, gray);

//            // 噪点纹理
//            using var noise = _baseTextures[BrushType.Pencil];
//            using var brush = new CanvasImageBrush(device, noise) {
//                Transform = Matrix3x2.CreateScale(args.Size / 256f),
//                Opacity = args.Hardness
//            };
//            DrawShape(ds, args.Shape, args.Size, brush);
//            DrawShape(ds, args.Shape, args.Size, baseColor);
//        }
//        return renderTarget;
//    }
//    #endregion

//    #region shape drawing
//    private static void DrawShape(CanvasDrawingSession ds, BrushShape shape, float size, ICanvasBrush brush) {
//        float center = size / 2;
//        switch (shape) {
//            case BrushShape.Circle:
//                ds.FillCircle(center, center, center, brush);
//                break;
//            case BrushShape.Rectangle:
//                ds.FillRectangle(0, 0, size, size, brush);
//                break;
//        }
//    }

//    private static void DrawShape(CanvasDrawingSession ds, BrushShape shape, float size, Color color) {
//        float center = size / 2;
//        switch (shape) {
//            case BrushShape.Circle:
//                ds.FillCircle(center, center, center, color);
//                break;
//            case BrushShape.Rectangle:
//                ds.FillRectangle(0, 0, size, size, color);
//                break;
//        }
//    }
//    #endregion

//    #region special effects processing
//    private static CanvasRenderTarget ApplyWatercolorEffect(CanvasBitmap source, float wetness) {
//        var device = source.Device;
//        var result = new CanvasRenderTarget(device, (float)source.Size.Width, (float)source.Size.Height, 96);

//        using (var ds = result.CreateDrawingSession())
//        using (var blur = new GaussianBlurEffect {
//            Source = source,
//            BlurAmount = 2f * wetness,
//            BorderMode = EffectBorderMode.Soft
//        }) {
//            ds.DrawImage(blur);
//            ds.DrawImage(source); // 增强中心浓度
//        }
//        return result;
//    }
//    #endregion

//    #region resource management
//    private static CanvasBitmap GetSharedTexture(string key) {
//        return _sharedTextureCache.GetOrAdd(key, _ => {
//            return key switch {
//                "calligraphy_noise" => GenerateNoiseTexture(256, 0.2f),
//                _ => throw new KeyNotFoundException()
//            };
//        });
//    }

//    private static CanvasRenderTarget GenerateNoiseTexture(int size, float intensity) {
//        var device = CanvasDevice.GetSharedDevice();
//        var texture = new CanvasRenderTarget(device, size, size, 96);
//        using (var ds = texture.CreateDrawingSession()) {
//            var rnd = new Random();
//            for (int i = 0; i < size * size * intensity; i++) {
//                byte gray = (byte)rnd.Next(100, 250);
//                ds.DrawCircle(
//                    rnd.Next(0, size),
//                    rnd.Next(0, size),
//                    rnd.NextSingle() * 1.5f,
//                    Color.FromArgb(255, gray, gray, gray));
//            }
//        }
//        return texture;
//    }

//    private static Dictionary<BrushType, CanvasBitmap> LoadBaseTextures() {
//        var device = CanvasDevice.GetSharedDevice();
//        return new Dictionary<BrushType, CanvasBitmap> {
//            [BrushType.Watercolor] = GenerateProceduralTexture(device, GenerateWatercolorPaper),
//            [BrushType.Oil] = GenerateProceduralTexture(device, GenerateOilStroke),
//            [BrushType.Pencil] = GenerateProceduralTexture(device, GeneratePencilNoise)
//        };
//    }

//    private static CanvasRenderTarget GenerateProceduralTexture(CanvasDevice device, Action<CanvasDrawingSession> generator) {
//        var texture = new CanvasRenderTarget(device, 512, 512, 96);
//        using (var ds = texture.CreateDrawingSession()) {
//            generator(ds);
//        }
//        return texture;
//    }

//    private static void GenerateWatercolorPaper(CanvasDrawingSession ds) {
//        var rnd = new Random();
//        for (int i = 0; i < 5000; i++) {
//            var x = rnd.Next(0, 512);
//            var y = rnd.Next(0, 512);
//            var size = rnd.Next(1, 5);
//            ds.FillCircle(x, y, size, Color.FromArgb(30, 220, 220, 220));
//        }
//    }

//    private static void GenerateOilStroke(CanvasDrawingSession ds) {
//        var colors = new[] { Colors.White, Colors.LightGray, Colors.Gray };
//        var rnd = new Random();
//        for (int i = 0; i < 200; i++) {
//            var color = colors[rnd.Next(colors.Length)];
//            var width = rnd.Next(5, 20);
//            var height = rnd.Next(30, 100);
//            var rotation = rnd.NextSingle() * MathF.PI;

//            ds.Transform = Matrix3x2.CreateRotation(rotation) *
//                          Matrix3x2.CreateTranslation(rnd.Next(0, 512), rnd.Next(0, 512));
//            ds.FillRectangle(-width / 2, -height / 2, width, height, color);
//        }
//    }

//    private static void GeneratePencilNoise(CanvasDrawingSession ds) {
//        var rnd = new Random();
//        for (int i = 0; i < 10000; i++) {
//            byte gray = (byte)rnd.Next(150, 250);
//            ds.DrawCircle(
//                rnd.Next(0, 512),
//                rnd.Next(0, 512),
//                rnd.NextSingle() * 1.5f,
//                Color.FromArgb(255, gray, gray, gray));
//        }
//    }

//    private static void DisposeAll(IEnumerable<CanvasBitmap> textures) {
//        foreach (var texture in textures) {
//            texture.Dispose();
//        }
//    }
//    #endregion

//    private static readonly ConcurrentDictionary<(Color, float, BrushShape), CanvasBitmap> _solidBrushCache = new();
//    private static readonly ConcurrentDictionary<BrushGenerateArgs, CanvasBitmap> _texturedBrushCache = new();
//    private static readonly ConcurrentDictionary<string, CanvasBitmap> _sharedTextureCache = new();
//    private static readonly IReadOnlyDictionary<BrushType, CanvasBitmap> _baseTextures;
//}
using System;
using System.Collections.Generic;
using System.Numerics;
using Microsoft.Graphics.Canvas;
using Microsoft.Graphics.Canvas.Brushes;
using Microsoft.Graphics.Canvas.Effects;
using Microsoft.Graphics.Canvas.Geometry;
using Windows.Foundation;
using Windows.UI;

namespace BuiltIn.InkSystem.Core.Services {
    /// <summary>
    /// 笔刷参数记录类型（不可变）
    /// </summary>
    public sealed record BrushGenerateArgs(
        Color Color,
        BrushType Type,
        BrushShape Shape,
        float Size = 1f,
        float Hardness = 1.0f,
        float Flow = 1.0f,
        float Angle = 0f) // 仅书法笔需要角度
    {
        // 自动实现的Equals和GetHashCode
    }

    /// <summary>
    /// 笔刷管理器（静态工具类）
    /// </summary>
    public static class BrushManager {
        private static readonly IReadOnlyDictionary<BrushType, CanvasBitmap> _baseTextures;
        private static readonly Dictionary<(Color, float, BrushShape), CanvasSolidColorBrush> _solidBrushCache = new();
        private static readonly Dictionary<BrushGenerateArgs, ICanvasBrush> _brushCache = new();

        static BrushManager() {
            _baseTextures = LoadBaseTextures();
        }

        /// <summary>
        /// 获取或创建笔刷
        /// </summary>
        public static ICanvasBrush GetBrush(BrushGenerateArgs args, CanvasDevice device) {
            if (_brushCache.TryGetValue(args, out var cachedBrush))
                return cachedBrush;

            var brush = args.Type switch {
                BrushType.General => CreateSolidColorBrush(args.Color),
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
        private static ICanvasBrush CreateSolidColorBrush(Color color) {
            var key = (color, 1f, BrushShape.Circle); // 尺寸和形状对纯色笔刷无影响
            if (!_solidBrushCache.TryGetValue(key, out var brush)) {
                brush = new CanvasSolidColorBrush(CanvasDevice.GetSharedDevice(), color);
                _solidBrushCache[key] = brush;
            }
            return brush;
        }

        private static ICanvasBrush CreateCalligraphyBrush(BrushGenerateArgs args, CanvasDevice device) {
            var texture = _baseTextures[BrushType.Calligraphy];
            return new CanvasImageBrush(device, texture) {
                Transform = Matrix3x2.CreateRotation(args.Angle) *
                           Matrix3x2.CreateScale(args.Size / (float)texture.Size.Width),
                ExtendX = CanvasEdgeBehavior.Wrap,
                Opacity = args.Flow,
                Interpolation = CanvasImageInterpolation.HighQualityCubic
            };
        }

        private static ICanvasBrush CreateAirbrush(BrushGenerateArgs args, CanvasDevice device) {
            var stops = new[]
            {
                new CanvasGradientStop { Color = Color.FromArgb(0, args.Color.R, args.Color.G, args.Color.B), Position = 0 },
                new CanvasGradientStop { Color = args.Color, Position = 1 }
            };

            return new CanvasRadialGradientBrush(device, stops) {
                Center = new Vector2(args.Size / 2),
                RadiusX = args.Size / 2,
                RadiusY = args.Size / 2,
                Opacity = args.Flow
            };
        }

        private static ICanvasBrush CreateOilBrush(BrushGenerateArgs args, CanvasDevice device) {
            using var cmdList = new CanvasCommandList(device);
            using (var ds = cmdList.CreateDrawingSession()) {
                ds.FillCircle(args.Size / 2, args.Size / 2, args.Size / 2, args.Color);
                ds.DrawImage(_baseTextures[BrushType.Oil]);
            }

            return new CanvasImageBrush(device, cmdList) {
                Transform = Matrix3x2.CreateScale(args.Size / 100f),
                ExtendX = CanvasEdgeBehavior.Mirror,
                Opacity = args.Hardness
            };
        }

        private static ICanvasBrush CreateWatercolorBrush(BrushGenerateArgs args, CanvasDevice device) {
            var texture = _baseTextures[BrushType.Watercolor];
            return new CanvasImageBrush(device, texture) {
                Transform = Matrix3x2.CreateScale(args.Size / (float)texture.Size.Width),
                Opacity = args.Flow * 0.7f,
                ExtendX = CanvasEdgeBehavior.Wrap,
                Interpolation = CanvasImageInterpolation.MultiSampleLinear
            };
        }

        private static ICanvasBrush CreateMarkerBrush(BrushGenerateArgs args, CanvasDevice device) {
            var stops = new[]
            {
                new CanvasGradientStop { Color = Color.FromArgb(150, args.Color.R, args.Color.G, args.Color.B), Position = 0 },
                new CanvasGradientStop { Color = Color.FromArgb(50, args.Color.R, args.Color.G, args.Color.B), Position = 1 }
            };

            return new CanvasLinearGradientBrush(device, stops) {
                StartPoint = new Vector2(0, 0),
                EndPoint = new Vector2(args.Size, 0),
                Opacity = args.Flow
            };
        }

        private static ICanvasBrush CreatePencilBrush(BrushGenerateArgs args, CanvasDevice device) {
            // 计算灰度颜色（RGB转灰度）
            byte gray = (byte)(args.Color.R * 0.3 + args.Color.G * 0.59 + args.Color.B * 0.11);
            var baseColor = Color.FromArgb(args.Color.A, gray, gray, gray);

            // 创建基础笔刷（灰度底色）
            var baseBrush = new CanvasSolidColorBrush(device, baseColor) {
                Opacity = args.Flow * 0.8f  // 基础层占比80%
            };

            // 创建纹理笔刷（铅笔噪点）
            var textureBrush = new CanvasImageBrush(device, _baseTextures[BrushType.Pencil]) {
                Transform = Matrix3x2.CreateScale(args.Size / 50f),
                Opacity = args.Hardness * 0.5f,  // 纹理层占比50%
                ExtendX = CanvasEdgeBehavior.Wrap,
                Interpolation = CanvasImageInterpolation.HighQualityCubic
            };

            // 构建复合笔刷
            return new CompositeBrush(baseBrush, textureBrush) {
                // 全局控制参数
                Opacity = args.Flow,  // 总透明度
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
    }

    /// <summary>
    /// 笔刷形状枚举
    /// </summary>
    public enum BrushShape {
        Circle,
        Rectangle,
        RoundedRect
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
        Pencil
    }
}
//}