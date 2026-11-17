using System.Text.Json;
using System.Text.Json.Nodes;
using System.Globalization;
using VirtualPaper.Common;
using VirtualPaper.Common.Runtime.PlayerWeb;

namespace VirtualPaper.Core.Test.T_Common;

[TestClass]
public class EffectPresetTests {
    [TestMethod]
    public void ParameterComparison_IgnoresRenamingDictionaryOrderAndEquivalentSliderNumbers() {
        var original = EffectPresets.Capture("Original", RuntimeType.RImage, EffectPresets.Defaults(RuntimeType.RImage));
        var reloaded = JsonSerializer.Deserialize<EffectPreset>(JsonSerializer.Serialize(original))!;
        var renamed = reloaded with { Name = "Renamed", Values = reloaded.Values.Reverse().ToDictionary(item => item.Key, item => item.Value) };
        renamed.Values[nameof(UniverseCostumise.Brightness)] = JsonSerializer.Deserialize<JsonElement>("1.0");
        EffectPresets.Validate(renamed);

        Assert.IsTrue(EffectPresets.HaveSameParameters(original, renamed));
        Assert.IsTrue(EffectPresets.HaveSameParameters(renamed, original));
        Assert.AreEqual("Original", original.Name);
    }

    [TestMethod]
    [DataRow("number")]
    [DataRow("checkbox")]
    [DataRow("dropdown")]
    [DataRow("type")]
    [DataRow("removed")]
    [DataRow("different-key")]
    public void ParameterComparison_DetectsChangesToAnExistingPreset(string change) {
        var original = EffectPresets.Capture("Selected", RuntimeType.RImage, EffectPresets.Defaults(RuntimeType.RImage));
        var revised = JsonSerializer.Deserialize<EffectPreset>(JsonSerializer.Serialize(original))!;
        switch (change) {
            case "number": revised.Values[nameof(UniverseCostumise.Saturation)] = JsonSerializer.SerializeToElement(0.7); break;
            case "checkbox": revised.Values[nameof(UniverseCostumise.TimeAtmoPerception)] = JsonSerializer.SerializeToElement(true); break;
            case "dropdown": revised.Values[nameof(PictureAndGifCostumise.Scaling)] = JsonSerializer.SerializeToElement(4); break;
            case "type": revised = revised with { RuntimeType = RuntimeType.RImage3D }; break;
            case "removed": revised.Values.Remove(nameof(UniverseCostumise.Saturation)); break;
            case "different-key":
                revised.Values.Remove(nameof(UniverseCostumise.Saturation));
                revised.Values[nameof(Picture3DCostumize.ParallaxAutoDrift)] = JsonSerializer.SerializeToElement(false);
                revised = revised with { RuntimeType = RuntimeType.RImage3D };
                original = original with { RuntimeType = RuntimeType.RImage3D };
                break;
        }
        EffectPresets.Validate(original);
        EffectPresets.Validate(revised);

        Assert.AreEqual(original.Id, revised.Id);
        Assert.IsFalse(EffectPresets.HaveSameParameters(original, revised));
        Assert.IsFalse(EffectPresets.HaveSameParameters(revised, original));
    }

    [TestMethod]
    public void BuiltIns_ApplyToEverySupportedTypeWithoutChangingTheirDefinitions() {
        foreach (var preset in EffectPresets.BuiltIns()) {
            string definition = JsonSerializer.Serialize(preset);
            EffectPresets.Validate(preset);
            foreach (var type in EffectPresets.Types) {
                var original = EffectPresets.Defaults(type);
                string before = original.ToJsonString();
                var result = EffectPresets.Apply(original, preset, type);
                Assert.AreEqual(before, original.ToJsonString(), "Trying a preset must leave the prior parameters available for cancellation.");
                foreach (var (key, value) in preset.Values)
                    Assert.IsTrue(JsonNode.DeepEquals(JsonNode.Parse(value.GetRawText()), result[key]!["Value"]), key);
            }
            Assert.AreEqual(definition, JsonSerializer.Serialize(preset));
        }
    }

