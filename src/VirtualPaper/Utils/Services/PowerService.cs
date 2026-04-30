using VirtualPaper.Common.Utils.Hardware;
using VirtualPaper.Utils.Interfcaes;

namespace VirtualPaper.Utils.Services {
    public class PowerService : IPowerService {
        public PowerUtil.ACLineStatus GetACPowerStatus()
            => PowerUtil.GetACPowerStatus();
        public PowerUtil.SystemStatusFlag GetBatterySaverStatus()
            => PowerUtil.GetBatterySaverStatus();
    }
}
