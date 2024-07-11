namespace VirtualPaper.Common.Models
{
    public class IntValueChangedEventArgs : EventArgs
    {
        public string ControlName { get; set; } = string.Empty;
        public string PropertyName { get; set; } = string.Empty;
        public int Value { get; set; }
    }
}
