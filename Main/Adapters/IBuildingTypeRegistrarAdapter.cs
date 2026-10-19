using System.Collections.Generic;
using TAOM.Features.TaomBuildings;

namespace TAOM.Adapters;

/// <summary>
/// The only place a TAOM building type is created. Registering is idempotent across a save load: the engine hands back
/// the type a saved town already references (a presumed object) and this makes it ready.
/// </summary>
public interface IBuildingTypeRegistrarAdapter
{
    /// <summary>Registers every variant of every definition with the campaign's object manager and initialises it.</summary>
    void RegisterAll(IEnumerable<TaomBuildingDefinition> definitions);
}
