using VirtualPaper.Common;

namespace VirtualPaper.Models {
    public class NetMessage {
        public int Code { get; set; } = -1;
        public string MsgKey { get; set; } = nameof(Constants.I18n.Server_CannotAccess);
        public object? Data { get; set; }
        public string? Token { get; set; }
    }
}
