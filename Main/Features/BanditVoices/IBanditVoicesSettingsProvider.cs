namespace TAOM.Features.BanditVoices;

/// <summary>The MCM "World/Bandit Voices" switch for Bandit Voices (#900).</summary>
public interface IBanditVoicesSettingsProvider
{
    /// <summary>True when a first meeting with a talking bandit clan may open with that clan's own greeting. Read on the main thread, once per bandit conversation.</summary>
    bool Enabled { get; }
}
