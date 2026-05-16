namespace VirtualPaper.Shader.Test {
    [TestClass]
    [TestCategory("Unit")]
    [DoNotParallelize]
    public class ShaderLoader_StateTests {

        [TestCleanup]
        public void Cleanup() {
            // 每个测试后重置缓存，避免状态污染
            ShaderLoader.ClearCache();
        }

        // ── IsLoaded ────────────────────────────────────────────────

        [TestMethod]
        [Description("初始状态下 IsLoaded 应为 false")]
        public void IsLoaded_BeforeAnyLoad_IsFalse() {
            ShaderLoader.ClearCache();

            Assert.IsFalse(ShaderLoader.IsLoaded);
        }

        [TestMethod]
        [Description("ClearCache 后 IsLoaded 应重置为 false")]
        public void ClearCache_ResetsIsLoadedToFalse() {
            // 直接操作 _isInited 需要通过行为验证
            // 先 ClearCache，再确认 IsLoaded = false
            ShaderLoader.ClearCache();

            Assert.IsFalse(ShaderLoader.IsLoaded,
                "ClearCache should reset IsLoaded to false");
        }

        // ── GetShader 未初始化 ──────────────────────────────────────

        [TestMethod]
        [Description("未调用 LoadAllShadersAsync 时 GetShader 应抛出 InvalidOperationException")]
        public void GetShader_BeforeLoad_ThrowsInvalidOperationException() {
            ShaderLoader.ClearCache(); // 确保未初始化

            Assert.Throws<InvalidOperationException>(
                () => ShaderLoader.GetShader(ShaderType.GeometryAlphaEraseEffect),
                "Should throw InvalidOperationException when shaders are not loaded");
        }

        [TestMethod]
        [Description("未初始化时异常消息应包含指导性文字")]
        public void GetShader_BeforeLoad_ExceptionMessageIsDescriptive() {
            ShaderLoader.ClearCache();

            try {
                ShaderLoader.GetShader(ShaderType.GeometryAlphaEraseEffect);
                Assert.Fail("Expected InvalidOperationException");
            }
            catch (InvalidOperationException ex) {
                Assert.Contains("LoadAllShadersAsync", ex.Message,
                    "Exception message should tell caller to call LoadAllShadersAsync");
            }
        }

        // ── ClearCache ──────────────────────────────────────────────

        [TestMethod]
        [Description("ClearCache 连续调用不应抛出异常")]
        public void ClearCache_CalledMultipleTimes_DoesNotThrow() {
            ShaderLoader.ClearCache();
            ShaderLoader.ClearCache();
            ShaderLoader.ClearCache();

            // 不抛即通过
        }
    }
}
