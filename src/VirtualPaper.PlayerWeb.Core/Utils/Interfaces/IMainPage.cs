using VirtualPaper.Common.Runtime.PlayerWeb;

namespace VirtualPaper.PlayerWeb.Core.Utils.Interfaces {
    interface IMainPage {
        StartArgsWeb StartArgs { get; }
        DataConfigTab AvailableConfigTab { get; }
    }
}
