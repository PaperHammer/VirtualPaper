using System.Reflection;
using System.Runtime.CompilerServices;
using VirtualPaper.Common.Utils.Containers;

namespace VirtualPaper.Core.Test.T_Common;

[TestClass]
[DoNotParallelize]
public sealed class StringKeyWeakCacheTests {
    [TestMethod]
    public void LookupAndReplacement_ReturnTheCurrentObject_AndRejectInvalidValues() {
        using var cache = new StringKeyWeakCache<object>();
        var first = new object();
        var second = new object();
        Assert.IsNull(cache.Get("missing"));
        Assert.IsNull(cache.Get(""));
        Assert.IsNull(cache.Get(null!));
        cache.Set("", first);
        cache.Set(null!, first);
        Assert.Throws<ArgumentNullException>(() => cache.Set("invalid", null!));
        Assert.IsEmpty(Entries(cache));
        cache.Set("image", first);
        Assert.AreSame(first, cache.Get("image"));
        cache.Set("image", second);
        Assert.AreSame(second, cache.Get("image"));
        Assert.HasCount(1, Entries(cache));
        GC.KeepAlive(first);
        GC.KeepAlive(second);
    }

    [TestMethod]
    public void Cache_DoesNotKeepItsValuesAlive_AndLookupRemovesACollectedEntry() {
        using var cache = new StringKeyWeakCache<object>();
        WeakReference reference = SeedCollectible(cache);
        GC.Collect(GC.MaxGeneration, GCCollectionMode.Forced, blocking: true, compacting: true);
        Assert.IsFalse(reference.IsAlive, "The cache must not own a strong reference to its value.");
        Assert.IsNull(cache.Get("collectible"));
        Assert.IsEmpty(Entries(cache));
    }

    [TestMethod]
    public void AccessCleanup_RemovesDeadKeysAndRetainsLiveEntries() {
        using var cache = new StringKeyWeakCache<object>();
        var live = new object();
        cache.Set("live", live);
        cache.Set("dead", new object());
        Entries(cache)["dead"].Reference.Target = null;
        for (int index = 0; index < 20; index++) Assert.AreSame(live, cache.Get("live"));
        Assert.HasCount(1, Entries(cache));
        Assert.IsTrue(Entries(cache).ContainsKey("live"));
        GC.KeepAlive(live);
    }

    [TestMethod]
    public void CapacityCleanup_RemovesDeadEntriesWithoutDroppingLiveValues() {
        using var cache = new StringKeyWeakCache<object>();
        var values = Enumerable.Range(0, 51).Select(_ => new object()).ToArray();
        for (int index = 0; index < 50; index++) cache.Set(index.ToString(), values[index]);
        foreach (string key in new[] { "0", "7", "49" }) Entries(cache)[key].Reference.Target = null;
        cache.Set("50", values[50]);
        Assert.HasCount(48, Entries(cache));
        foreach (string key in new[] { "0", "7", "49" }) Assert.IsNull(cache.Get(key));
        Assert.AreSame(values[50], cache.Get("50"));
        Assert.AreSame(values[20], cache.Get("20"));
        GC.KeepAlive(values);
    }

    [TestMethod]
    public void ScheduledCleanup_ExpiresIdleEntries_WhileAccessRefreshesTheirLifetime() {
        using var cache = new StringKeyWeakCache<object>();
        var expired = new object();
        var refreshed = new object();
        var recent = new object();
        cache.Set("expired", expired);
        cache.Set("refreshed", refreshed);
        cache.Set("recent", recent);
        var entries = Entries(cache);
        entries["expired"] = (entries["expired"].Reference, DateTime.Now.AddMinutes(-11));
        entries["refreshed"] = (entries["refreshed"].Reference, DateTime.Now.AddMinutes(-11));
        Assert.AreSame(refreshed, cache.Get("refreshed"));
        typeof(StringKeyWeakCache<object>).GetMethod("TimerCleanup", BindingFlags.NonPublic | BindingFlags.Instance)!.Invoke(cache, [null]);
        Assert.IsNull(cache.Get("expired"));
        Assert.AreSame(refreshed, cache.Get("refreshed"));
        Assert.AreSame(recent, cache.Get("recent"));
        GC.KeepAlive(expired);
        GC.KeepAlive(refreshed);
        GC.KeepAlive(recent);
    }

    [TestMethod]
    public async Task ConcurrentReadsAndReplacements_DoNotLoseIndependentKeys() {
        using var cache = new StringKeyWeakCache<object>();
        var values = Enumerable.Range(0, 80).Select(_ => new object()).ToArray();
        await Task.WhenAll(values.Select((value, index) => Task.Run(() => {
            string key = index.ToString();
            for (int repeat = 0; repeat < 30; repeat++) {
                cache.Set(key, value);
                Assert.AreSame(value, cache.Get(key));
            }
        })));
        for (int index = 0; index < values.Length; index++) Assert.AreSame(values[index], cache.Get(index.ToString()));
        cache.Dispose();
        cache.Dispose();
        Assert.IsEmpty(Entries(cache));
        foreach (int index in Enumerable.Range(0, values.Length)) Assert.IsNull(cache.Get(index.ToString()));
        GC.KeepAlive(values);
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static WeakReference SeedCollectible(StringKeyWeakCache<object> cache) {
        var value = new object();
        cache.Set("collectible", value);
        return new WeakReference(value);
    }

    private static Dictionary<string, (WeakReference Reference, DateTime LastAccess)> Entries(StringKeyWeakCache<object> cache) =>
        (Dictionary<string, (WeakReference, DateTime)>)typeof(StringKeyWeakCache<object>)
            .GetField("_cache", BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(cache)!;
}
