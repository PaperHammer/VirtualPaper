using VirtualPaper.Common.Utils.UndoRedo;

namespace VirtualPaper.Core.Test.T_Common {
    [TestClass]
    public class UndoRedoUtilTests {
        private UndoRedoUtil<IUndoableCommand> _util = null!;

        [TestInitialize]
        public void Setup() {
            _util = new UndoRedoUtil<IUndoableCommand>(isSaved: true);
        }

        [TestCleanup]
        public void Cleanup() {
            _util.Dispose();
        }

        // ── 辅助 ─────────────────────────────────────────────────────

        private static IUndoableCommand MakeCommand(string desc = "cmd") {
            int execCount = 0, undoCount = 0;
            return new TestCommand(desc,
                () => execCount++,
                () => undoCount++);
        }

        private sealed class TestCommand : IUndoableCommand {
            public string Description { get; }
            private readonly Action _exec;
            private readonly Action _undo;
            public int ExecCount;
            public int UndoCount;

            public TestCommand(string desc, Action exec, Action undo) {
                Description = desc;
                _exec = exec;
                _undo = undo;
            }

            public Task ExecuteAsync() { ExecCount++; _exec(); return Task.CompletedTask; }
            public Task UndoAsync() { UndoCount++; _undo(); return Task.CompletedTask; }
        }

        // ── Record ────────────────────────────────────────────────────

        [TestMethod]
        public void Record_IncreasesUndoStackSize() {
            _util.Record(MakeCommand());
            Assert.AreEqual(1, _util.UndoStackSize);

            _util.Record(MakeCommand());
            Assert.AreEqual(2, _util.UndoStackSize);
        }

        [TestMethod]
        public async Task Record_ClearsRedoStack() {
            _util.Record(MakeCommand());
            _util.Record(MakeCommand());
            await _util.UndoAsync();
            Assert.IsTrue(_util.CanRedo);

            _util.Record(MakeCommand());
            Assert.IsFalse(_util.CanRedo);
        }

        [TestMethod]
        public void Record_WhenMaxStackSize_ExceedsOldestRemoved() {
            using var limited = new UndoRedoUtil<IUndoableCommand>(isSaved: true, maxStackSize: 3);

            limited.Record(MakeCommand("a"));
            limited.Record(MakeCommand("b"));
            limited.Record(MakeCommand("c"));
            limited.Record(MakeCommand("d"));

            Assert.AreEqual(3, limited.UndoStackSize);
        }

        // ── UndoAsync ─────────────────────────────────────────────────

        [TestMethod]
        public async Task UndoAsync_WithCommand_MovesToRedoStack() {
            _util.Record(MakeCommand());

            bool result = await _util.UndoAsync();

            Assert.IsTrue(result);
            Assert.AreEqual(0, _util.UndoStackSize);
            Assert.AreEqual(1, _util.RedoStackSize);
        }

        [TestMethod]
        public async Task UndoAsync_EmptyStack_ReturnsFalse() {
            bool result = await _util.UndoAsync();
            Assert.IsFalse(result);
        }

        [TestMethod]
        public async Task UndoAsync_CallsCommandUndo() {
            var cmd = new TestCommand("test", () => { }, () => { });
            _util.Record(cmd);

            await _util.UndoAsync();

            Assert.AreEqual(1, cmd.UndoCount);
            Assert.AreEqual(0, cmd.ExecCount);
        }

        [TestMethod]
        public async Task UndoAsync_MultipleCommands_UndoesLastFirst() {
            var cmd1 = new TestCommand("first", () => { }, () => { });
            var cmd2 = new TestCommand("second", () => { }, () => { });
            _util.Record(cmd1);
            _util.Record(cmd2);

            await _util.UndoAsync();

            Assert.AreEqual(1, cmd2.UndoCount);
            Assert.AreEqual(0, cmd1.UndoCount);
        }

        // ── RedoAsync ─────────────────────────────────────────────────

        [TestMethod]
        public async Task RedoAsync_AfterUndo_MovesBackToUndoStack() {
            _util.Record(MakeCommand());
            await _util.UndoAsync();

            bool result = await _util.RedoAsync();

            Assert.IsTrue(result);
            Assert.AreEqual(1, _util.UndoStackSize);
            Assert.AreEqual(0, _util.RedoStackSize);
        }

        [TestMethod]
        public async Task RedoAsync_EmptyRedoStack_ReturnsFalse() {
            bool result = await _util.RedoAsync();
            Assert.IsFalse(result);
        }

        [TestMethod]
        public async Task RedoAsync_CallsCommandExecute() {
            var cmd = new TestCommand("test", () => { }, () => { });
            _util.Record(cmd);
            await _util.UndoAsync();

            await _util.RedoAsync();

            // ExecuteAsync called once during Redo
            Assert.AreEqual(1, cmd.ExecCount);
        }

        // ── Undo → Redo → Undo 往返 ──────────────────────────────────

