namespace TAOM.Features.FieldCamp;

/// <summary>MCM boundary; values validated here (Config Providers MUST Validate).</summary>
public interface ICampSettingsProvider
{
    /// <summary>Master toggle. Off = no overlay button, no menus, no ticks; an existing camp
    /// stays until broken so state is never silently dropped by a toggle.</summary>
    bool Enabled { get; }

    /// <summary>Hours to raise a field camp (default 4). Ambush/lookout take a quarter of this,
    /// a fortified camp double.</summary>
    float CampSetupHours { get; }

    /// <summary>Party morale gained per camped hour (default 1); fortified camps double it.</summary>
    float CampMoralePerHour { get; }

    /// <summary>Forage throughput factor per troop (default 0.1).</summary>
    float ForagePerTroopFactor { get; }

    /// <summary>Spotting distance inside which an armed ambush can trigger (default 10).</summary>
    float MaxAmbushRange { get; }

    /// <summary>Base chance an eligible passer-by springs the ambush (default 0.5).</summary>
    float BaseAmbushChance { get; }

    /// <summary>Minimum map distance from towns/castles to pitch a field/fortified camp
    /// (default 10; 0 disables; ambush/lookout exempt).</summary>
    float MinTownDistance { get; }

    /// <summary>Gold cost to fortify a field camp (default 500).</summary>
    int FortifiedUpgradeCost { get; }

    /// <summary>Hours a camp work (hunt, timber, fish) takes once started (default 10).</summary>
    float CampActivityHours { get; }

    /// <summary>Goods a work brings in per ten healthy men on good ground (default 10).</summary>
    float CampActivityYieldPerTenMen { get; }

    /// <summary>Most goods one finished work can bring in (default 30).</summary>
    int CampActivityYieldCap { get; }

    /// <summary>Extra healing per day for the troops while the camp panel is open (default 15).</summary>
    float CampRestHealRegularsPerDay { get; }

    /// <summary>Extra healing points per day for heroes while the camp panel is open (default 19).</summary>
    float CampRestHealHeroesPerDay { get; }

    /// <summary>A new campaign starts with a field camp pitched, when the ground allows it (default off).</summary>
    bool CampStartInCamp { get; }
}
