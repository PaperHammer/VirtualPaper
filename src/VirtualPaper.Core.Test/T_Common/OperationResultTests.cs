using VirtualPaper.Common.Utils;

namespace VirtualPaper.Core.Test.T_Common {
    [TestClass]
    public class OperationResultTests {
        [TestMethod]
        public void Success_IsSuccessTrue_ResultSet() {
            var result = OperationResult<string>.Success("data");

            Assert.IsTrue(result.IsSuccess);
            Assert.AreEqual("data", result.Result);
            Assert.AreEqual(string.Empty, result.ErrorMessage);
        }

        [TestMethod]
        public void Failure_IsSuccessFalse_ErrorMessageSet() {
            var result = OperationResult<int>.Failure("something went wrong");

            Assert.IsFalse(result.IsSuccess);
            Assert.AreEqual("something went wrong", result.ErrorMessage);
            Assert.AreEqual(default(int), result.Result);
        }

        [TestMethod]
        public void Success_WithNull_ResultIsNull() {
            var result = OperationResult<string>.Success(null!);

            Assert.IsTrue(result.IsSuccess);
            Assert.IsNull(result.Result);
        }

        [TestMethod]
        public void Success_WithComplexType_ResultPreserved() {
            var data = new List<int> { 1, 2, 3 };
            var result = OperationResult<List<int>>.Success(data);

            Assert.IsTrue(result.IsSuccess);
            Assert.AreSame(data, result.Result);
        }
    }
}
