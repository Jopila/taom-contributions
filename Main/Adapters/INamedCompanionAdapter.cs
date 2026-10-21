namespace TAOM.Adapters;

public interface INamedCompanionAdapter
{
    bool HeroExists(string characterId);
    bool IsHeroAlive(string characterId);
    bool IsPlacedInSettlement(string characterId);
    bool IsRecruitedOrInParty(string characterId);

    // #127 — Entity State Matrix completion. Prisoner / Fugitive bypass the existing guards
    // (PartyBelongedTo=null, CurrentSettlement=null, StayingInSettlement=null) and were being
    // force-placed on every load — corrupting captor prison rosters and resetting fugitive state.
    bool IsHeroPrisoner(string characterId);
    bool IsHeroFugitive(string characterId);

    // Named companions are XML heroes carrying faction="Faction.neutral", and Hero.Deserialize
    // skips the Clan assignment for that literal id — so Clan stays null. XML deserialization never
    // assigns BornSettlement either (only HeroCreator does), so HomeSettlement resolves to null too,
    // and a companion sitting in a tavern has no PartyBelongedTo. All three null makes
    // Hero.MapFaction return null, which the engine dereferences unguarded in several places.
    // Returns true when a repair was applied.
    bool EnsureHomeSettlement(string characterId, string settlementId);

    void PlaceInSettlement(string characterId, string settlementId);
    void MarkAsMet(string characterId);

    // Oaths of Service (#904). All string-id based; every method is safe to call with an unknown id.

    /// <summary>The companion's relation with the player hero; 0 when either hero is missing.</summary>
    int GetRelationWithPlayer(string characterId);

    /// <summary>Raises (or lowers) the player's relation with the companion. False when it could not be applied.</summary>
    bool ChangeRelationWithPlayer(string characterId, int delta);

    /// <summary>Vanilla's own companion-limit test (<c>too_many_companions</c> in the hire conversation).</summary>
    bool IsCompanionLimitReached();

    /// <summary>The price the vanilla hiring model asks for this companion; 0 when it cannot be read.</summary>
    int GetVanillaHiringPrice(string characterId);

    /// <summary>
    /// Pays <paramref name="gold"/> to the companion, then adds them to the player's clan and party in
    /// vanilla's order. Returns false, with nothing moved, when the hero is missing or the purse is short.
    /// </summary>
    bool HireIntoPlayerParty(string characterId, int gold);

    bool IsHeroActive(string characterId);

    /// <summary>The StringId of the settlement the hero is in, or null.</summary>
    string GetCurrentSettlementId(string characterId);

    /// <summary>Takes the hero out of wherever they stand and places them in the settlement. No state change.</summary>
    void ReturnHome(string characterId, string settlementId);

    /// <summary>True when the player is inside the settlement.</summary>
    bool IsPlayerInSettlement(string settlementId);

    /// <summary>True when the loaded game has a settlement with this StringId; false on any failure.</summary>
    bool SettlementExists(string settlementId);
}
