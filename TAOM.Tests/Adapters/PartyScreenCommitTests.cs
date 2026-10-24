using System.IO;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using TAOM.Adapters;
using TAOM.Core.Logging;
using TAOM.Tests.Infrastructure;

namespace TAOM.Tests.Adapters;

/// <summary>
/// One place commits a party screen left open under a conversation (adapters.md, "A write made in a conversation opened from the
/// party screen is reverted by Escape"), and only when the screen is the main party's own: the ransom broker's screen binds cloned
/// rosters and sells their prisoners again when it is forced done.
/// </summary>
[TestClass]
public class PartyScreenCommitTests
{
    [TestMethod]
    [TestCategory("RequiresGame")]
    public void CommitIfMainParty_NoPartyScreenOpen_DoesNothingAndLogsNothing()
    {
        var logger = Substitute.For<IModLogger>();

        PartyScreenCommit.CommitIfMainParty(logger, "Test");

        logger.DidNotReceiveWithAnyArgs().LogError(default!);
    }

    [TestMethod]
    public void OnlyTheSharedHelper_ForcesAPartyScreenDone()
    {
        var callers = RepoPaths.CSharpFiles()
            .Where(f => RepoPaths.MentionsOutsideComments(f, ".DoneLogic("))
            .Select(Path.GetFileName)
            .OrderBy(n => n, System.StringComparer.Ordinal)
            .ToList();

        CollectionAssert.AreEqual(new[] { "PartyScreenCommit.cs" }, callers);
    }

    [DataTestMethod]
    [DataRow("Main/Adapters/SwornAnewAdapter.cs", "SwornAnew")]
    [DataRow("Main/Adapters/HardQuestionsActionAdapter.cs", "HardQuestions")]
    [DataRow("Main/Adapters/GiftItemAdapter.cs", "Gifts")]
    [DataRow("Main/Adapters/ProvenderWorldAdapter.cs", "Provender")]
    public void TheAdapterCommit_GoesThroughTheHelper_UnderItsOwnTag(string path, string tag)
    {
        var src = RepoPaths.ReadSource(path, stripComments: true);

        StringAssert.Contains(src, "PartyScreenCommit.CommitIfMainParty(_logger, \"" + tag + "\")");
    }

    [TestMethod]
    public void TheHelper_ChecksTheRightOwnerAndTheRansomMode_BeforeForcingItDone()
    {
        var src = RepoPaths.ReadSource("Main/Adapters/PartyScreenCommit.cs", stripComments: true);

        var owner = src.IndexOf("logic.RightOwnerParty != PartyBase.MainParty", System.StringComparison.Ordinal);
        var ransom = src.IndexOf("PartyScreenHelper.PartyScreenMode.Ransom && logic.PrisonerRosters[(int)PartyScreenLogic.PartyRosterSide.Left].TotalManCount > 0", System.StringComparison.Ordinal);
        var done = src.IndexOf("DoneLogic(isForced: true)", System.StringComparison.Ordinal);
        Assert.IsTrue(owner >= 0 && ransom > owner && done > ransom, "the owner check and the ransom skip come first");
        StringAssert.Contains(src, "catch (Exception ex)");
    }
}
