using VirtualPaper.Common.Utils.Hardware;

namespace VirtualPaper.Utils.Interfcaes {
    public interface IPowerService {
        PowerUtil.ACLineStatus GetACPowerStatus();
        PowerUtil.SystemStatusFlag GetBatterySaverStatus();
    }
}
