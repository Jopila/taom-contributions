using TAOM.Core.Domain;

namespace TAOM.Tests.Features.HiddenHosts;

/// <summary>
/// The scout seam with no scout: nobody is away and nobody is named. The default collaborator of every Hidden Hosts test that
/// is not about Far Watch, so those tests build the service the way they always did. A test double only: never shipped in Main.
/// </summary>
internal sealed class NoScoutIntel : IScoutIntelQuery
{
    public bool IsScoutAway => false;

    public bool IsNamedByScout(string partyId, int nowHour) => false;
}
