using System.Collections.ObjectModel;
using System.Linq;
using VirtualPaper.Models.Mvvm;
using VirtualPaper.UIComponent.Models;

namespace VirtualPaper.UIComponent.ViewModels {
    public partial class GlobalMsgViewModel : ObservableObject {
        public ObservableCollection<GlobalMsgInfo> InfobarMessages { get; set; } = [];

        public void AddMsg(GlobalMsgInfo globalMsgInfo, bool isAllowDuplication = false) {
            if (!isAllowDuplication && GetGlobalMsg(globalMsgInfo.Key) != null) return;            

            globalMsgInfo.PropertyChanged += (sender, args) => {
                if (args.PropertyName == nameof(GlobalMsgInfo.IsOpen)) {
                    if (sender is GlobalMsgInfo msg && !msg.IsOpen) {
                        // 当 IsOpen 变为 false 时，从集合中移除
                        RemoveMsg(msg);
                    }
                }
            };

            InfobarMessages.Add(globalMsgInfo);
        }

        public void CloseAndRemoveMsg(string key) {
            var msg = GetGlobalMsg(key);
            if (msg != null) {
                msg.IsOpen = false;
            }
        }

        private void RemoveMsg(GlobalMsgInfo msg) {
            InfobarMessages.Remove(msg);
        }

        private GlobalMsgInfo GetGlobalMsg(string key) {
            return InfobarMessages.FirstOrDefault(m => m.Key == key);
        }
    }
}
