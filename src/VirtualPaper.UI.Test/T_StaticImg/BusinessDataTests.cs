using Windows.UI;
using Workloads.Creation.StaticImg.Models.SerializableData;

namespace VirtualPaper.UI.Test.T_StaticImg {
    [TestClass]
    public class BusinessDataTests {
        [TestMethod]
        public void SetColors_AddsColorsUpToMax() {
            var data = new BusinessData();
            var colors = Enumerable.Range(0, 15)
                .Select(i => Color.FromArgb(255, (byte)i, 0, 0))
                .ToList();

            data.SetColors(colors);

            Assert.HasCount(BusinessData.MAX_COLORS, data.Colors);
        }

        [TestMethod]
        public void SetColors_FewerThanMax_AllAdded() {
            var data = new BusinessData();
            var colors = new List<Color> {
                Color.FromArgb(255, 255, 0, 0),
                Color.FromArgb(255, 0, 255, 0),
                Color.FromArgb(255, 0, 0, 255),
            };

            data.SetColors(colors);

            Assert.HasCount(3, data.Colors);
            Assert.AreEqual(colors[0], data.Colors[0]);
            Assert.AreEqual(colors[1], data.Colors[1]);
            Assert.AreEqual(colors[2], data.Colors[2]);
        }

        [TestMethod]
        public void SetColors_ReplacesPreviousColors() {
            var data = new BusinessData();
            data.SetColors(new[] { Color.FromArgb(255, 255, 0, 0) });
            Assert.HasCount(1, data.Colors);

            data.SetColors(new[] {
                Color.FromArgb(255, 0, 255, 0),
                Color.FromArgb(255, 0, 0, 255),
            });
            Assert.HasCount(2, data.Colors);
            Assert.AreEqual(Color.FromArgb(255, 0, 255, 0), data.Colors[0]);
        }

        [TestMethod]
        public void SetColors_EmptyCollection_ClearsColors() {
            var data = new BusinessData();
            data.SetColors(new[] { Color.FromArgb(255, 255, 0, 0) });

            data.SetColors(Array.Empty<Color>());

            Assert.IsEmpty(data.Colors);
        }

        [TestMethod]
        public void SelectedLayerIndex_DefaultIsZero() {
            var data = new BusinessData();
            Assert.AreEqual(0, data.SelectedLayerIndex);
        }

        [TestMethod]
        public void SelectedLayerIndex_CanSetAndGet() {
            var data = new BusinessData { SelectedLayerIndex = 3 };
            Assert.AreEqual(3, data.SelectedLayerIndex);
        }

        // --- Serialize / Deserialize roundtrip ---

        [TestMethod]
        public void SerializeDeserialize_EmptyData_Roundtrips() {
            var original = new BusinessData { SelectedLayerIndex = 2 };

            var bytes = BusinessData.Serialize(original);
            var restored = BusinessData.Deserialize(bytes);

            Assert.AreEqual(original.SelectedLayerIndex, restored.SelectedLayerIndex);
            Assert.IsEmpty(restored.Colors);
        }

        [TestMethod]
        public void SerializeDeserialize_WithColors_Roundtrips() {
            var original = new BusinessData { SelectedLayerIndex = 1 };
            original.SetColors(new[] {
                Color.FromArgb(255, 100, 150, 200),
                Color.FromArgb(128, 50, 75, 25),
            });

            var bytes = BusinessData.Serialize(original);
            var restored = BusinessData.Deserialize(bytes);

            Assert.AreEqual(1, restored.SelectedLayerIndex);
            Assert.HasCount(2, restored.Colors);
            Assert.AreEqual(Color.FromArgb(255, 100, 150, 200), restored.Colors[0]);
            Assert.AreEqual(Color.FromArgb(128, 50, 75, 25), restored.Colors[1]);
        }

        [TestMethod]
        public void SerializeDeserialize_MaxColors_Roundtrips() {
            var original = new BusinessData();
            var colors = Enumerable.Range(0, BusinessData.MAX_COLORS)
                .Select(i => Color.FromArgb((byte)255, (byte)i, (byte)(i * 2), (byte)(i * 3)))
                .ToList();
            original.SetColors(colors);

            var bytes = BusinessData.Serialize(original);
            var restored = BusinessData.Deserialize(bytes);

            Assert.HasCount(BusinessData.MAX_COLORS, restored.Colors);
            for (int i = 0; i < BusinessData.MAX_COLORS; i++) {
                Assert.AreEqual(colors[i], restored.Colors[i]);
            }
        }

        [TestMethod]
        public void Deserialize_NullData_Throws() {
            Assert.Throws<ArgumentException>(() => BusinessData.Deserialize(null!));
        }

        [TestMethod]
        public void Deserialize_TooShortData_Throws() {
            Assert.Throws<ArgumentException>(() => BusinessData.Deserialize(new byte[] { 0, 1 }));
        }

        [TestMethod]
        public void Serialize_ProducesNonEmptyBytes() {
            var data = new BusinessData { SelectedLayerIndex = 0 };
            var bytes = BusinessData.Serialize(data);
            Assert.IsNotEmpty(bytes);
        }
    }
}
