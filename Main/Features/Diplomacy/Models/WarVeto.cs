namespace TAOM.Features.Diplomacy.Models;

// Why a war between two kingdoms is refused; None means it may be declared. Permanent is checked
// first in every phase, so a Permanent pair always reads Permanent. SameAlignment comes next, ahead
// of the two phase vetoes, so a same-side pair reads SameAlignment before Full War as well: the
// phase only ever adds a refusal to a pair the lore rule would allow.
public enum WarVeto
{
    None,
    Permanent,
    SameAlignment,
    PhasePeace,
    PhaseNotScripted
}
