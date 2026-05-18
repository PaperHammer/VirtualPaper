using VirtualPaper.Common;

namespace VirtualPaper.UI.Test {
    [TestClass]
    public class TestSetup {
        [AssemblyInitialize]
        public static void AssemblyInit(TestContext context) {
            Constants.IsTestMode = true;
        }
    }
}
