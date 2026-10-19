
namespace TAOM.Features.BondsOfBattle;

/// <summary>The Bonds of Battle MCM group, clamped. Read live at each battle, hand-over and barter.</summary>
public interface IBondsOfBattleSettingsProvider
{
    BondsOfBattleSettings Get();
}
