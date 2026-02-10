using System;
using Microsoft.Graphics.Canvas;
using VirtualPaper.Common;
using Workloads.Creation.StaticImg.InkSystem.Utils;
using Workloads.Creation.StaticImg.Models.SerializableData;

namespace Workloads.Creation.StaticImg.Core.Utils {
    public class InkProjectSession : IDisposable {
        public string SessionId { get; } = Guid.NewGuid().ToString();

        // 资源属性
        public CanvasDevice SharedDevice { get; private set; }
        public StaticImgUndoRedoUtil UnReUtil { get; private set; }
        public ProjectFile ProjectUtil { get; private set; }
        public FileType RTFileType { get; private set; }

        // 事件
        public event EventHandler? SessionDisposed;

        public InkProjectSession(string filePath, FileType type) {
            RTFileType = type;
            ProjectUtil = ProjectFile.Create(filePath);
            Initialize();
        }

        public InkProjectSession(string fileName) {
            RTFileType = FileType.FDesign;
            ProjectUtil = ProjectFile.Create(fileName);
            Initialize();
        }

        private void Initialize() {
            SharedDevice = CanvasDevice.GetSharedDevice();
            UnReUtil = new StaticImgUndoRedoUtil();
        }

        public void Dispose() {
            SharedDevice?.Dispose();
            UnReUtil?.Dispose();
            SessionDisposed?.Invoke(this, EventArgs.Empty);
        }
    }
}
