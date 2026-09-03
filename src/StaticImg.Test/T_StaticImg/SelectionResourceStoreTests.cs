using Workloads.Creation.StaticImg.Core.Utils;

namespace StaticImg.Test.T_StaticImg {
    [TestClass]
    public class SelectionResourceStoreTests {
        [TestMethod]
        public void ReplaceOriginalContentSnapshot_DoesNotAffectSelectedRegionSnapshot() {
            using var resources = new SelectionResourceStore<TestResource>();
            var selection = new TestResource();
            var previousBase = new TestResource();
            var currentBase = new TestResource();
            var target = new TestResource();
            resources.ReplaceSelectedRegionSnapshot(selection);
            resources.ReplaceOriginalContentSnapshot(previousBase, target);

            resources.ReplaceOriginalContentSnapshot(currentBase, target);

            Assert.AreEqual(1, previousBase.DisposeCount);
            Assert.AreEqual(0, selection.DisposeCount);
            Assert.AreSame(currentBase, resources.OriginalContentSnapshot);
            Assert.AreSame(selection, resources.SelectedRegionSnapshot);
        }

        [TestMethod]
        public void ReplaceSelectedRegionSnapshot_DoesNotAffectOriginalContentSnapshot() {
            using var resources = new SelectionResourceStore<TestResource>();
            var baseContent = new TestResource();
            var target = new TestResource();
            var previousSelection = new TestResource();
            var currentSelection = new TestResource();
            resources.ReplaceOriginalContentSnapshot(baseContent, target);
            resources.ReplaceSelectedRegionSnapshot(previousSelection);

            resources.ReplaceSelectedRegionSnapshot(currentSelection);

            Assert.AreEqual(1, previousSelection.DisposeCount);
            Assert.AreEqual(0, baseContent.DisposeCount);
            Assert.AreSame(baseContent, resources.OriginalContentSnapshot);
            Assert.AreSame(currentSelection, resources.SelectedRegionSnapshot);
        }

        [TestMethod]
        public void ReleaseSelectedRegionSnapshot_KeepsOriginalSnapshotForFinalRender() {
            using var resources = new SelectionResourceStore<TestResource>();
            var baseContent = new TestResource();
            var target = new TestResource();
            var selection = new TestResource();
            resources.ReplaceOriginalContentSnapshot(baseContent, target);
            resources.ReplaceSelectedRegionSnapshot(selection);

            bool released = resources.ReleaseSelectedRegionSnapshot();

            Assert.IsTrue(released);
            Assert.AreEqual(1, selection.DisposeCount);
            Assert.AreEqual(0, baseContent.DisposeCount);
            Assert.IsNull(resources.SelectedRegionSnapshot);
            Assert.AreSame(baseContent, resources.OriginalContentSnapshot);
            Assert.AreSame(target, resources.SourceRenderTarget);
        }

        [TestMethod]
        public void ReleaseOriginalContentSnapshot_WithStaleInstance_KeepsCurrentSnapshot() {
            using var resources = new SelectionResourceStore<TestResource>();
            var stale = new TestResource();
            var current = new TestResource();
            var target = new TestResource();
            resources.ReplaceOriginalContentSnapshot(current, target);

            bool released = resources.ReleaseOriginalContentSnapshot(stale);

            Assert.IsFalse(released);
            Assert.AreSame(current, resources.OriginalContentSnapshot);
            Assert.AreEqual(0, current.DisposeCount);
            Assert.AreSame(target, resources.SourceRenderTarget);
        }

        [TestMethod]
        public void Dispose_ReleasesBothResourcesAndClearsReferences() {
            var resources = new SelectionResourceStore<TestResource>();
            var baseContent = new TestResource();
            var target = new TestResource();
            var selection = new TestResource();
            resources.ReplaceOriginalContentSnapshot(baseContent, target);
            resources.ReplaceSelectedRegionSnapshot(selection);

            resources.Dispose();
            resources.Dispose();

            Assert.AreEqual(1, baseContent.DisposeCount);
            Assert.AreEqual(1, selection.DisposeCount);
            Assert.AreEqual(0, target.DisposeCount);
            Assert.IsNull(resources.OriginalContentSnapshot);
            Assert.IsNull(resources.SourceRenderTarget);
            Assert.IsNull(resources.SelectedRegionSnapshot);
        }

        [TestMethod]
        public void Dispose_WhenSelectedRegionDisposeThrows_StillReleasesOriginalSnapshot() {
            var resources = new SelectionResourceStore<TestResource>();
            var baseContent = new TestResource();
            var target = new TestResource();
            var selection = new TestResource(throwOnDispose: true);
            resources.ReplaceOriginalContentSnapshot(baseContent, target);
            resources.ReplaceSelectedRegionSnapshot(selection);

            resources.Dispose();

            Assert.AreEqual(1, selection.DisposeCount);
            Assert.AreEqual(1, baseContent.DisposeCount);
            Assert.IsNull(resources.SelectedRegionSnapshot);
            Assert.IsNull(resources.OriginalContentSnapshot);
            Assert.IsNull(resources.SourceRenderTarget);
        }

        [TestMethod]
        public void ReplaceOriginalContentSnapshot_TracksSourceTargetWithoutOwningIt() {
            using var resources = new SelectionResourceStore<TestResource>();
            var firstBase = new TestResource();
            var firstTarget = new TestResource();
            var secondBase = new TestResource();
            var secondTarget = new TestResource();

            resources.ReplaceOriginalContentSnapshot(firstBase, firstTarget);
            resources.ReplaceOriginalContentSnapshot(secondBase, secondTarget);

            Assert.AreEqual(1, firstBase.DisposeCount);
            Assert.AreEqual(0, firstTarget.DisposeCount);
            Assert.AreSame(secondBase, resources.OriginalContentSnapshot);
            Assert.AreSame(secondTarget, resources.SourceRenderTarget);
        }

        [TestMethod]
        public void ReleaseOriginalContentSnapshot_ClearsSourceTargetWithoutDisposingIt() {
            using var resources = new SelectionResourceStore<TestResource>();
            var baseContent = new TestResource();
            var target = new TestResource();
            resources.ReplaceOriginalContentSnapshot(baseContent, target);

            bool released = resources.ReleaseOriginalContentSnapshot(baseContent);

            Assert.IsTrue(released);
            Assert.AreEqual(1, baseContent.DisposeCount);
            Assert.AreEqual(0, target.DisposeCount);
            Assert.IsNull(resources.SourceRenderTarget);
        }

        private sealed class TestResource(bool throwOnDispose = false) : IDisposable {
            public int DisposeCount { get; private set; }

            public void Dispose() {
                DisposeCount++;
                if (throwOnDispose) throw new InvalidOperationException("Test disposal failure.");
            }
        }
    }
}
