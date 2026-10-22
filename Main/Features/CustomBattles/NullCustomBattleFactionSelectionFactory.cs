using System;
using TaleWorlds.Core;
using TaleWorlds.MountAndBlade.CustomBattle.CustomBattle.SelectionItem;

namespace TAOM.Features.CustomBattles;

/// <summary>No client selector (a dedicated server, or a client where TAOM.Client did not load): the vanilla faction selector stays.</summary>
public sealed class NullCustomBattleFactionSelectionFactory : ICustomBattleFactionSelectionFactory
{
    public CustomBattleFactionSelectionVM? Create(Action<BasicCultureObject> onSelectionChanged) => null;
}
