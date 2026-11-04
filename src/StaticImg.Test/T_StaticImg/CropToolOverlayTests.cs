using System.Reflection;
using System.Runtime.CompilerServices;
using Windows.Foundation;
using Workloads.Creation.StaticImg.Core.Rendering;
using Workloads.Creation.StaticImg.Models.ToolItems;

namespace StaticImg.Test.T_StaticImg {
    [TestClass]
    public class CropToolOverlayTests {
        [TestMethod]
        public void CalculateAspectRatioRect_DoesNotRequireLayerRenderTarget() {
            MethodInfo? calculator = typeof(CropTool).GetMethod(
                "CalculateAspectRatioRect",
                BindingFlags.Static | BindingFlags.NonPublic);
            Assert.IsNotNull(calculator);

            var cropRect = (Rect)calculator.Invoke(null, [new Size(1000, 500), 16d / 9d])!;

            Assert.IsFalse(cropRect.IsEmpty);
            Assert.AreEqual(16d / 9d, cropRect.Width / cropRect.Height, 0.0001);
        }

        [TestMethod]
        public void CropTool_DisablesOriginalLayerSnapshot() {
            var tool = (CropTool)RuntimeHelpers.GetUninitializedObject(typeof(CropTool));

            Assert.IsFalse(RequiresOriginalContentSnapshot(tool));
        }

        [TestMethod]
        public void SelectionTool_KeepsOriginalLayerSnapshotBehavior() {
            var tool = (SelectionTool)RuntimeHelpers.GetUninitializedObject(typeof(SelectionTool));

            Assert.IsTrue(RequiresOriginalContentSnapshot(tool));
        }

        private static bool RequiresOriginalContentSnapshot(CanvasAreaSelector tool) {
            PropertyInfo? snapshotPolicy = typeof(CanvasAreaSelector).GetProperty(
                "RequiresOriginalContentSnapshot",
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.IsNotNull(snapshotPolicy);
            return (bool)snapshotPolicy.GetValue(tool)!;
        }
    }
}
