using VirtualPaper.Common.Models;

namespace VirtualPaper.Common.Utils.ObserverMode
{
    public interface ICustomizeValueChangedObserver
    {
        void OnCustomizeValueChanged(object sender, DoubleValueChangedEventArgs args);
        void OnCustomizeValueChanged(object sender, BoolValueChangedEventArgs args);
        void OnCustomizeValueChanged(object sender, StringValueChangedEventArgs args);
    }
}
