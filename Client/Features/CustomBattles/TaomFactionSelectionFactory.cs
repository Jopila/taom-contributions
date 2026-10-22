using System;
using TaleWorlds.Core;
using TaleWorlds.MountAndBlade.CustomBattle.CustomBattle.SelectionItem;
using TAOM.Features.CustomBattles;

namespace TAOM.Client.Features.CustomBattles;

/// <summary>Puts TAOM's faction selector on a custom-battle side (ADR-013: the selector is client-only).</summary>
internal sealed class TaomFactionSelectionFactory : ICustomBattleFactionSelectionFactory
{
    public CustomBattleFactionSelectionVM? Create(Action<BasicCultureObject> onSelectionChanged) =>
        new TaomFactionSelectionVM(onSelectionChanged);
}
