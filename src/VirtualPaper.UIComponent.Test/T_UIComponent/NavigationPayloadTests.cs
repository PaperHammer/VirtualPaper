using VirtualPaper.UIComponent.Utils;

namespace VirtualPaper.UIComponent.Test.T_UIComponent;

[TestClass]
public sealed class NavigationPayloadTests {
    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public void Merge_RetainsOrReplacesTheSessionAccordingToOverwrite(bool overwrite) {
        object previousSession = new(), incomingSession = new(), context = new();
        var target = new FrameworkPayload { [NaviPayloadKey.WebProjectSession] = previousSession };
        var incoming = new FrameworkPayload {
            [NaviPayloadKey.WebProjectSession] = incomingSession,
            [NaviPayloadKey.ContextKey] = context,
            ["extension-state"] = "retained"
        };
        Assert.AreSame(target, target.Merge(incoming, overwrite));
        Assert.AreSame(overwrite ? incomingSession : previousSession, target.Get<object>(NaviPayloadKey.WebProjectSession));
        Assert.AreSame(context, target.Get<object>(NaviPayloadKey.ContextKey));
        Assert.AreEqual("retained", target.Get<string>("extension-state"));
        Assert.AreSame(incomingSession, incoming.Get<object>(NaviPayloadKey.WebProjectSession));
    }

    [TestMethod]
    public void NavigationProjection_RoundTripsKnownKeys_WithoutLeakingUnrelatedEntries() {
        object session = new(), context = new();
        var payload = new FrameworkPayload {
            [NaviPayloadKey.InkProjectSession] = session,
            [NaviPayloadKey.ContextKey] = context,
            ["private-state"] = new object()
        };
        var restored = new FrameworkPayload().AddRange(payload.ToArray());
        Assert.HasCount(2, restored.GetRawData());
        Assert.AreSame(session, restored.Get<object>(NaviPayloadKey.InkProjectSession));
        Assert.AreSame(context, restored.Get<object>(NaviPayloadKey.ContextKey));
        Assert.IsFalse(restored.ContainsKey("private-state"));
        restored.AddRange(false, new NaviPayloadData(NaviPayloadKey.InkProjectSession, new object()));
        Assert.AreSame(session, restored.Get<object>(NaviPayloadKey.InkProjectSession));
        restored.Remove(NaviPayloadKey.InkProjectSession);
        Assert.IsFalse(restored.TryGet<object>(NaviPayloadKey.InkProjectSession, out _));
        Assert.AreSame(context, restored.Get<object>(NaviPayloadKey.ContextKey));
        Assert.IsFalse(restored.TryGet<int>(NaviPayloadKey.ContextKey, out int wrongType));
        Assert.AreEqual(0, wrongType);
    }

    [TestMethod]
    public void OptionalNavigationPayloads_PreserveTheExistingInstance() {
        FrameworkPayload? absent = null;
        var existing = new FrameworkPayload();
        Assert.AreSame(existing, absent.Merge(existing));
        Assert.AreSame(existing, existing.Merge(null));
        Assert.IsEmpty(absent.Merge(null).GetRawData());
        Assert.AreSame(existing, existing.AddRange(null!));
        Assert.IsEmpty(NavigationPayloadExtensions.ToArray(null!));
    }
}
