namespace TAOM.Features.ShadowfaxScreens.Party;

/// <summary>The service the Party Fast Open patches reach, handed over by the module's static initialisation.</summary>
internal static class PartyFastOpenCalls
{
    /// <summary>The Harmony category of the Party hooks; the number is assigned when the batch is rebased.</summary>
    internal const string PatchCategory = "PatchXXX_ShadowfaxParty";

    internal static PartyScreenFastPathService? Service { get; private set; }

    internal static void Initialize(PartyScreenFastPathService? service) => Service = service;
}