        [TestMethod]
        public async Task UndoRedoRoundtrip_ThreeCommands() {
            var cmd1 = new TestCommand("a", () => { }, () => { });
            var cmd2 = new TestCommand("b", () => { }, () => { });
            var cmd3 = new TestCommand("c", () => { }, () => { });
            _util.Record(cmd1);
            _util.Record(cmd2);
            _util.Record(cmd3);

            // Undo c, b, a
            await _util.UndoAsync();
            await _util.UndoAsync();
            await _util.UndoAsync();

            Assert.AreEqual(0, _util.UndoStackSize);
            Assert.AreEqual(3, _util.RedoStackSize);

            // Redo a, b, c
            await _util.RedoAsync();
            await _util.RedoAsync();
            await _util.RedoAsync();

            Assert.AreEqual(3, _util.UndoStackSize);
            Assert.AreEqual(0, _util.RedoStackSize);
        }

        // ── IsSaved / MarkAsSaved ─────────────────────────────────────

        [TestMethod]
        public void IsSaved_InitiallyTrue_WhenConstructedAsSaved() {
            Assert.IsTrue(_util.IsSaved);
        }

        [TestMethod]
        public void IsSaved_FalseAfterRecord() {
            _util.Record(MakeCommand());
            Assert.IsFalse(_util.IsSaved);
        }

        [TestMethod]
        public async Task IsSaved_TrueAfterMarkAsSaved() {
            _util.Record(MakeCommand());
            Assert.IsFalse(_util.IsSaved);

            _util.MarkAsSaved();
            Assert.IsTrue(_util.IsSaved);
        }

        [TestMethod]
        public async Task IsSaved_FalseAfterUndoFromSavedState() {
            _util.Record(MakeCommand());
            _util.MarkAsSaved();
            Assert.IsTrue(_util.IsSaved);

            await _util.UndoAsync();
            Assert.IsFalse(_util.IsSaved);
        }

        [TestMethod]
        public async Task IsSaved_TrueAfterUndoBackToSavedCommand() {
            _util.Record(MakeCommand("a"));
            _util.Record(MakeCommand("b"));
            _util.MarkAsSaved(); // saved at "b"

            await _util.UndoAsync(); // undo "b"
            Assert.IsFalse(_util.IsSaved);

            await _util.RedoAsync(); // redo "b" → back to saved state
            Assert.IsTrue(_util.IsSaved);
        }

        [TestMethod]
        public void IsSaved_EmptyStackAfterRecord_ReturnsFalseAfterMarkSaved() {
            // 构造时 isSaved=true，空栈 → IsSaved=true
            Assert.IsTrue(_util.IsSaved);

            _util.Record(MakeCommand());
            _util.MarkAsSaved();
            Assert.IsTrue(_util.IsSaved);

            // Clear 不重置 _savedAtEmpty 标记，栈空时 IsSaved 取决于该标记
            // MarkAsSaved 在非空栈时设置 _savedAtEmpty=false，Clear 后仍为 false
            _util.Clear();
            Assert.IsFalse(_util.IsSaved);
        }

        // ── IsSavedChanged 事件 ───────────────────────────────────────

        [TestMethod]
        public void IsSavedChanged_FiresOnRecord() {
            bool? received = null;
            _util.IsSavedChanged += (_, e) => received = e.IsSaved;

            _util.Record(MakeCommand());

            Assert.IsFalse(received);
        }

        [TestMethod]
        public void IsSavedChanged_FiresOnMarkAsSaved() {
            _util.Record(MakeCommand());
            bool? received = null;
            _util.IsSavedChanged += (_, e) => received = e.IsSaved;

            _util.MarkAsSaved();

            Assert.IsTrue(received);
        }

        // ── Clear ─────────────────────────────────────────────────────

        [TestMethod]
        public void Clear_EmptiesBothStacks() {
            _util.Record(MakeCommand());
            _util.Record(MakeCommand());

            _util.Clear();

            Assert.AreEqual(0, _util.UndoStackSize);
            Assert.AreEqual(0, _util.RedoStackSize);
            Assert.IsFalse(_util.CanUndo);
            Assert.IsFalse(_util.CanRedo);
        }

        // ── Dispose ───────────────────────────────────────────────────

        [TestMethod]
        public void Dispose_PreventsFurtherRecord() {
            _util.Dispose();
            // 不应抛异常，静默忽略
            _util.Record(MakeCommand());
            Assert.AreEqual(0, _util.UndoStackSize);
        }

        [TestMethod]
        public async Task Dispose_PreventsFurtherUndo() {
            _util.Record(MakeCommand());
            _util.Dispose();

            bool result = await _util.UndoAsync();
            Assert.IsFalse(result);
        }

        // ── CanUndo / CanRedo ─────────────────────────────────────────

        [TestMethod]
        public void CanUndo_InitiallyFalse() {
            Assert.IsFalse(_util.CanUndo);
        }

        [TestMethod]
        public void CanRedo_InitiallyFalse() {
            Assert.IsFalse(_util.CanRedo);
        }

        [TestMethod]
        public async Task CanUndo_TrueAfterRecord_FalseAfterUndo() {
            _util.Record(MakeCommand());
            Assert.IsTrue(_util.CanUndo);

            await _util.UndoAsync();
            Assert.IsFalse(_util.CanUndo);
        }

        [TestMethod]
        public async Task CanRedo_TrueAfterUndo_FalseAfterRecord() {
            _util.Record(MakeCommand());
            await _util.UndoAsync();
            Assert.IsTrue(_util.CanRedo);

            _util.Record(MakeCommand());
            Assert.IsFalse(_util.CanRedo);
        }
    }
}
