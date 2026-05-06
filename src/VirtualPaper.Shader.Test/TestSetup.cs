using VirtualPaper.Common;

namespace VirtualPaper.Shader.Test {
    [TestClass]
    public class TestSetup {
        [AssemblyInitialize]
        public static void AssemblyInit(TestContext context) {
            Constants.IsTestMode = true;
        }
    }
}
