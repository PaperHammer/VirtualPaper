using System;
using System.Collections.Generic;
using Microsoft.UI.Xaml.Controls;

namespace VirtualPaper.UIComponent.Others;

public sealed partial class MessageSummaryView : UserControl {
    public MessageSummaryView(IReadOnlyList<string> messages) {
        InitializeComponent();
        Messages.ItemsSource = messages;
        Messages.MaxHeight = Math.Clamp(messages.Count * 56d, 56d, 320d);
    }
}
