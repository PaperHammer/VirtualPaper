using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Navigation;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Threading.Tasks;
using VirtualPaper.Common.Logging;
using VirtualPaper.Common.Utils.Storage;
using VirtualPaper.Common.Utils.UndoRedo.Events;
using VirtualPaper.UIComponent;
using VirtualPaper.UIComponent.Templates;
using VirtualPaper.UIComponent.Utils;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Workloads.Creation.WebBackdrop.Core.Utils;
using Workloads.Utils.DraftUtils.Interfaces;
using Workloads.Utils.DraftUtils.Models;

// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.

namespace Workloads.Creation.WebBackdrop {
    /// <summary>
    /// An empty page that can be used on its own or navigated to within a Frame.
    /// </summary>
    public sealed partial class MainPage : ArcPage {
        public override Type ArcType => typeof(MainPage);

        public MainPage() {
            InitializeComponent();
        }
    }
    //public sealed partial class MainPage : ArcPage, IRuntime {
    //    public event EventHandler<IsSavedChangedEventArgs>? IsSavedChanged;
    //    public string FileName => Session.DesignFileUtil.FileName;
    //    public string FileNameWithoutEx => Session.DesignFileUtil.FileNameWithoutEx;
    //    public string Id => Session.SessionId;
    //    public override Type ArcType => typeof(MainPage);
    //    protected override bool IsMultiInstance => true;
    //    public WebProjectSession Session { get; private set; }
    //    public bool IsSavedFromInit => Session.DesignFileUtil.IsSaveFromInit;

    //    public MainPage() {
    //        InitializeComponent();
    //    }

    //    #region workSpace events
    //    public async Task<bool> SaveAsync() {
    //        try {
    //            //var res = await inkCanvas.SaveAsync();
    //            //return res;
    //            throw new NotImplementedException();
    //        }
    //        catch (Exception ex) {
    //            ArcLog.GetLogger<MainPage>().Error(ex);
    //            GlobalMessageUtil.ShowException(ex);
    //        }
    //        return false;
    //    }

    //    public async Task<string?> SaveAsAsync() {
    //        try {
    //            var format = Session.DesignFileUtil.ExportFormatDefult;
    //            var path = await ExportAsync(format);
    //            if (path != null) {
    //                Session.DesignFileUtil.SetFilePath(path);
    //                //await inkCanvas.UpdateRecentUsedAsync(path);
    //                throw new NotImplementedException();
    //            }

    //            return path;
    //        }
    //        catch (Exception ex) {
    //            ArcLog.GetLogger<MainPage>().Error(ex);
    //            GlobalMessageUtil.ShowException(ex);
    //        }
    //        return null;
    //    }

    //    public async Task UndoAsync() {
    //        try {
    //            await Session.UnReUtil.UndoAsync();
    //        }
    //        catch (Exception ex) {
    //            ArcLog.GetLogger<MainPage>().Error(ex);
    //            GlobalMessageUtil.ShowException(ex);
    //        }
    //    }

    //    public async Task RedoAsync() {
    //        try {
    //            await Session.UnReUtil.RedoAsync();
    //        }
    //        catch (Exception ex) {
    //            ArcLog.GetLogger<MainPage>().Error(ex);
    //            GlobalMessageUtil.ShowException(ex);
    //        }
    //    }

    //    public async Task<string?> ExportAsync(ExportImageFormat format) {
    //        throw new NotImplementedException();
    //    }
    //    #endregion
    //}
}
