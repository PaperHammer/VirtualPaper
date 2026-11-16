using Microsoft.Graphics.Canvas;
using System.Numerics;
using VirtualPaper.Shader.Core;
using VirtualPaper.Shader.Models;
using Windows.UI;

namespace VirtualPaper.Shader.Test;

[TestClass]
[DoNotParallelize]
public sealed class EffectApplierRenderingTests {
    [TestInitialize]
    public void Initialize() {
        _device = new CanvasDevice(forceSoftwareRenderer: true);
        _source = CanvasBitmap.CreateFromColors(_device, Enumerable.Range(0, 256)
            .Select(i => Color.FromArgb(255, (byte)(30 + i % 180), (byte)(50 + i % 150), (byte)(70 + i % 120)))
            .ToArray(), 16, 16);
    }

    [TestCleanup]
    public void Cleanup() { _source.Dispose(); _device.Dispose(); }

    public static IEnumerable<object[]> BuiltInEffects() => Enum.GetValues<ShaderType>()
        .Where(type => type != ShaderType.None && !ShaderTypeManager.IsCustomShader(type))
        .Select(type => new object[] { type });

    [TestMethod]
    [DynamicData(nameof(BuiltInEffects))]
    public void BuiltInEffects_RenderRepeatably_WithoutMutatingTheSource(ShaderType type) {
        byte[] original = _source.GetPixelBytes();
        var parameters = new EffectParams {
            Value = 25, Value2 = 35, Value3 = 45, Value4 = 50,
            Color1 = new Vector4(0.1f, 0.2f, 0.3f, 1), Color2 = Vector4.One
        };
        var effect = EffectApplier.Apply(type, parameters, _source);
        var pixels = Render(effect);
        Assert.HasCount(256, pixels);
        Assert.IsTrue(pixels.Any(pixel => pixel.A > 0), $"{type} discarded the entire image.");
        CollectionAssert.AreEqual(pixels, Render(effect), $"{type} changed without an input change.");
        CollectionAssert.AreEqual(original, _source.GetPixelBytes(), $"{type} modified the source bitmap.");
        DisposeEffect(effect);
    }

    [TestMethod]
    public void InvertTwice_RestoresOriginalColors_AndGrayscalePreservesAlpha() {
        var first = EffectApplier.Apply(ShaderType.Invert, default, _source);
        var second = EffectApplier.Apply(ShaderType.Invert, default, first);
        CollectionAssert.AreEqual(Render(_source), Render(second));
        var gray = EffectApplier.Apply(ShaderType.Grayscale, default, _source);
        foreach (var pixel in Render(gray)) {
            Assert.AreEqual(pixel.R, pixel.G);
            Assert.AreEqual(pixel.G, pixel.B);
            Assert.AreEqual((byte)255, pixel.A);
        }
        DisposeEffect(gray); DisposeEffect(second); DisposeEffect(first);
    }

    [TestMethod]
    [DataRow(ShaderType.BlendMultiply)]
    [DataRow(ShaderType.BlendScreen)]
    [DataRow(ShaderType.BlendOverlay)]
    [DataRow(ShaderType.BlendSoftLight)]
    [DataRow(ShaderType.Shadow)]
    [DataRow(ShaderType.Glow)]
    public void ZeroEffectOpacity_PreservesTheOriginal_AndFullOpacityChangesIt(ShaderType type) {
        var parameters = new EffectParams { Value = 25, Value2 = 2, Value3 = 2, Value4 = 0 };
        if (type is not (ShaderType.Shadow or ShaderType.Glow)) parameters.Value = 0;
        var zero = EffectApplier.Apply(type, parameters, _source);
        var original = Render(_source, 32);
        CollectionAssert.AreEqual(original, Render(zero, 32), $"{type} changed pixels at zero opacity.");
        if (type is ShaderType.Shadow or ShaderType.Glow) parameters.Value4 = 100;
        else parameters.Value = 100;
        var full = EffectApplier.Apply(type, parameters, _source);
        Assert.IsFalse(original.SequenceEqual(Render(full, 32)), $"{type} ignored full opacity.");
        DisposeEffect(full); DisposeEffect(zero);
    }

    [TestMethod]
    public void Brightness_HandlesNeutralBrighterAndDarkerValues() {
        var original = Render(_source);
        foreach (var (black, white, direction) in new[] { (0f, 255f, 0), (50f, 255f, 1), (0f, 200f, -1) }) {
            var effect = EffectApplier.Apply(ShaderType.Brightness, new() { Value = black, Value2 = white }, _source);
            var changed = Render(effect);
            for (int i = 0; i < original.Length; i++) {
                Assert.AreEqual(original[i].A, changed[i].A);
                Assert.AreEqual(direction, Math.Sign(changed[i].R - original[i].R));
            }
            DisposeEffect(effect);
        }
    }

