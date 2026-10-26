using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using DryIoc;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using TAOM.Core.Infrastructure;
using TAOM.Core.Logging;
using TAOM.Features.KeepWardens;
using static TAOM.Tests.Infrastructure.RepoPaths;

namespace TAOM.Tests.Features.KeepWardens;

/// <summary>
/// The roster hook (#991) reads engine objects (a mobile party, a troop roster) that a no-game test cannot build, so the
/// decision is tested in <see cref="PatrolRosterServiceTests"/> and these tests pin the boundary and the hook's place in
/// <c>TaomPartySizeModel</c>.
/// </summary>
[TestClass]
public class PatrolRosterHookTests
{
    private const string ModelPath = "Main/Features/CulturalFeats/Models/TaomPartySizeModel.cs";

    private static string HookSource() => ReadSource("Main/Features/KeepWardens/PatrolRosterHook.cs", stripComments: true);

    private static string RosterOverrideBody()
    {
        var src = ReadSource(ModelPath, stripComments: true);
        var start = src.IndexOf("public override TroopRoster FindAppropriateInitialRosterForMobileParty(", System.StringComparison.Ordinal);
        Assert.AreNotEqual(-1, start, "TaomPartySizeModel must override FindAppropriateInitialRosterForMobileParty");
        return src.Substring(start, src.IndexOf(';', start) - start);
    }

    [TestMethod]
    public void TaomPartySizeModel_FitsTheBaseRoster_BaseFirst_InOneExpression()
    {
        // Base runs first, so the roster the engine, TAOM's own Patch39 postfix and the co-op postfix on the base all see is
        // the one that gets fitted. The body is one expression (gamemodels.md rule 4).
        var body = RosterOverrideBody();

        var hook = body.IndexOf("_patrolRoster.FitToLimit(", System.StringComparison.Ordinal);
        var baseCall = body.IndexOf("base.FindAppropriateInitialRosterForMobileParty(party, partyTemplate)", System.StringComparison.Ordinal);
        Assert.AreNotEqual(-1, hook, "the override must call the roster hook");
        Assert.IsTrue(hook < baseCall, "the base call must be the hook's argument, so it runs first");
        Assert.IsFalse(Regex.IsMatch(body, @"\b(if|switch|foreach|for|while)\s*\("), "an override body must hold no branch or loop");
        Assert.AreEqual(2, Regex.Matches(ReadSource(ModelPath, stripComments: true), @"FindAppropriateInitialRosterForMobileParty\(").Count,
            "one override declaration and its one base call");
    }

    [TestMethod]
    public void TaomPartySizeModel_ResolvesTheRosterHookOnceInItsConstructor_AndSubModuleNeverNamesIt()
    {
        var src = ReadSource(ModelPath, stripComments: true);

        StringAssert.Contains(src, "private readonly IPatrolRosterHook _patrolRoster");
        Assert.AreEqual(1, Regex.Matches(src, @"IoC\.Resolve<IPatrolRosterHook>\(\)").Count);
        Assert.IsFalse(File.ReadAllText(RepoPath("Main", "SubModule.cs")).Contains("IPatrolRosterHook"));

        var ctor = src.IndexOf("public TaomPartySizeModel(", System.StringComparison.Ordinal);
        var method = src.IndexOf("GetPartyMemberSizeLimit(", ctor, System.StringComparison.Ordinal);
        var resolve = src.IndexOf("IoC.Resolve<IPatrolRosterHook>", System.StringComparison.Ordinal);
        Assert.IsTrue(ctor >= 0 && ctor < resolve && resolve < method, "the hook must be resolved inside the constructor");
    }

    [TestMethod]
    public void TheHook_LeavesAnyOtherPartyAndANavalPatrolAlone()
    {
        // A naval patrol's template carries ships and fills at its own rules; only a land patrol is fitted.
        var src = HookSource();

        StringAssert.Contains(src, "IsPatrolParty");
        StringAssert.Contains(src, "PatrolPartyComponent.IsNaval");
        Assert.IsTrue(src.IndexOf("IsPatrolParty", System.StringComparison.Ordinal) < src.IndexOf("_service.Fit(", System.StringComparison.Ordinal),
            "the patrol guard must come before the fit");
    }

    [TestMethod]
    public void TheHook_ReadsAFreshLimitFromTheModel_NotTheVersionCachedOne()
    {
        // PartyBase.PartySizeLimit is cached per roster version and can be stale on the spawn call and right after an upgrade.
        var src = HookSource();

        StringAssert.Contains(src, "PartySizeLimitModel.GetPartyMemberSizeLimit(");
        Assert.IsFalse(Regex.IsMatch(src, @"\.PartySizeLimit\b"), "the cached PartySizeLimit property must not be read");
    }

    [TestMethod]
    public void TheModuleRegistration_ResolvesTheRosterHook()
    {
        var container = new Container();
        container.RegisterInstance(Substitute.For<IPathService>());
        container.RegisterInstance(Substitute.For<IModLogger>());
        new KeepWardensModule().RegisterServices(container);

        var errors = container.Validate(typeof(IPatrolRosterHook), typeof(PatrolRosterService));

        Assert.AreEqual(0, errors.Length, string.Join("; ", errors.Select(e => e.Value.Message)));
    }
}
