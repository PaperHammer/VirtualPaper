using System;
using System.Collections.Generic;
using System.Linq;
using Windows.Foundation;

namespace Workloads.Creation.StaticImg.Models {
    public class SpatialIndex<T> {
        private readonly List<(Rect Bounds, T Item)> _items = [];

        public void Insert(Rect bounds, T item) => _items.Add((bounds, item));

        public IEnumerable<T> Query(Point point) {
            return _items.Where(x => x.Bounds.Contains(point))
                        .Select(x => x.Item);
        }

        //public IEnumerable<T> Query(Rect area, ContainmentMode containment = ContainmentMode.Intersects) {
        //    lock (_lock) {
        //        return containment switch {
        //            ContainmentMode.Contains => _items.Where(x => area.Contains(x.Bounds))
        //                                                .Select(x => x.Item)
        //                                                .ToList(),
        //            ContainmentMode.Intersects => _items.Where(x => x.Bounds.IntersectsWith(area))
        //                                                .Select(x => x.Item)
        //                                                .ToList(),
        //            ContainmentMode.ContainedWithin => _items.Where(x => x.Bounds.Contains(area))
        //                                                .Select(x => x.Item)
        //                                                .ToList(),
        //            _ => throw new ArgumentOutOfRangeException(nameof(containment), containment, null),
        //        };
        //    }
        //}
    }
}
