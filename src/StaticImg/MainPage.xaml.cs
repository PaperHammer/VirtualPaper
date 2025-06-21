using System;
using System.Threading.Tasks;
using Microsoft.Graphics.Canvas;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using VirtualPaper.Common;
using VirtualPaper.Common.Runtime.Draft;
using VirtualPaper.Common.Utils.Bridge;
using Workloads.Creation.StaticImg.Models.ToolItems.Utils;

// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.

namespace Workloads.Creation.StaticImg {
    /// <summary>
    /// An empty page that can be used on its own or navigated to within a Frame.
    /// </summary>
    public sealed partial class MainPage : Page, IRuntime {
        internal static MainPage Instance { get; private set; }
        internal IDraftPanelBridge Bridge { get; }
        internal CanvasDevice SharedDevice { get; }
        internal SI_UndoRedoUtil UnReUtil { get; }
        internal string EntryFilePath { get; }
        internal FileType RTFileType { get; }

        /// <summary>
        /// 静态图像工作页面
        /// </summary>
        /// <param name="entryFilePath">接收类型为 FImage or FE_STATIC_IMG_PROJ 的文件路径</param>
        public MainPage(IDraftPanelBridge bridge, string entryFilePath, FileType rtFileType) {
            Instance = this;
            Bridge = bridge;
            EntryFilePath = entryFilePath;
            RTFileType = rtFileType;
            SharedDevice = CanvasDevice.GetSharedDevice();
            UnReUtil = new SI_UndoRedoUtil();

            this.InitializeComponent();
        }

        private async void Page_Loaded(object sender, RoutedEventArgs e) {
            this.IsEnabled = false;
            Bridge.GetNotify().Loading(false, false);

            await InkCanvas.IsInited.Task;

            Bridge.GetNotify().Loaded();
            this.IsEnabled = true;
        }

        private void Page_Unloaded(object sender, RoutedEventArgs e) {
            SharedDevice.Dispose();
            UnReUtil.Dispose();
        }

        #region workSpace events
        public async Task SaveAsync() {
            try {
                await InkCanvas.SaveAsync();
            }
            catch (Exception ex) {
                Bridge.Log(LogType.Error, ex);
                Bridge.GetNotify().ShowExp(ex);
            }
        }


        public async Task UndoAsync() {
            try {
                await UnReUtil.UndoAsync();
            }
            catch (Exception ex) {
                Bridge.Log(LogType.Error, ex);
                Bridge.GetNotify().ShowExp(ex);
            }
        }

        public async Task RedoAsync() {
            try {
                await UnReUtil.RedoAsync();
            }
            catch (Exception ex) {
                Bridge.Log(LogType.Error, ex);
                Bridge.GetNotify().ShowExp(ex);
            }
        }
        #endregion
    }
}
