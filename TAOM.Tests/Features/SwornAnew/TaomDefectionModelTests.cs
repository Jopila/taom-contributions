using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using TaleWorlds.CampaignSystem.GameComponents;
using TAOM.Adapters;
using TAOM.Features.CoopInterop;
using TAOM.Features.Execution;
using TAOM.Features.SwornAnew;
using TAOM.Features.SwornAnew.Models;
using TAOM.Features.UncapturableHeroes;
using TAOM.Tests.Infrastructure;
using static TAOM.Tests.Features.SwornAnew.DefectionFactsFixture;

namespace TAOM.Tests.Features.SwornAnew;

/// <summary>
/// The model is one boundary conversion and one service call (gamemodels.md rule 4). The service answers null for a free hero, so
/// vanilla's own check decides; for a prisoner its verdict is the answer. A null hero is vanilla's own false, which tells the two
/// paths apart without an engine hero.
/// </summary>
[TestClass]
[TestCategory("RequiresGame")]
public class TaomDefectionModelTests
{
    private ISwornAnewAdapter _adapter = null!;
    private ISwornAnewSettingsProvider _settings = null!;
    private TaomDefectionModel _model = null!;

    [TestInitialize]
    public void Setup()
    {
        _adapter = Substitute.For<ISwornAnewAdapter>();
        _settings = Substitute.For<ISwornAnewSettingsProvider>();
        _settings.Enabled.Returns(true);
        var alignment = Substitute.For<IAlignmentService>();
        alignment.ResolveSide(Arg.Any<string>(), Arg.Any<string>()).Returns(FactionSide.Free);
        var service = new SwornAnewService(_settings, alignment, Substitute.For<IUncapturableRegistry>(),
            Substitute.For<ICoopSessionProvider>(), Substitute.For<IDedicatedServerProvider>());
        _model = new TaomDefectionModel(service, _adapter);
    }

    [TestMethod]
    public void CanHeroDefectToFaction_APrisonerTheServiceAllows_IsTrue()
    {
        _adapter.Read(null, null).Returns(EligiblePrisoner());

        Assert.IsTrue(_model.CanHeroDefectToFaction(null!, null!));
    }

    [TestMethod]
    public void CanHeroDefectToFaction_AFreeHero_FallsThroughToVanilla()
    {
        // Vanilla's answer for a null hero is false (its first term is hero != null): the verdict the base gives, not the service's.
        _adapter.Read(null, null).Returns(EligiblePrisoner() with { IsPrisoner = false });

        Assert.IsFalse(_model.CanHeroDefectToFaction(null!, null!));
    }

    [TestMethod]
    public void TaomDefectionModel_OverridesOnlyCanHeroDefectToFaction_OverTheDefaultModel()
    {
        var overridden = typeof(TaomDefectionModel)
            .GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
            .Select(m => m.Name)
            .ToList();

        CollectionAssert.AreEqual(new[] { "CanHeroDefectToFaction" }, overridden);
        Assert.AreEqual(typeof(DefaultDefectionModel), typeof(TaomDefectionModel).BaseType);
    }

    [TestMethod]
    public void TaomDefectionModel_BodyIsOneBoundaryConversionAndOneServiceCall()
    {
        // gamemodels.md rule 4: no if, loop or switch in an override body.
        var src = RepoPaths.ReadSource("Main/Features/SwornAnew/Models/TaomDefectionModel.cs", stripComments: true);

        Assert.IsFalse(Regex.IsMatch(src, @"\b(if|foreach|for|while|switch)\b\s*\("), "the model carries no decision");
        StringAssert.Contains(src, "_service.CanPrisonerDefect(_adapter.Read(hero, kingdom))");
        StringAssert.Contains(src, "base.CanHeroDefectToFaction(hero, kingdom)");
    }
}
