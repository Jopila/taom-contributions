using System.Collections.Generic;

namespace TAOM.Features.WarChronicle.Events;

public interface IChronicleConfigProvider
{
    /// <summary>The validated events of <c>war_chronicle/war_chronicle.json</c>, in file order; empty when none load.</summary>
    IReadOnlyList<ChronicleEventDefinition> GetEvents();
}
