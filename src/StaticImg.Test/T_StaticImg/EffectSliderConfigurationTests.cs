using VirtualPaper.Shader;
using Workloads.Creation.StaticImg.Utils;

namespace StaticImg.Test.T_StaticImg;

[TestClass]
public sealed class EffectSliderConfigurationTests {
    public static IEnumerable<object[]> Effects() => Enum.GetValues<ShaderType>().Select(type => new object[] { type });

    [TestMethod]
    [DynamicData(nameof(Effects))]
    public void EffectDefaults_StayInsideTheEditableRanges(ShaderType type) {
        var configuration = EffectMap.GetSliderConfig(type);
        if (configuration.HasSlider1) {
            Assert.IsGreaterThanOrEqualTo(configuration.Min, configuration.Default);
            Assert.IsLessThanOrEqualTo(configuration.Max, configuration.Default);
            Assert.IsFalse(string.IsNullOrWhiteSpace(configuration.Label));
        }
        if (configuration.HasSlider2) {
            Assert.IsGreaterThanOrEqualTo(configuration.Min2, configuration.Default2);
            Assert.IsLessThanOrEqualTo(configuration.Max2, configuration.Default2);
            Assert.IsFalse(string.IsNullOrWhiteSpace(configuration.Label2));
        }
    }

    [TestMethod]
    public void PosterizeSlider_OffersOnlyWin2DSupportedColorCounts() {
        var configuration = EffectMap.GetSliderConfig(ShaderType.Posterize);
        Assert.AreEqual(2f, configuration.Min);
        Assert.AreEqual(16f, configuration.Max);
        Assert.AreEqual(16f, configuration.Default);
        Assert.AreEqual(configuration.Min, configuration.Min2);
        Assert.AreEqual(configuration.Max, configuration.Max2);
        Assert.AreEqual(configuration.Default, configuration.Default2);
        Assert.AreEqual(ShaderType.Posterize, EffectMap.ToShaderType("art_pixelate"));
        Assert.AreEqual(ShaderType.None, EffectMap.ToShaderType("unknown-effect"));
    }
}
