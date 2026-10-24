using NSubstitute;
using TAOM.Features.Enlistment;

namespace TAOM.Tests.Features.Enlistment;

/// <summary>
/// Shared test doubles for the enlistment services.
///
/// EXISTS BECAUSE OF A REAL TRAP: <c>Substitute.For&lt;IEnlistmentFeatureSettingsProvider&gt;()</c>
/// returns <c>false</c> for <c>IsEnabled</c> by default, and the reconciler reads a false there as
/// "the player switched the feature off in MCM" and discharges them. A bare substitute therefore
/// silently turns every reconciler test into a discharge test — it took out 36 of them at once.
/// Use <see cref="FeatureOn"/> in any test that is not specifically about the master switch.
/// </summary>
internal static class EnlistmentTestDoubles
{
    public static IEnlistmentFeatureSettingsProvider FeatureOn()
    {
        var provider = Substitute.For<IEnlistmentFeatureSettingsProvider>();
        provider.IsEnabled.Returns(true);
        return provider;
    }

    /// <summary>A real renewal service over substitutes, for tests that construct the maintenance pump (#836).</summary>
    public static TAOM.Features.Enlistment.Content.ContractRenewalService ContractRenewal() =>
        new TAOM.Features.Enlistment.Content.ContractRenewalService(
            NSubstitute.Substitute.For<TAOM.Features.Enlistment.IEnlistmentStore>(),
            NSubstitute.Substitute.For<TAOM.Features.Enlistment.Content.IEnlistmentContentStore>(),
            NSubstitute.Substitute.For<TAOM.Features.Enlistment.Content.IEnlistmentContentConfigProvider>(),
            NSubstitute.Substitute.For<TAOM.Features.Enlistment.IEnlistmentConfigProvider>(),
            NSubstitute.Substitute.For<TAOM.Features.Enlistment.Content.IServiceRewardService>(),
            NSubstitute.Substitute.For<TAOM.Features.Enlistment.IDischargeService>(),
            NSubstitute.Substitute.For<TAOM.Features.CoopInterop.ICoopSessionProvider>(),
            NSubstitute.Substitute.For<TAOM.Adapters.ICommanderLordAdapter>(),
            NSubstitute.Substitute.For<TAOM.Adapters.IEncounterAdapter>(),
            NSubstitute.Substitute.For<TAOM.Adapters.IInquiryAdapter>(),
            NSubstitute.Substitute.For<TAOM.Core.Logging.IModLogger>());
}
