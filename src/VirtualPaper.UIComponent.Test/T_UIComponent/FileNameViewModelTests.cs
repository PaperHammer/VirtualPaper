using VirtualPaper.UIComponent.ViewModels;

namespace VirtualPaper.UIComponent.Test.T_UIComponent {
    [TestClass]
    public class FileNameViewModelTests {
        [TestMethod]
        public void AddFileItem_ConstructorValidatesDefaultName() {
            var valid = new AddFileItemViewModel("new-folder", maxLength: 20, onlyLength: false);
            var invalid = new AddFileItemViewModel("", maxLength: 20, onlyLength: false);

            Assert.AreEqual("new-folder", valid.NewName);
            Assert.IsTrue(valid.IsNameOk);
            Assert.IsFalse(invalid.IsNameOk);
        }

        [TestMethod]
        public void AddFileItem_NameChangeRaisesNameAndValidationNotifications() {
            var viewModel = new AddFileItemViewModel("valid", maxLength: 20, onlyLength: false);
            var changedProperties = new List<string?>();
            viewModel.PropertyChanged += (_, args) => changedProperties.Add(args.PropertyName);

            viewModel.NewName = "";

            Assert.IsFalse(viewModel.IsNameOk);
            CollectionAssert.Contains(changedProperties, nameof(viewModel.NewName));
            CollectionAssert.Contains(changedProperties, nameof(viewModel.IsNameOk));
        }

        [TestMethod]
        public void Rename_PreservesOldNameAndStartsWithoutCandidate() {
            var viewModel = new RenameViewModel("before.png", maxLength: 30, onlyLength: false);

            Assert.AreEqual("before.png", viewModel.OldName);
            Assert.IsNull(viewModel.NewName);
            Assert.IsFalse(viewModel.IsNameOk);
        }

        [TestMethod]
        [DataRow("a", true)]
        [DataRow("abcd", true)]
        [DataRow("abcde", false)]
        [DataRow("   ", false)]
        public void Rename_EnforcesLengthAndWhitespaceRules(string candidate, bool expected) {
            var viewModel = new RenameViewModel("before", maxLength: 4, onlyLength: true);

            viewModel.NewName = candidate;

            Assert.AreEqual(expected, viewModel.IsNameOk);
        }

        [TestMethod]
        public void Rename_OnlyLengthModeAllowsCharactersRejectedByFileNameMode() {
            string candidate = $"name{Path.GetInvalidFileNameChars()[0]}part";
            var lengthOnly = new RenameViewModel("before", maxLength: 30, onlyLength: true);
            var fileName = new RenameViewModel("before", maxLength: 30, onlyLength: false);

            lengthOnly.NewName = candidate;
            fileName.NewName = candidate;

            Assert.IsTrue(lengthOnly.IsNameOk);
            Assert.IsFalse(fileName.IsNameOk);
        }
    }
}
