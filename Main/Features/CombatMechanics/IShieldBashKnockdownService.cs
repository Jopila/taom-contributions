using TAOM.Features.CombatMechanics.Domain;

namespace TAOM.Features.CombatMechanics;

public interface IShieldBashKnockdownService
{
    // Hammerhand: a shield bash or kick can floor a man. true = knock down; null = no opinion, fall through to base.
    // (Never returns false: TAOM only ADDS knockdowns, it never vetoes a vanilla one.)
    bool? Decide(in ShieldBashContext context);
}
