using TAOM.Features.CompanionTactics.BattleActionBar;
using TAOM.Features.CompanionTactics.BattleActionBar.Hooks;
using TaleWorlds.MountAndBlade;

namespace TAOM.Client.Features.CompanionTactics;

/// <summary>The battle action bar's mission view (ADR-013); core's null factory adds none.</summary>
internal sealed class BattleActionBarViewFactory : IBattleActionBarViewFactory
{
    public MissionBehavior? Create() => new BattleActionBarMissionView();
}
