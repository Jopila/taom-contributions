namespace TAOM.Features.ShadowfaxScreens.Clan;

/// <summary>The service the Clan fast open hooks reach, handed over by the module's static initialisation.</summary>
internal static class ClanFastOpenCalls
{
    /// <summary>The Harmony category of the three hooks; the number is assigned when the batch is rebased.</summary>
    internal const string PatchCategory = "PatchXXX_ShadowfaxClan";

    internal static ClanScreenFastPathService? Service { get; private set; }

    internal static void Initialize(ClanScreenFastPathService? service) => Service = service;
}
