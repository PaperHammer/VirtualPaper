using VirtualPaper.Common;
using VirtualPaper.Common.Utils.Storage;

namespace VirtualPaper.Shader.Test {
    [TestClass]
    public class TestSetup {
        [AssemblyInitialize]
        public static void AssemblyInit(TestContext context) {
            Constants.IsTestMode = true;
            InitMemorySharedContext();
        }

        private static void InitMemorySharedContext() {
            var context = new SharedContext {
                BaseDir = AppDomain.CurrentDomain.BaseDirectory,
            };
            FileShared.Write(context);  // 写到磁盘某个共享位置
        }
    }
}
