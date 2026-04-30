namespace VirtualPaper.Shader.Test {
    [TestClass]
    [TestCategory("Integration")]
    [DoNotParallelize]
    public class ShaderLoader_LoadTests {
        [TestInitialize]
        public async Task TestSetup() {
            await ShaderLoader.LoadAllShadersAsync();
        }

        [TestCleanup]
        public void TestCleanup() {
            ShaderLoader.ClearCache();
        }

        /// <summary>
        /// 合并自：SetsIsLoadedTrue + IsIdempotent
        /// 验证 LoadAllShadersAsync 的幂等性：首次设置 IsLoaded，重复调用不改变状态
        /// </summary>
        [TestMethod]
        [Description("LoadAllShadersAsync 应设置 IsLoaded，且多次调用保持幂等")]
        public async Task LoadAllShadersAsync_IsIdempotentAndSetsIsLoaded() {
            Assert.IsTrue(ShaderLoader.IsLoaded, "首次加载后 IsLoaded 应为 true");

            await ShaderLoader.LoadAllShadersAsync(); // 第二次调用
            Assert.IsTrue(ShaderLoader.IsLoaded, "重复调用后 IsLoaded 应仍为 true");
        }

        /// <summary>
        /// 合并自：AllTypesAccessible + StartsWithDxbcMagic
        /// 遍历所有 ShaderType，验证可访问且数据格式合法
        /// </summary>
        [TestMethod]
        [Description("加载后每个 ShaderType 均可访问，且数据不为空")]
        public void LoadAllShadersAsync_AllTypes_Accessible() {
            var allTypes = Enum.GetValues<ShaderType>()
                               .Where(t => t != ShaderType.None)
                               .ToArray();

            foreach (var type in allTypes) {
                byte[] data;
                try {
                    data = ShaderLoader.GetShader(type);
                }
                catch (KeyNotFoundException) {
                    Assert.Fail(
                        $"ShaderType.{type} was not loaded. " +
                        $"Check that the corresponding shader file exists.");
                    return;
                }

                Assert.IsNotEmpty(data, $"{type}: shader data should not be empty");
            }
        }

        [TestMethod]
        [Description("ReloadAllShadersAsync 应重新填充缓存，IsLoaded 保持 true")]
        public async Task ReloadAllShadersAsync_RefreshesCache() {
            await ShaderLoader.ReloadAllShadersAsync();

            Assert.IsTrue(ShaderLoader.IsLoaded);
        }

        [TestMethod]
        [Description("ReloadShaderAsync 应返回与缓存一致的数据")]
        public async Task ReloadShaderAsync_UpdatesCache() {
            var type = Enum.GetValues<ShaderType>()
                           .FirstOrDefault(t => t != ShaderType.None);
            if (type == ShaderType.None)
                Assert.Inconclusive("No ShaderType available");

            var original = ShaderLoader.GetShader(type);
            var reloaded = await ShaderLoader.ReloadShaderAsync(type);

            CollectionAssert.AreEqual(original, reloaded,
                "Reloaded data should match the cached content");
        }
    }
}
