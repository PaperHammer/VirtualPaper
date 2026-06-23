using Workloads.Creation.StaticImg;

namespace VirtualPaper.UI.Test.T_StaticImg {
    [TestClass]
    public class LayerStateTests {
        [TestMethod]
        public void SerializeDeserialize_VisibleLayer_Roundtrips() {
            var original = new LayerState { IsVisible = true, ZIndex = 3 };

            using var ms = new MemoryStream();
            using var writer = new BinaryWriter(ms, System.Text.Encoding.UTF8, leaveOpen: true);
            original.Serialize(writer);
            writer.Flush();

            ms.Position = 0;
            using var reader = new BinaryReader(ms);
            var restored = LayerState.Deserialize(reader);

            Assert.AreEqual(original.IsVisible, restored.IsVisible);
            Assert.AreEqual(original.ZIndex, restored.ZIndex);
        }

        [TestMethod]
        public void SerializeDeserialize_HiddenLayer_Roundtrips() {
            var original = new LayerState { IsVisible = false, ZIndex = 0 };

            using var ms = new MemoryStream();
            using var writer = new BinaryWriter(ms, System.Text.Encoding.UTF8, leaveOpen: true);
            original.Serialize(writer);
            writer.Flush();

            ms.Position = 0;
            using var reader = new BinaryReader(ms);
            var restored = LayerState.Deserialize(reader);

            Assert.IsFalse(restored.IsVisible);
            Assert.AreEqual(0, restored.ZIndex);
        }

        [TestMethod]
        public void SerializeDeserialize_NegativeZIndex_Roundtrips() {
            var original = new LayerState { IsVisible = true, ZIndex = -5 };

            using var ms = new MemoryStream();
            using var writer = new BinaryWriter(ms, System.Text.Encoding.UTF8, leaveOpen: true);
            original.Serialize(writer);
            writer.Flush();

            ms.Position = 0;
            using var reader = new BinaryReader(ms);
            var restored = LayerState.Deserialize(reader);

            Assert.AreEqual(-5, restored.ZIndex);
        }

        [TestMethod]
        public void Serialize_ProducesExpectedByteLength() {
            var state = new LayerState { IsVisible = true, ZIndex = 1 };

            using var ms = new MemoryStream();
            using var writer = new BinaryWriter(ms);
            state.Serialize(writer);

            // bool = 1 byte, int = 4 bytes → 5 bytes total
            Assert.AreEqual(5, ms.Length);
        }

        [TestMethod]
        public void DefaultStruct_IsNotVisible_ZeroZIndex() {
            var state = new LayerState();
            Assert.IsFalse(state.IsVisible);
            Assert.AreEqual(0, state.ZIndex);
        }
    }
}
