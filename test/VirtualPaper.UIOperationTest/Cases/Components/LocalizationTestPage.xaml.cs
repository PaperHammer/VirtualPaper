using Microsoft.UI.Xaml.Controls;

namespace VirtualPaper.UIOperationTest.Cases;

public sealed partial class LocalizationTestPage : Page {
    internal Border TestSurface => Surface;
    public LocalizationTestPage() => InitializeComponent();
}
