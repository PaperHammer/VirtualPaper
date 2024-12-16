using VirtualPaper.Models.Cores.Interfaces;

namespace VirtualPaper.Models.Cores {
    public class WpLibData : IWpLibData {
        public int Idx { get; set; }
        public IWpBasicData BasicData { get; set; }

        public WpLibData() {
            BasicData = new WpBasicData();
        }

        public bool IsAvailable() {
            return BasicData.IsAvailable();
        }
    }
}
