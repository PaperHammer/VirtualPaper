using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;

namespace VirtualPaper.WpSettingsPanel.Utils {
    // Keep display order and the existing semicolon format. Commas in author
    // names are not separators, unlike commas in pasted tag lists.
    internal sealed class WallpaperInfoTokens {
        private readonly char[] _separators;
        public ObservableCollection<string> Items { get; } = [];

        internal WallpaperInfoTokens(IEnumerable<string> values, bool splitCommas) {
            _separators = splitCommas ? [';', '；', ',', '，', '\r', '\n'] : [';', '；', '\r', '\n'];
            foreach (string value in values) Add(value);
        }

        internal void Add(string text) {
            foreach (string part in text.Split(_separators, StringSplitOptions.RemoveEmptyEntries)) {
                string item = part.Trim();
                if (item.Length > 0 && !Items.Contains(item, StringComparer.OrdinalIgnoreCase)) Items.Add(item);
            }
        }

        // Include pending text without changing the form while Save is in progress.
        internal string Serialize(string pendingText) {
            var snapshot = new WallpaperInfoTokens(Items, _separators.Contains(','));
            snapshot.Add(pendingText);
            return string.Join(';', snapshot.Items);
        }
    }
}
