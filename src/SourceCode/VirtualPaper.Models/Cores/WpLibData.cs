using VirtualPaper.Models.Cores.Interfaces;

namespace VirtualPaper.Models.Cores {
    public class WpLibData : IWpLibData {
        public int Idx { get; set; }
        public IWpMetadata Data { get; set; }

        public WpLibData() {
            Data = new WpMetadata();
        }

        public bool IsAvailable() {
            return Data.IsAvailable();
        }
    }
}
