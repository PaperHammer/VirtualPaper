using VirtualPaper.Common;
using VirtualPaper.Common.Utils.Storage;
using VirtualPaper.Shader;

namespace StaticImg.Test {
    [TestClass]
    public class TestSetup {
        [AssemblyInitialize]
        public static void AssemblyInit(TestContext context) {
            Constants.IsTestMode = true;
            FileShared.Write(new SharedContext { BaseDir = AppContext.BaseDirectory });
        }

        [AssemblyCleanup]
        public static void Cleanup() {
            ShaderLoader.ClearCache();
            FileShared.Cleanup();
        }
    }
}
