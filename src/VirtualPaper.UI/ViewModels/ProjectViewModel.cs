using System.Collections.Generic;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using VirtualPaper.Common;
using VirtualPaper.Common.Utils.Files;
using VirtualPaper.Common.Utils.Storage;
using VirtualPaper.Common.Utils.Bridge;
using VirtualPaper.Models.Mvvm;
using VirtualPaper.DraftPanel.Views;
using VirtualPaper.UI.Utils;
using Windows.Storage;

namespace VirtualPaper.UI.ViewModels
{
    //internal partial class ProjectViewModel : ObservableObject {
    //    private bool workSpaceVisible;
    //    public bool WorkSpaceVisible {
    //        get { return workSpaceVisible; }
    //        set { workSpaceVisible = value; OnPropertyChanged(); }
    //    }

    //    private object frameContent;
    //    public object FrameContent {
    //        get { return frameContent; }
    //        set { frameContent = value; OnPropertyChanged(); }
    //    }        

    //    public ProjectViewModel() {
    //        _scope = App.Services.CreateScope();
    //        NavigetBasedState(false, nextState: DraftPanelState.Startup);
    //    }

    //    #region TypeConfig
    //    private TypeConfig InitTypeConfig() {
    //        var typeConfig = _scope.ServiceProvider.GetRequiredService<TypeConfig>();

    //        return typeConfig;
    //    }
    //    #endregion

    //    private void ToWorkSpace(object proj = null) {
    //        NavigetBasedState(true, proj, DraftPanelState.WorkSpace);
    //    }

    //    internal void NavigetBasedState(bool isScopeChangetoWorkSpace, object param = null, DraftPanelState nextState = DraftPanelState.WorkSpace) {
    //        if (isScopeChangetoWorkSpace) {
    //            _scope?.Dispose();
    //            _scope = App.Services.CreateScope();
    //        }
    //        WorkSpaceVisible = isScopeChangetoWorkSpace;

    //        object content = null;
    //        switch (nextState) {
    //            case DraftPanelState.Startup:
    //                content = InitGetStart();
    //                break;
    //            case DraftPanelState.ProjectTypeConfig:
    //                content = InitTypeConfig();
    //                break;
    //            case DraftPanelState.DraftConfig:
    //                content = null;
    //                break;
    //            case DraftPanelState.WorkSpace:
    //                content = null;
    //                break;
    //            default:
    //                break;
    //        }

    //        FrameContent = content;
    //    }

    //    internal IServiceScope _scope;
    //    public IWindowBridge windowBridge;
    //    private StorageFile[] _storage;
    //}
}