    [TestMethod]
    [DataRow(RuntimeType.RImage)]
    [DataRow(RuntimeType.RImage3D)]
    [DataRow(RuntimeType.RVideo)]
    public void BuiltIns_PreserveUserScalingParallaxAndPlaybackSettings(RuntimeType type) {
        var properties = EffectPresets.Defaults(type);
        properties[nameof(PictureAndGifCostumise.Scaling)]!["Value"] = 4;
        properties[nameof(PictureAndGifCostumise.Parallax)]!["Value"] = true;
        if (type == RuntimeType.RImage3D) {
            properties[nameof(Picture3DCostumize.ParallaxStrength)]!["Value"] = 2;
            properties[nameof(Picture3DCostumize.ParallaxAutoDrift)]!["Value"] = true;
        }
        if (type == RuntimeType.RVideo) {
            properties[nameof(VideoCostumize.Speed)]!["Value"] = 1.5;
            properties[nameof(VideoCostumize.Volume)]!["Value"] = 37;
        }

        foreach (var preset in EffectPresets.BuiltIns()) {
            var result = EffectPresets.Apply(properties, preset, type);
            foreach (var (key, value) in properties.Where(item => !preset.Values.ContainsKey(item.Key)))
                Assert.IsTrue(JsonNode.DeepEquals(value, result[key]), key);
        }
    }

    [TestMethod]
    public void CaptureAndApply_ExcludePathsAndPreserveWallpaperSpecificFields() {
        var properties = EffectPresets.Defaults(RuntimeType.RImage3D);
        properties["DepthFilePath"] = @"C:\private\depth.png";
        properties["Monitor"] = "DISPLAY1";
        properties["CustomAsset"] = new JsonObject { ["Type"] = "Textbox", ["Value"] = "private/image.png" };
        properties[nameof(UniverseCostumise.Brightness)]!["Value"] = 1.4;
        var preset = EffectPresets.Capture("My effect", RuntimeType.RImage3D, properties);

        Assert.IsFalse(preset.Values.ContainsKey("DepthFilePath"));
        Assert.IsFalse(preset.Values.ContainsKey("Monitor"));
        Assert.IsFalse(preset.Values.ContainsKey("CustomAsset"));
        var target = EffectPresets.Defaults(RuntimeType.RImage3D);
        target["DepthFilePath"] = "other/depth.png";
        target["CustomAsset"] = properties["CustomAsset"]!.DeepClone();
        var result = EffectPresets.Apply(target, preset, RuntimeType.RImage3D);
        Assert.AreEqual("other/depth.png", result["DepthFilePath"]!.ToString());
        Assert.AreEqual(target["CustomAsset"]!.ToJsonString(), result["CustomAsset"]!.ToJsonString());
        Assert.AreEqual(1.4, (double)result[nameof(UniverseCostumise.Brightness)]!["Value"]!);
        Assert.AreEqual(1.0, (double)target[nameof(UniverseCostumise.Brightness)]!["Value"]!);
    }