    [TestMethod]
    [DataRow(-20f)]
    [DataRow(0f)]
    [DataRow(30f)]
    public void Noise_HandlesReductionBypassAndAddition(float amount) {
        var effect = EffectApplier.Apply(ShaderType.Noise, new() { Value = amount }, _source);
        if (amount == 0) Assert.AreSame(_source, effect);
        else Assert.IsFalse(Render(_source).SequenceEqual(Render(effect)));
        DisposeEffect(effect);
    }

    [TestMethod]
    [DataRow(0)]
    [DataRow(1)]
    [DataRow(2)]
    public void LuminanceToAlpha_ProducesVisibleOutput_ForEachMode(int mode) {
        var effect = EffectApplier.Apply(ShaderType.LuminanceToAlpha, new() { Mode = mode }, _source);
        Assert.IsTrue(Render(effect).Any(pixel => pixel.A > 0));
        Assert.IsFalse(Render(_source).SequenceEqual(Render(effect)));
        DisposeEffect(effect);
    }

    [TestMethod]
    public void UnsupportedEffect_PreservesTheOriginalBitmap() {
        Assert.AreSame(_source, EffectApplier.Apply(ShaderType.None, default, _source));
        Assert.AreSame(_source, EffectApplier.Apply((ShaderType)int.MaxValue, default, _source));
    }

    [TestMethod]
    [DataRow(-100f, 2)]
    [DataRow(0f, 2)]
    [DataRow(2f, 2)]
    [DataRow(16f, 16)]
    [DataRow(256f, 16)]
    public void Posterize_ClampsLegacyAndBoundaryLevels_AndActuallyReducesColors(float requested, int supported) {
        var effect = EffectApplier.Apply(ShaderType.Posterize,
            new() { Value = requested, Value2 = requested, Value3 = requested }, _source);
        var changed = Render(effect);
        Assert.IsTrue(changed.All(pixel => pixel.A == 255));
        Assert.IsLessThanOrEqualTo(supported, changed.Select(pixel => pixel.R).Distinct().Count());
        Assert.IsLessThanOrEqualTo(supported, changed.Select(pixel => pixel.G).Distinct().Count());
        Assert.IsLessThanOrEqualTo(supported, changed.Select(pixel => pixel.B).Distinct().Count());
        Assert.IsFalse(Render(_source).SequenceEqual(changed));
        DisposeEffect(effect);
    }

    [TestMethod]
    public async Task ThresholdShader_ClassifiesActualPixels_IntoTheRequestedColors() {
        try {
            await ShaderLoader.LoadAllShadersAsync();
            var effect = EffectApplier.Apply(ShaderType.ThresholdEffect,
                new() { Value = 1, Color1 = new Vector4(1, 0, 0, 1), Color2 = new Vector4(0, 0, 1, 1) }, _source);
            var original = Render(_source);
            var changed = Render(effect);
            for (int i = 0; i < original.Length; i++) {
                var expected = original[i].R + original[i].G + original[i].B <= 255
                    ? Color.FromArgb(255, 0, 0, 255) : Color.FromArgb(255, 255, 0, 0);
                Assert.AreEqual(expected, changed[i], $"Threshold classification at pixel {i}.");
            }
            DisposeEffect(effect);
        }
        finally { ShaderLoader.ClearCache(); }
    }

    [TestMethod]
    public async Task RippleShader_ZeroAmplitude_PreservesActualPixels() {
        try {
            await ShaderLoader.LoadAllShadersAsync();
            var effect = EffectApplier.Apply(ShaderType.RippleEffect,
                new() { Value = 20, Value2 = 1, Value3 = 0, Value4 = 1, Dpi = 96, Point1 = new Vector2(8) }, _source);
            CollectionAssert.AreEqual(Render(_source), Render(effect));
            DisposeEffect(effect);
        }
        finally { ShaderLoader.ClearCache(); }
    }

    private Color[] Render(ICanvasImage image, int size = 16) {
        using var target = new CanvasRenderTarget(_device, size, size, 96);
        using (var drawing = target.CreateDrawingSession()) {
            drawing.Clear(Color.FromArgb(0, 0, 0, 0));
            drawing.DrawImage(image);
        }
        return target.GetPixelColors();
    }

    private void DisposeEffect(ICanvasImage effect) {
        if (!ReferenceEquals(effect, _source) && effect is IDisposable resource) resource.Dispose();
    }

    private CanvasDevice _device = null!;
    private CanvasBitmap _source = null!;
}
