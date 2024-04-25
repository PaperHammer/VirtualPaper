namespace VirtualPaper.Common.Models
{
    public class StringValueChangedEventArgs : EventArgs
    {
        public string ControlName { get; set; } = string.Empty;
        public string PropertyName { get; set; } = string.Empty;
        public string Value { get; set; } = string.Empty;
    }
}
