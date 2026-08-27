using Workloads.Creation.StaticImg.Core.Utils;

namespace VirtualPaper.UI.Test.T_StaticImg {
    [TestClass]
    public class DisposableResourceSlotTests {
        [TestMethod]
        public void Replace_NewResource_DisposesPreviousResource() {
            using var slot = new DisposableResourceSlot<TestResource>();
            var previous = new TestResource();
            var current = new TestResource();

            slot.Replace(previous);
            slot.Replace(current);

            Assert.AreEqual(1, previous.DisposeCount);
            Assert.AreEqual(0, current.DisposeCount);
            Assert.AreSame(current, slot.Value);
        }

        [TestMethod]
        public void Replace_SameResource_DoesNotDisposeResource() {
            using var slot = new DisposableResourceSlot<TestResource>();
            var resource = new TestResource();

            slot.Replace(resource);
            slot.Replace(resource);

            Assert.AreEqual(0, resource.DisposeCount);
            Assert.AreSame(resource, slot.Value);
        }

        [TestMethod]
        public void Release_CurrentResource_DisposesAndClearsResource() {
            using var slot = new DisposableResourceSlot<TestResource>();
            var resource = new TestResource();
            slot.Replace(resource);

            bool released = slot.Release();

            Assert.IsTrue(released);
            Assert.AreEqual(1, resource.DisposeCount);
            Assert.IsNull(slot.Value);
        }

        [TestMethod]
        public void Release_StaleExpectedResource_DoesNotReleaseReplacement() {
            using var slot = new DisposableResourceSlot<TestResource>();
            var stale = new TestResource();
            var current = new TestResource();
            slot.Replace(current);

            bool released = slot.Release(stale);

            Assert.IsFalse(released);
            Assert.AreEqual(0, stale.DisposeCount);
            Assert.AreEqual(0, current.DisposeCount);
            Assert.AreSame(current, slot.Value);
        }

        [TestMethod]
        public void Release_MatchingExpectedResource_ReleasesResource() {
            using var slot = new DisposableResourceSlot<TestResource>();
            var resource = new TestResource();
            slot.Replace(resource);

            bool released = slot.Release(resource);

            Assert.IsTrue(released);
            Assert.AreEqual(1, resource.DisposeCount);
            Assert.IsNull(slot.Value);
        }

        [TestMethod]
        public void Release_RepeatedCall_DisposesResourceOnlyOnce() {
            using var slot = new DisposableResourceSlot<TestResource>();
            var resource = new TestResource();
            slot.Replace(resource);

            bool firstRelease = slot.Release();
            bool secondRelease = slot.Release();

            Assert.IsTrue(firstRelease);
            Assert.IsFalse(secondRelease);
            Assert.AreEqual(1, resource.DisposeCount);
        }

        [TestMethod]
        public void Release_WhenDisposeThrows_ClearsResourceAndDoesNotThrow() {
            using var slot = new DisposableResourceSlot<TestResource>();
            var resource = new TestResource(throwOnDispose: true);
            slot.Replace(resource);

            bool released = slot.Release();

            Assert.IsTrue(released);
            Assert.AreEqual(1, resource.DisposeCount);
            Assert.IsNull(slot.Value);
        }

        [TestMethod]
        public void Replace_NullResource_ThrowsAndKeepsCurrentResource() {
            using var slot = new DisposableResourceSlot<TestResource>();
            var current = new TestResource();
            slot.Replace(current);

            Assert.Throws<ArgumentNullException>(() => slot.Replace(null!));

            Assert.AreSame(current, slot.Value);
            Assert.AreEqual(0, current.DisposeCount);
        }

        [TestMethod]
        public void Replace_WhenPreviousDisposeThrows_KeepsNewResource() {
            using var slot = new DisposableResourceSlot<TestResource>();
            var previous = new TestResource(throwOnDispose: true);
            var current = new TestResource();
            slot.Replace(previous);

            slot.Replace(current);

            Assert.AreEqual(1, previous.DisposeCount);
            Assert.AreSame(current, slot.Value);
            Assert.AreEqual(0, current.DisposeCount);
        }

        [TestMethod]
        public void Replace_MultipleResources_DisposesEverySupersededResourceOnce() {
            using var slot = new DisposableResourceSlot<TestResource>();
            var first = new TestResource();
            var second = new TestResource();
            var third = new TestResource();

            slot.Replace(first);
            slot.Replace(second);
            slot.Replace(third);

            Assert.AreEqual(1, first.DisposeCount);
            Assert.AreEqual(1, second.DisposeCount);
            Assert.AreEqual(0, third.DisposeCount);
            Assert.AreSame(third, slot.Value);
        }

        [TestMethod]
        public void Dispose_CurrentResource_DisposesAndClearsResource() {
            var slot = new DisposableResourceSlot<TestResource>();
            var resource = new TestResource();
            slot.Replace(resource);

            slot.Dispose();
            slot.Dispose();

            Assert.AreEqual(1, resource.DisposeCount);
            Assert.IsNull(slot.Value);
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
