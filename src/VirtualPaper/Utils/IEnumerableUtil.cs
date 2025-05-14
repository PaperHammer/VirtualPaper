namespace VirtualPaper.Utils {
    public static class IEnumerableUtil {
        public static int FindIndex<T>(this IEnumerable<T> source, T target) {
            ArgumentNullException.ThrowIfNull(source);
            ArgumentNullException.ThrowIfNull(nameof(target));

            int i = 0;
            foreach (var item in source) {
                if (item != null && item.Equals(target))
                    return i;
                i++;
            }

            return i;
        }

        public static int FindIndex<T>(this IEnumerable<T> source, Func<T, bool> predicate) {
            ArgumentNullException.ThrowIfNull(source);
            ArgumentNullException.ThrowIfNull(predicate);

            int i = 0;
            foreach (var item in source) {
                if (predicate(item))
                    return i;
                i++;
            }

            return -1;
        }
    }
}
