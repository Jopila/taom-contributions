using System;
using TaleWorlds.Core;
using TaleWorlds.MountAndBlade.CustomBattle.CustomBattle.SelectionItem;

namespace TAOM.Features.CustomBattles;

/// <summary>The faction selector TAOM puts on a custom-battle side (ADR-013: the selector type is client-only).
/// Null keeps the vanilla selector.</summary>
public interface ICustomBattleFactionSelectionFactory
{
    CustomBattleFactionSelectionVM? Create(Action<BasicCultureObject> onSelectionChanged);
}
