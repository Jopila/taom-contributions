using TaleWorlds.MountAndBlade;

namespace TAOM.Features.CompanionTactics.BattleActionBar;

/// <summary>Core's default (ADR-013): no action bar view. TAOM.Client replaces it on a rendering client.</summary>
public sealed class NullBattleActionBarViewFactory : IBattleActionBarViewFactory
{
    public MissionBehavior? Create() => null;
}