    [TestMethod]
    public void UnsupportedRuntimeType_IsRejectedWithoutChangingTheWallpaper() {
        var preset = EffectPresets.BuiltIns()[0];
        var properties = EffectPresets.Defaults(RuntimeType.RImage);
        string before = properties.ToJsonString();
        Assert.IsFalse(EffectPresets.CanApply(preset, (RuntimeType)999));
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => EffectPresets.Defaults((RuntimeType)999));
        Assert.ThrowsExactly<InvalidOperationException>(() => EffectPresets.Apply(properties, preset, (RuntimeType)999));
        Assert.AreEqual(before, properties.ToJsonString());
    }

    [TestMethod]
    [DataRow(RuntimeType.RImage)]
    [DataRow(RuntimeType.RImage3D)]
    [DataRow(RuntimeType.RVideo)]
    [DataRow(RuntimeType.RWeb)]
    public void TypeSpecificPresets_OnlyApplyToTheirOwnType(RuntimeType type) {
        var preset = EffectPresets.Capture("Typed", type, EffectPresets.Defaults(type));
        foreach (var target in EffectPresets.Types) {
            var properties = EffectPresets.Defaults(target);
            string before = properties.ToJsonString();
            if (target == type) {
                Assert.IsTrue(EffectPresets.CanApply(preset, target));
                var result = EffectPresets.Apply(properties, preset, target);
                foreach (var (key, value) in preset.Values)
                    Assert.IsTrue(JsonNode.DeepEquals(JsonNode.Parse(value.GetRawText()), result[key]!["Value"]), key);
            }
            else {
                Assert.IsFalse(EffectPresets.CanApply(preset, target));
                Assert.ThrowsExactly<InvalidOperationException>(() => EffectPresets.Apply(properties, preset, target));
            }
            Assert.AreEqual(before, properties.ToJsonString());
        }
    }

    [TestMethod]
    [DataRow(-1)]
    [DataRow(3)]
    public void InvalidBrightness_IsRejectedBeforeChangingParameters(int value) {
        var preset = EffectPresets.Capture("Bad", RuntimeType.RImage, EffectPresets.Defaults(RuntimeType.RImage));
        preset.Values[nameof(UniverseCostumise.Brightness)] = JsonSerializer.SerializeToElement(value);
        var original = EffectPresets.Defaults(RuntimeType.RImage);
        string before = original.ToJsonString();
        Assert.ThrowsExactly<InvalidDataException>(() => EffectPresets.Apply(original, preset, RuntimeType.RImage));
        Assert.AreEqual(before, original.ToJsonString());
    }

    [TestMethod]
    public void PersistedUnknownPropertiesAndStringValues_AreRejected() {
        var preset = EffectPresets.Capture("Bad", RuntimeType.RImage, EffectPresets.Defaults(RuntimeType.RImage));
        preset.Values["FilePath"] = JsonSerializer.SerializeToElement("private.png");
        Assert.ThrowsExactly<InvalidDataException>(() => EffectPresets.Validate(preset));
        preset.Values.Remove("FilePath");
        preset.Values[nameof(UniverseCostumise.Brightness)] = JsonSerializer.SerializeToElement("private.png");
        Assert.ThrowsExactly<InvalidDataException>(() => EffectPresets.Validate(preset));
    }

    [TestMethod]
    [DataRow("id")]
    [DataRow("name")]
    [DataRow("long-name")]
    [DataRow("type")]
    [DataRow("empty-values")]
    [DataRow("null-values")]
    public void InvalidPresetMetadata_IsRejected(string invalidField) {
        var preset = EffectPresets.Capture("Valid", RuntimeType.RImage, EffectPresets.Defaults(RuntimeType.RImage));
        preset = invalidField switch {
            "id" => preset with { Id = null! },
            "name" => preset with { Name = " \t " },
            "long-name" => preset with { Name = new string('a', 81) },
            "type" => preset with { RuntimeType = (RuntimeType)999 },
            "empty-values" => preset with { Values = [] },
            "null-values" => preset with { Values = null! },
            _ => throw new AssertFailedException("Unknown test case.")
        };

        Assert.ThrowsExactly<InvalidDataException>(() => EffectPresets.Validate(preset));
    }

    [TestMethod]
    [DataRow(nameof(UniverseCostumise.TimeAtmoPerception), "1")]
    [DataRow(nameof(UniverseCostumise.TimeAtmoPerception), "\"true\"")]
    [DataRow(nameof(UniverseCostumise.TimeAtmoPerception), "null")]
    [DataRow(nameof(UniverseCostumise.Brightness), "true")]
    [DataRow(nameof(UniverseCostumise.Brightness), "null")]
    [DataRow(nameof(UniverseCostumise.Brightness), "[]")]
    [DataRow(nameof(UniverseCostumise.Brightness), "{}")]
    [DataRow(nameof(UniverseCostumise.Brightness), "1e400")]
    [DataRow(nameof(UniverseCostumise.Atmosphere), "-1")]
    [DataRow(nameof(UniverseCostumise.Atmosphere), "8")]
    [DataRow(nameof(UniverseCostumise.Atmosphere), "0.5")]
    [DataRow(nameof(PictureAndGifCostumise.Scaling), "-1")]
    [DataRow(nameof(PictureAndGifCostumise.Scaling), "5")]
    [DataRow(nameof(PictureAndGifCostumise.Scaling), "0.5")]
    [DataRow(nameof(PictureAndGifCostumise.Scaling), "1.0")]
    [DataRow(nameof(PictureAndGifCostumise.Scaling), "1e0")]
    public void InvalidParameterValues_AreRejectedWithoutChangingTheWallpaper(string key, string json) {
        var preset = EffectPresets.Capture("Invalid", RuntimeType.RImage, EffectPresets.Defaults(RuntimeType.RImage));
        preset.Values[key] = JsonSerializer.Deserialize<JsonElement>(json);
        var properties = EffectPresets.Defaults(RuntimeType.RImage);
        string before = properties.ToJsonString();

        Assert.ThrowsExactly<InvalidDataException>(() => EffectPresets.Apply(properties, preset, RuntimeType.RImage));
        Assert.AreEqual(before, properties.ToJsonString());
    }

    [TestMethod]
    [DataRow("fr-FR")]
    [DataRow("de-DE")]
    public void BoundaryValues_AreValidAcrossCultures(string culture) {
        var previous = CultureInfo.CurrentCulture;
        try {
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo(culture);
            var properties = EffectPresets.Defaults(RuntimeType.RImage3D);
            properties[nameof(UniverseCostumise.Brightness)]!["Value"] = 2;
            properties[nameof(UniverseCostumise.Hue)]!["Value"] = 359;
            properties[nameof(UniverseCostumise.Saturation)]!["Value"] = 0;
            properties[nameof(UniverseCostumise.TimeAtmoPerception)]!["Value"] = true;
            properties[nameof(UniverseCostumise.AtmosphereIntensity)]!["Value"] = 100;
            properties[nameof(Picture3DCostumize.Scaling)]!["Value"] = 4;
            properties[nameof(Picture3DCostumize.ParallaxStrength)]!["Value"] = 0;
            properties[nameof(Picture3DCostumize.ParallaxAutoDrift)]!["Value"] = true;

            var preset = EffectPresets.Capture("  " + new string('a', 80) + "  ", RuntimeType.RImage3D, properties);
            var result = EffectPresets.Apply(EffectPresets.Defaults(RuntimeType.RImage3D), preset, RuntimeType.RImage3D);

            Assert.AreEqual(80, preset.Name.Length);
            foreach (var (key, value) in preset.Values)
                Assert.IsTrue(JsonNode.DeepEquals(JsonNode.Parse(value.GetRawText()), result[key]!["Value"]), key);
        }
        finally { CultureInfo.CurrentCulture = previous; }
    }

    [TestMethod]
    [DataRow(AtmosphereEffect.None, 0)]
    [DataRow(AtmosphereEffect.Stardust, 1)]
    [DataRow(AtmosphereEffect.Snow, 1)]
    [DataRow(AtmosphereEffect.Fireflies, 1)]
    [DataRow(AtmosphereEffect.Meteor, 1)]
    [DataRow(AtmosphereEffect.Rain, 1)]
    [DataRow(AtmosphereEffect.Petals, 1)]
    [DataRow(AtmosphereEffect.Bokeh, 1)]
    public void AtmosphereStyles_UseTheSelectedEffectsChoicesRegardlessOfParameterOrder(AtmosphereEffect effect, int style) {
        var preset = new EffectPreset {
            Name = "Atmosphere", RuntimeType = RuntimeType.RUnknown,
            Values = new() {
                [nameof(UniverseCostumise.AtmosphereStyle)] = JsonSerializer.SerializeToElement(style),
                [nameof(UniverseCostumise.Atmosphere)] = JsonSerializer.SerializeToElement((int)effect)
            }
        };
        var properties = EffectPresets.Defaults(RuntimeType.RImage);
        properties[nameof(UniverseCostumise.Atmosphere)]!["Value"] = (int)AtmosphereEffect.Fireflies;
        AtmosphereProperties.UpdateStyleItems(properties);

        var result = EffectPresets.Apply(properties, preset, RuntimeType.RImage);

        Assert.AreEqual(style, (int)result[nameof(UniverseCostumise.AtmosphereStyle)]!["Value"]!);
        CollectionAssert.AreEqual(AtmosphereStyleProperty.GetItems(effect),
            result[nameof(UniverseCostumise.AtmosphereStyle)]!["Items"]!.AsArray().Select(item => item!.ToString()).ToArray());
        Assert.AreEqual((int)AtmosphereEffect.Fireflies, (int)properties[nameof(UniverseCostumise.Atmosphere)]!["Value"]!);
    }

    [TestMethod]
    [DataRow(AtmosphereEffect.None, 1)]
    [DataRow(AtmosphereEffect.Snow, 2)]
    public void PersistedStyleOutsideSelectedEffect_IsRejected(AtmosphereEffect effect, int style) {
        var preset = EffectPresets.Capture("Invalid style", RuntimeType.RUnknown, EffectPresets.Defaults(RuntimeType.RUnknown));
        preset.Values[nameof(UniverseCostumise.Atmosphere)] = JsonSerializer.SerializeToElement((int)effect);
        preset.Values[nameof(UniverseCostumise.AtmosphereStyle)] = JsonSerializer.SerializeToElement(style);

        Assert.ThrowsExactly<InvalidDataException>(() => EffectPresets.Validate(preset));
    }

    [TestMethod]
    public void PartialPreset_FillsMissingDefinitionsAndDepthDefaultsWithoutChangingOtherValues() {
        var preset = new EffectPreset {
            Name = "Colors", RuntimeType = RuntimeType.RUnknown,
            Values = new() { [nameof(UniverseCostumise.Saturation)] = JsonSerializer.SerializeToElement(0.7) }
        };
        var properties = new JsonObject { ["PrivateAsset"] = "keep.png", ["Brightness"] = new JsonObject { ["Value"] = 1.4 } };
        string before = properties.ToJsonString();

        var result = EffectPresets.Apply(properties, preset, RuntimeType.RImage3D);

        Assert.AreEqual("Slider", result[nameof(UniverseCostumise.Saturation)]!["Type"]!.ToString());
        Assert.AreEqual(0.7, (double)result[nameof(UniverseCostumise.Saturation)]!["Value"]!);
        Assert.AreEqual(1.4, (double)result[nameof(UniverseCostumise.Brightness)]!["Value"]!);
        Assert.AreEqual(1, (int)result[nameof(Picture3DCostumize.ParallaxStrength)]!["Value"]!);
        Assert.IsFalse((bool)result[nameof(Picture3DCostumize.ParallaxAutoDrift)]!["Value"]!);
        Assert.AreEqual("keep.png", result["PrivateAsset"]!.ToString());
        Assert.AreEqual(before, properties.ToJsonString());
    }

    [TestMethod]
    public void CaptureAndApply_ReturnIndependentSnapshots() {
        var properties = EffectPresets.Defaults(RuntimeType.RImage);
        properties[nameof(UniverseCostumise.Saturation)]!["Value"] = 0.7;
        var preset = EffectPresets.Capture("Snapshot", RuntimeType.RImage, properties);
        properties[nameof(UniverseCostumise.Saturation)]!["Value"] = 1.3;
        var result = EffectPresets.Apply(properties, preset, RuntimeType.RImage);
        result[nameof(UniverseCostumise.Saturation)]!["Value"] = 1.6;

        Assert.AreEqual(0.7, preset.Values[nameof(UniverseCostumise.Saturation)].GetDouble());
        Assert.AreEqual(1.3, (double)properties[nameof(UniverseCostumise.Saturation)]!["Value"]!);
        Assert.AreEqual(1.0, (double)EffectPresets.Defaults(RuntimeType.RImage)[nameof(UniverseCostumise.Saturation)]!["Value"]!);
    }

    [TestMethod]
    public void Capture_NormalizesAStaleAtmosphereStyleWithoutChangingSource() {
        var properties = EffectPresets.Defaults(RuntimeType.RImage);
        properties[nameof(UniverseCostumise.AtmosphereStyle)]!["Value"] = 1;
        var preset = EffectPresets.Capture("None", RuntimeType.RImage, properties);
        Assert.AreEqual(0, preset.Values[nameof(UniverseCostumise.AtmosphereStyle)].GetInt32());
        Assert.AreEqual(1, (int)properties[nameof(UniverseCostumise.AtmosphereStyle)]!["Value"]!);
        EffectPresets.Validate(preset);
    }
}
