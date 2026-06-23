using VirtualPaper.Common.Utils;

namespace VirtualPaper.Core.Test.T_Common {
    [TestClass]
    public class ComplianceUtilTests {
        // ── IsValidFolderPath ─────────────────────────────────────────

        [TestMethod]
        [DataRow(@"C:\Users\Test")]
        [DataRow(@"D:\Wallpapers\Sub")]
        [DataRow(@"\\server\share")]
        public void IsValidFolderPath_ValidPaths_ReturnsTrue(string path) {
            Assert.IsTrue(ComplianceUtil.IsValidFolderPath(path));
        }

        [TestMethod]
        [DataRow(null)]
        [DataRow("")]
        [DataRow("AB")]           // 太短
        [DataRow("C:folder")]     // 缺少反斜杠
        [DataRow("folder")]       // 无盘符
        public void IsValidFolderPath_InvalidPaths_ReturnsFalse(string? path) {
            Assert.IsFalse(ComplianceUtil.IsValidFolderPath(path));
        }

        [TestMethod]
        public void IsValidFolderPath_TooLong_ReturnsFalse() {
            var longPath = @"C:\" + new string('a', 300);
            Assert.IsFalse(ComplianceUtil.IsValidFolderPath(longPath));
        }

        // ── IsValidName ───────────────────────────────────────────────

        [TestMethod]
        [DataRow("MyWallpaper")]
        [DataRow("test_file")]
        [DataRow("file.name")]
        public void IsValidName_ValidNames_ReturnsTrue(string name) {
            Assert.IsTrue(ComplianceUtil.IsValidName(name));
        }

        [TestMethod]
        [DataRow(null)]
        [DataRow("")]
        [DataRow("a b")]  // 空格不在 ValidChars
        public void IsValidName_InvalidNames_ReturnsFalse(string? name) {
            Assert.IsFalse(ComplianceUtil.IsValidName(name));
        }

        [TestMethod]
        public void IsValidName_ExceedsMaxLen_ReturnsFalse() {
            Assert.IsFalse(ComplianceUtil.IsValidName(new string('x', 31)));
        }

        // ── IsValidValueOnlyLength ────────────────────────────────────

        [TestMethod]
        public void IsValidValueOnlyLength_InRange_ReturnsTrue() {
            Assert.IsTrue(ComplianceUtil.IsValidValueOnlyLength("hello", 1, 10));
        }

        [TestMethod]
        [DataRow(null)]
        [DataRow("")]
        public void IsValidValueOnlyLength_EmptyOrNull_ReturnsFalse(string? value) {
            Assert.IsFalse(ComplianceUtil.IsValidValueOnlyLength(value));
        }

        [TestMethod]
        public void IsValidValueOnlyLength_TooShort_ReturnsFalse() {
            Assert.IsFalse(ComplianceUtil.IsValidValueOnlyLength("ab", 3, 10));
        }

        // ── IsValidEmail ──────────────────────────────────────────────

        [TestMethod]
        [DataRow("user@example.com")]
        [DataRow("test.name@domain.org")]
        public void IsValidEmail_ValidEmails_ReturnsTrue(string email) {
            Assert.IsTrue(ComplianceUtil.IsValidEmail(email));
        }

        [TestMethod]
        [DataRow("")]
        [DataRow("noatsign")]
        [DataRow("@nodomain")]
        [DataRow("user@")]
        [DataRow("user@domain")]
        public void IsValidEmail_InvalidEmails_ReturnsFalse(string email) {
            Assert.IsFalse(ComplianceUtil.IsValidEmail(email));
        }

        // ── IsValidPwd ────────────────────────────────────────────────

        [TestMethod]
        [DataRow("Abcdef1!")]
        [DataRow("MyP@ssw0rd")]
        [DataRow("Str0ng#Pass")]
        public void IsValidPwd_ValidPasswords_ReturnsTrue(string pwd) {
            Assert.IsTrue(ComplianceUtil.IsValidPwd(pwd));
        }

        [TestMethod]
        [DataRow("")]
        [DataRow("shrt1A!")]       // 太短（<8）
        [DataRow("alllowercase1!")] // 无大写
        [DataRow("ALLUPPERCASE1!")] // 无小写
        [DataRow("NoDigitsHere!")]  // 无数字
        [DataRow("NoSpecial1a")]    // 无特殊字符
        public void IsValidPwd_InvalidPasswords_ReturnsFalse(string pwd) {
            Assert.IsFalse(ComplianceUtil.IsValidPwd(pwd));
        }

        // ── IsValidUserName ───────────────────────────────────────────

        [TestMethod]
        [DataRow("alice")]
        [DataRow("user123")]
        [DataRow("john.doe")]
        [DataRow("user-name")]
        [DataRow("user_name")]
        public void IsValidUserName_ValidNames_ReturnsTrue(string name) {
            Assert.IsTrue(ComplianceUtil.IsValidUserName(name));
        }

        [TestMethod]
        [DataRow("")]
        [DataRow("ab")]              // 太短（<3）
        [DataRow(".startswithdot")]  // 以 . 开头
        [DataRow("-startswithdash")] // 以 - 开头
        [DataRow("endswithdot.")]    // 以 . 结尾
        [DataRow("endswithdash-")]   // 以 - 结尾
        [DataRow("has..double")]     // 连续 ..
        [DataRow("has--double")]     // 连续 --
        [DataRow("has._mixed")]      // 连续 ._
        [DataRow("has-.mixed")]      // 连续 -.
        public void IsValidUserName_InvalidNames_ReturnsFalse(string name) {
            Assert.IsFalse(ComplianceUtil.IsValidUserName(name));
        }

        [TestMethod]
        [DataRow("admin")]
        [DataRow("root")]
        [DataRow("test")]
        [DataRow("guest")]
        [DataRow("user")]
        [DataRow("system")]
        public void IsValidUserName_BlacklistedNames_ReturnsFalse(string name) {
            Assert.IsFalse(ComplianceUtil.IsValidUserName(name));
        }

        [TestMethod]
        public void IsValidUserName_BlacklistCaseInsensitive_ReturnsFalse() {
            Assert.IsFalse(ComplianceUtil.IsValidUserName("Admin"));
            Assert.IsFalse(ComplianceUtil.IsValidUserName("ROOT"));
        }

        // ── IsValidSign ───────────────────────────────────────────────

        [TestMethod]
        public void IsValidSign_NormalText_ReturnsTrue() {
            Assert.IsTrue(ComplianceUtil.IsValidSign("Hello World! 你好世界 🎉"));
        }

        [TestMethod]
        public void IsValidSign_EmptyString_ReturnsTrue() {
            Assert.IsTrue(ComplianceUtil.IsValidSign(""));
        }

        [TestMethod]
        public void IsValidSign_TooLong_ReturnsFalse() {
            Assert.IsFalse(ComplianceUtil.IsValidSign(new string('a', 101)));
        }
    }
}
