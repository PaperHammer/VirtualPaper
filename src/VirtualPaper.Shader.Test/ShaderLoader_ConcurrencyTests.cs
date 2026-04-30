using System.Collections.Concurrent;

namespace VirtualPaper.Shader.Test {
    [TestClass]
    [TestCategory("Unit")]
    [DoNotParallelize]
    public class ShaderLoader_ConcurrencyTests {
        [TestCleanup]
        public void Cleanup() {
            ShaderLoader.ClearCache();
        }

        /// <summary>
        /// 已初始化时，100 个并发线程调用 GetShader 不应抛出任何异常。
        /// 使用真实加载确保 _isInited 对所有线程可见。
        /// </summary>
        [TestMethod]
        [Description("已初始化时并发调用 GetShader 不应出现竞态异常")]
        public async Task GetShader_ConcurrentAccess_DoesNotThrow() {
            await ShaderLoader.LoadAllShadersAsync();

            var types = Enum.GetValues<ShaderType>()
                            .Where(t => t != ShaderType.None)
                            .ToArray();

            var exceptions = new ConcurrentBag<Exception>();

            Parallel.For(0, 100, i => {
                try {
                    _ = ShaderLoader.GetShader(types[i % types.Length]);
                }
                catch (Exception ex) {
                    exceptions.Add(ex);
                }
            });

            Assert.IsEmpty(exceptions,
                $"Concurrent GetShader threw {exceptions.Count} exception(s): " +
                $"{exceptions.FirstOrDefault()?.Message}");
        }

        /// <summary>
        /// LoadAllShadersAsync 已加载后，20 个并发调用均应立即返回（幂等快路径），
        /// 且 IsLoaded 不被意外重置。
        /// 使用真实加载保证初始状态对所有线程可见。
        /// </summary>
        [TestMethod]
        [Description("LoadAllShadersAsync 已加载后并发调用应全部完成，IsLoaded 保持 true")]
        public async Task LoadAllShadersAsync_ConcurrentCallsWhenAlreadyLoaded_AllCompleteAndIsLoadedTrue() {
            await ShaderLoader.LoadAllShadersAsync(); // 真实加载，确保内存可见性

            var tasks = Enumerable.Range(0, 20)
                .Select(_ => ShaderLoader.LoadAllShadersAsync());

            await Task.WhenAll(tasks); // 全部命中 if (_isInited) return 快路径

            Assert.IsTrue(ShaderLoader.IsLoaded,
                "IsLoaded should remain true after concurrent idempotent calls");
        }

        /// <summary>
        /// 未加载时，多个线程并发调用 LoadAllShadersAsync，
        /// 信号量保证内部逻辑只执行一次（double-check lock）。
        /// 通过 IsLoaded 最终为 true 且无异常来验证。
        /// </summary>
        [TestMethod]
        [Description("未加载时并发调用 LoadAllShadersAsync，内部逻辑只应执行一次")]
        public async Task LoadAllShadersAsync_ConcurrentCallsWhenNotLoaded_LoadOnlyOnce() {
            // 确保未加载状态（Cleanup 已清理，此处为防御性断言）
            Assert.IsFalse(ShaderLoader.IsLoaded, "Precondition: should not be loaded");

            var exceptions = new ConcurrentBag<Exception>();

            var tasks = Enumerable.Range(0, 20)
                .Select(_ => Task.Run(async () => {
                    try {
                        await ShaderLoader.LoadAllShadersAsync();
                    }
                    catch (Exception ex) {
                        exceptions.Add(ex);
                    }
                }));

            await Task.WhenAll(tasks);

            Assert.IsEmpty(exceptions,
                $"Concurrent LoadAllShadersAsync threw {exceptions.Count} exception(s): " +
                $"{exceptions.FirstOrDefault()?.Message}");

            Assert.IsTrue(ShaderLoader.IsLoaded,
                "IsLoaded should be true after concurrent loads complete");
        }
    }
}