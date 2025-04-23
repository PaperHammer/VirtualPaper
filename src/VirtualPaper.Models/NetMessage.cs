namespace VirtualPaper.Models {
    public class NetMessage {
        public int Code { get; set; } = -1;
        public string Msg { get; set; } = "Error";
        public object? Data { get; set; }
        public string? Token { get; set; }
    }
}
