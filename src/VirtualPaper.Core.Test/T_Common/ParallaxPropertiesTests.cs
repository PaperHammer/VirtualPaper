using System.Text.Json;
using System.Text.Json.Nodes;
using VirtualPaper.Common;
using VirtualPaper.Common.Runtime.PlayerWeb;

namespace VirtualPaper.Core.Test.T_Common {
    [TestClass]
    public class ParallaxPropertiesTests {
        [TestMethod]
        public void OldDepthConfig_AddsDefaults_AndPreservesSavedValues() {
            var properties = JsonNode.Parse("""{"Parallax":{"Value":true},"Custom":{"Value":"keep"}}""")!.AsObject();
            ParallaxProperties.EnsureDepthProperties(properties);
            Assert.IsTrue(properties["Parallax"]!["Value"]!.GetValue<bool>());
            Assert.AreEqual("keep", properties["Custom"]!["Value"]!.GetValue<string>());
            Assert.AreEqual(1, properties["ParallaxStrength"]!["Value"]!.GetValue<int>());
            Assert.IsFalse(properties["ParallaxAutoDrift"]!["Value"]!.GetValue<bool>());
            properties["ParallaxStrength"]!["Value"] = 2;
            properties["ParallaxAutoDrift"]!["Value"] = true;
            var saved = JsonNode.Parse(properties.ToJsonString())!.AsObject();
            ParallaxProperties.EnsureDepthProperties(saved);
            Assert.AreEqual(2, saved["ParallaxStrength"]!["Value"]!.GetValue<int>());
            Assert.IsTrue(saved["ParallaxAutoDrift"]!["Value"]!.GetValue<bool>());
            Assert.AreEqual(4, saved.Count);
        }

        [TestMethod]
        public void DepthProperties_UseExistingUpdateAndSerialization_AndStayDepthOnly() {
            var properties = new Picture3DCostumize();
            Assert.AreEqual(1, properties.ParallaxStrength.Value);
            Assert.IsFalse(properties.ParallaxAutoDrift.Value);
            properties.ModifyPropertyValue("ParallaxStrength", 2);
            properties.ModifyPropertyValue("ParallaxAutoDrift", true);
            var saved = JsonNode.Parse(JsonSerializer.Serialize(properties, Picture3DCostumizeContext.Default.Picture3DCostumize))!;
            Assert.AreEqual(2, saved["ParallaxStrength"]!["Value"]!.GetValue<int>());
            Assert.IsTrue(saved["ParallaxAutoDrift"]!["Value"]!.GetValue<bool>());
            Assert.Throws<ArgumentOutOfRangeException>(() => properties.ModifyPropertyValue("ParallaxStrength", 3));
            UniverseCostumise[] otherTypes = [new PictureAndGifCostumise(), new VideoCostumize(), new WebCostumize()];
            foreach (var other in otherTypes) {
                Assert.Throws<ArgumentException>(() => other.ModifyPropertyValue("ParallaxAutoDrift", true));
                var json = JsonNode.Parse(JsonSerializer.Serialize(other, other.GetType()))!;
                Assert.IsNull(json["ParallaxStrength"]);
                Assert.IsNull(json["ParallaxAutoDrift"]);
            }
        }
    }
}
