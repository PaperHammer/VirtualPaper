using System.Text.Json;
using System.Text.Json.Nodes;
using VirtualPaper.Common;
using VirtualPaper.Common.Runtime.PlayerWeb;

namespace VirtualPaper.Core.Test.T_Common {
    [TestClass]
    public class AtmospherePropertiesTests {
        [TestMethod]
        public void OldConfig_AddsOnlyMissingFields_AndPreservesUserSettings() {
            var properties = JsonNode.Parse("""{"Brightness":{"Value":1.4},"Custom":{"Value":"keep"}}""")!.AsObject();
            var brightness = properties["Brightness"];
            AtmosphereProperties.EnsureAtmosphereProperties(properties);
            Assert.AreSame(brightness, properties["Brightness"]);
            Assert.AreEqual("keep", properties["Custom"]!["Value"]!.GetValue<string>());
            Assert.AreEqual(0, properties["Atmosphere"]!["Value"]!.GetValue<int>());
            Assert.AreEqual(50, properties["AtmosphereIntensity"]!["Value"]!.GetValue<int>());
            Assert.AreEqual(0, properties["AtmosphereStyle"]!["Value"]!.GetValue<int>());
            Assert.AreEqual(0, properties["AtmosphereStyle"]!["Items"]!.AsArray().Count);
            properties["Atmosphere"]!["Value"] = 3;
            properties["AtmosphereIntensity"]!["Value"] = 72;
            properties["AtmosphereStyle"]!["Value"] = 1;
            var saved = JsonNode.Parse(properties.ToJsonString())!.AsObject();
            AtmosphereProperties.EnsureAtmosphereProperties(saved);
            Assert.AreEqual(3, saved["Atmosphere"]!["Value"]!.GetValue<int>());
            Assert.AreEqual(72, saved["AtmosphereIntensity"]!["Value"]!.GetValue<int>());
            Assert.AreEqual(1, saved["AtmosphereStyle"]!["Value"]!.GetValue<int>());
            Assert.AreEqual("BlueFireflies", saved["AtmosphereStyle"]!["Items"]![1]!.GetValue<string>());
            Assert.AreEqual(5, saved.Count);
        }

        [TestMethod]
        public void ExistingDropdowns_GainNewEffects_WithoutResettingSelection() {
            var properties = JsonNode.Parse("""{"Atmosphere":{"Type":"Dropdown","Value":2,"Max":4,"Items":["None","Stardust","Snow","Fireflies","Meteor"]}}""")!.AsObject();
            AtmosphereProperties.EnsureAtmosphereProperties(properties);
            Assert.AreEqual(2, properties["Atmosphere"]!["Value"]!.GetValue<int>());
            Assert.AreEqual(7, properties["Atmosphere"]!["Max"]!.GetValue<int>());
            Assert.AreEqual(8, properties["Atmosphere"]!["Items"]!.AsArray().Count);
            Assert.AreEqual("Snowflakes", properties["AtmosphereStyle"]!["Items"]![1]!.GetValue<string>());
            properties["AtmosphereStyle"]!["Value"] = 1;
            properties["Atmosphere"]!["Value"] = (int)AtmosphereEffect.Petals;
            AtmosphereProperties.UpdateStyleItems(properties);
            Assert.AreEqual("RosePetals", properties["AtmosphereStyle"]!["Items"]![1]!.GetValue<string>());
            Assert.AreEqual(1, properties["AtmosphereStyle"]!["Value"]!.GetValue<int>());
            properties["Atmosphere"]!["Value"] = (int)AtmosphereEffect.None;
            AtmosphereProperties.UpdateStyleItems(properties);
            Assert.AreEqual(0, properties["AtmosphereStyle"]!["Items"]!.AsArray().Count);
            Assert.AreEqual(1, properties["AtmosphereStyle"]!["Value"]!.GetValue<int>());
        }

        [TestMethod]
        public void AllWallpaperTypes_SerializeAndAcceptAtmosphereProperties() {
            UniverseCostumise[] types = [new PictureAndGifCostumise(), new VideoCostumize(), new Picture3DCostumize(), new WebCostumize()];
            foreach (var properties in types) {
                Assert.AreEqual((int)AtmosphereEffect.None, properties.Atmosphere.Value);
                properties.ModifyPropertyValue("Atmosphere", (int)AtmosphereEffect.Meteor);
                properties.ModifyPropertyValue("AtmosphereIntensity", 80);
                properties.ModifyPropertyValue("AtmosphereStyle", 1);
                var saved = JsonNode.Parse(JsonSerializer.Serialize(properties, properties.GetType()))!;
                Assert.AreEqual(4, saved["Atmosphere"]!["Value"]!.GetValue<int>());
                Assert.AreEqual(80, saved["AtmosphereIntensity"]!["Value"]!.GetValue<int>());
                Assert.AreEqual("Dropdown", saved["Atmosphere"]!["Type"]!.GetValue<string>());
                Assert.AreEqual(1, saved["AtmosphereStyle"]!["Value"]!.GetValue<int>());
                foreach (var effect in Enum.GetValues<AtmosphereEffect>()) properties.ModifyPropertyValue("Atmosphere", (int)effect);
                Assert.Throws<ArgumentOutOfRangeException>(() => properties.ModifyPropertyValue("Atmosphere", 999));
                Assert.Throws<ArgumentOutOfRangeException>(() => properties.ModifyPropertyValue("AtmosphereStyle", 2));
                Assert.Throws<ArgumentOutOfRangeException>(() => properties.ModifyPropertyValue("AtmosphereIntensity", 101));
            }
        }
    }
}
