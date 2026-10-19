using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Features.HiddenHosts;

namespace TAOM.Tests.Features.HiddenHosts;

/// <summary>
/// #846: the three nameplate bind fields are laid over for the one call of <c>RefreshBinding</c> that copies them to the
/// bound properties, and put back by the finalizer. Vanilla decides its own writes from int caches, never from the
/// field text, so restoring the originals is all the bookkeeping there is.
/// </summary>
[TestClass]
public class VanillaBindsTests
{
    private static NameplateIntel Masked(string text, bool horizon = false) => new(text, horizon);

    [TestMethod]
    public void LayMask_Unmasked_LeavesTheFieldsAlone_AndReturnsUnset()
    {
        string count = "42", extra = "3 wounded", name = "Lord Aldric";

        var state = VanillaBinds.LayMask(NameplateIntel.Unmasked, "Unknown party", ref count, ref extra, ref name);

        Assert.IsFalse(state.IsSet);
        Assert.AreEqual("42", count);
        Assert.AreEqual("3 wounded", extra);
        Assert.AreEqual("Lord Aldric", name);
    }

    [TestMethod]
    public void LayMask_Masked_WritesTheCount_BlanksTheExtraInfo_KeepsTheName()
    {
        string count = "42", extra = "3 wounded", name = "Lord Aldric";

        var state = VanillaBinds.LayMask(Masked("4?"), "Unknown party", ref count, ref extra, ref name);

        Assert.IsTrue(state.IsSet);
        Assert.AreEqual("4?", count);
        Assert.AreEqual(string.Empty, extra);
        Assert.AreEqual("Lord Aldric", name);
    }

    [TestMethod]
    public void LayMask_Horizon_AlsoWritesTheName()
    {
        string count = "42", extra = "3 wounded", name = "Lord Aldric";

        var state = VanillaBinds.LayMask(Masked("...", horizon: true), "Unknown party", ref count, ref extra, ref name);

        Assert.IsTrue(state.IsSet);
        Assert.AreEqual("...", count);
        Assert.AreEqual(string.Empty, extra);
        Assert.AreEqual("Unknown party", name);
    }

    [TestMethod]
    public void LayMask_HorizonWithoutAName_KeepsTheName()
    {
        string count = "42", extra = "", name = "Lord Aldric";

        VanillaBinds.LayMask(Masked("...", horizon: true), null, ref count, ref extra, ref name);

        Assert.AreEqual("Lord Aldric", name);
    }

    [TestMethod]
    public void Restore_PutsTheOriginalsBack()
    {
        string count = "42", extra = "3 wounded", name = "Lord Aldric";
        var state = VanillaBinds.LayMask(Masked("...", horizon: true), "Unknown party", ref count, ref extra, ref name);

        state.Restore(ref count, ref extra, ref name);

        Assert.AreEqual("42", count);
        Assert.AreEqual("3 wounded", extra);
        Assert.AreEqual("Lord Aldric", name);
    }

    [TestMethod]
    public void Restore_PutsBackNulls()
    {
        string count = null!, extra = null!, name = null!;
        var state = VanillaBinds.LayMask(Masked("?"), null, ref count, ref extra, ref name);
        Assert.AreEqual("?", count);

        state.Restore(ref count, ref extra, ref name);

        Assert.IsNull(count);
        Assert.IsNull(extra);
        Assert.IsNull(name);
    }

    [TestMethod]
    public void Restore_Unset_RestoresNothing()
    {
        string count = "vanilla count", extra = "vanilla extra", name = "vanilla name";

        default(VanillaBinds).Restore(ref count, ref extra, ref name);

        Assert.AreEqual("vanilla count", count);
        Assert.AreEqual("vanilla extra", extra);
        Assert.AreEqual("vanilla name", name);
    }

    [TestMethod]
    public void Default_IsUnset() => Assert.IsFalse(default(VanillaBinds).IsSet);
}
