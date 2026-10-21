using System.Collections.Generic;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.CampaignSystem.ViewModelCollection;
using TaleWorlds.Localization;
using TAOM.Features.Namegiving;

namespace TAOM.Adapters;

public sealed class FiefNamingAdapter : IFiefNamingAdapter
{
    public FiefNameSnapshot? Snapshot(string settlementId)
    {
        var settlement = Settlement.Find(settlementId);
        if (settlement == null)
            return null;
        FiefKind kind;
        if (settlement.IsTown)
            kind = FiefKind.Town;
        else if (settlement.IsCastle)
            kind = FiefKind.Castle;
        else if (settlement.IsVillage)
            kind = FiefKind.Village;
        else
            return null;

        var faction = settlement.MapFaction;
        var player = Hero.MainHero.MapFaction;
        // A village's MapFaction is its bound town's, so one comparison covers all three kinds. Only a kingdom counts as a realm.
        var inRealm = faction != null && player != null && player.IsKingdomFaction && faction == player;
        // The raw text, "{=key}English" or "{=!}Name": the lore pass reads the key prefix off it, the display name would hide it.
        var custom = settlement.Party.CustomName?.Value ?? "";
        return new FiefNameSnapshot(settlement.StringId, kind, settlement.Name.ToString(), custom, inRealm,
            faction?.StringId ?? "", faction?.Culture?.StringId ?? "");
    }

    public IReadOnlyList<string> VillageIds(string fiefId)
    {
        var settlement = Settlement.Find(fiefId);
        var ids = new List<string>();
        if (settlement == null || settlement.IsVillage)
            return ids;
        foreach (var village in settlement.BoundVillages)
            ids.Add(village.Settlement.StringId);
        return ids;
    }

    public IReadOnlyList<NamedSettlement> AllNames()
    {
        var all = Settlement.All;
        var names = new List<NamedSettlement>(all.Count);
        foreach (var settlement in all)
            names.Add(new NamedSettlement(settlement.StringId, settlement.Name.ToString()));
        return names;
    }

    public float Influence => Clan.PlayerClan.Influence;

    public bool IsRuler => Hero.MainHero.IsKingdomLeader;

    public string? MapActionBlockedReason() =>
        CampaignUIHelper.GetMapScreenActionIsEnabledWithReason(out var reason) ? null : reason.ToString();

    public void ApplyPlayerName(string settlementId, string name, int cost)
    {
        var settlement = Settlement.Find(settlementId);
        // The engine's "{=!}" prefix marks the text as not localizable. A fresh TextObject: SetCustomName stamps settlement
        // variables onto the one it is given. The name goes first: a throw in the charge then leaves a named fief, never a charge for nothing.
        settlement.Party.SetCustomName(new TextObject("{=!}" + name));
        ChangeClanInfluenceAction.Apply(Clan.PlayerClan, -cost);
    }

    public void ApplyLore(string settlementId, string text) =>
        Settlement.Find(settlementId).Party.SetCustomName(new TextObject(text));

    public void Restore(string settlementId) =>
        Settlement.Find(settlementId).Party.SetCustomName(null);
}
