using System;
using TaleWorlds.Core;
using TaleWorlds.Localization;

namespace TAOM.Features.RedArrow;

/// <summary>
/// The refusal texts of the Red Arrow. No text here is TAOM's own: each is the string vanilla shows for the same branch,
/// under vanilla's own key (v1.5.5 <c>DefaultArmyManagementCalculationModel.CheckPartyEligibility</c> :405-479 and
/// <c>CampaignUIHelper.GetMapScreenActionIsEnabledWithReason</c> :2440-2497), so the player sees the same words in every
/// language the base game has, and no <c>taom_*</c> row is needed. The default after the key is the English the engine
/// itself carries. The map texts that vanilla takes from <c>GameTexts</c> are looked up the same way.
/// </summary>
internal static class RedArrowTexts
{
    /// <summary>The text for a refusal, or null for <see cref="ArmyCallReason.None"/> (vanilla leaves the text null when the party is eligible).</summary>
    public static TextObject? For(ArmyCallReason reason)
    {
        switch (reason)
        {
            case ArmyCallReason.None: return null;
            case ArmyCallReason.NoParty: return new TextObject("{=f6vTzVar}Does not have a mobile party.");
            case ArmyCallReason.RulersParty: return new TextObject("{=ipLqVv1f}You cannot invite the ruler's party to your army.");
            case ArmyCallReason.InAnotherArmy: return new TextObject("{=aROohsat}Already in another army.");
            case ArmyCallReason.InThisArmy: return new TextObject("{=Vq8yavES}Already in army.");
            case ArmyCallReason.Fighting: return new TextObject("{=pkbUiKFJ}Currently fighting an enemy.");
            case ArmyCallReason.TooSmall: return new TextObject("{=SVJlOYCB}Party has less men than 40% of it's party size limit.");
            case ArmyCallReason.Disbanding: return new TextObject("{=tFGM0yav}This party is disbanding.");
            case ArmyCallReason.NoShips: return new TextObject("{=nqq84Dzq}Party cannot reach your army since it has no ships.");
            case ArmyCallReason.AtSea: return new TextObject("{=gFixGQsr}You cannot call a party to your army while your party is at sea.");
            case ArmyCallReason.LostAtSea: return new TextObject("{=TbXDmh3t}This party is lost at sea.");
            case ArmyCallReason.TooFar: return new TextObject("{=UINgZDN5}You can not call a party that is far away.");
            default: throw new ArgumentOutOfRangeException(nameof(reason), reason, "no vanilla text mapped");
        }
    }

    /// <summary>The text for a map-action block, or the empty text for <see cref="MapActionReason.None"/> (vanilla's own value for an allowed action).</summary>
    public static TextObject For(MapActionReason reason)
    {
        switch (reason)
        {
            case MapActionReason.None: return TextObject.GetEmpty();
            case MapActionReason.Prisoner: return GameTexts.FindText("str_action_disabled_reason_prisoner");
            case MapActionReason.RaftState: return GameTexts.FindText("str_action_disabled_reason_raft_state");
            case MapActionReason.FerryState: return GameTexts.FindText("str_action_disabled_reason_ferry_state");
            case MapActionReason.InMission: return new TextObject("{=FdzsOvDq}This action is disabled while in a mission");
            case MapActionReason.Encounter: return GameTexts.FindText("str_action_disabled_reason_encounter");
            case MapActionReason.Raid: return GameTexts.FindText("str_action_disabled_reason_raid");
            case MapActionReason.Siege: return GameTexts.FindText("str_action_disabled_reason_siege");
            case MapActionReason.MapEvent: return new TextObject("{=MIylzRc5}You can't perform this action while you are in a map event.");
            default: throw new ArgumentOutOfRangeException(nameof(reason), reason, "no vanilla text mapped");
        }
    }
}
