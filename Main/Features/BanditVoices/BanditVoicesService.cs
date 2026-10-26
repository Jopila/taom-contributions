using TAOM.Adapters;
using TAOM.Features.CoopInterop;
using TAOM.Features.InitialChildGeneration;
using TAOM.Features.Messengers;

namespace TAOM.Features.BanditVoices;

/// <summary>The engine-free choice behind Bandit Voices (#900): one roll of <see cref="Chance"/> on a first meeting gives a faction its own greeting and the vanilla ultimatum that answers it.</summary>
internal sealed class BanditVoicesService
{
    /// <summary>The chance of a clan greeting on a first meeting. Compiled, not an MCM setting.</summary>
    internal const double Chance = 0.25;

    /// <summary>The game text the greeting rows live under; the variation is <c>&lt;clanId&gt;_harmless</c> or <c>_brothers</c>.</summary>
    internal const string GreetingTextId = "str_taom_bv_greeting";

    /// <summary>Vanilla's reply to its own "harmless travellers" greeting, key and default verbatim.</summary>
    internal const string HarmlessUltimatum = "{=A5IJmN0X}I think not, brigand. Surrender or die!";

    /// <summary>Vanilla's reply to its own "dozens more of us hiding" greeting, key and default verbatim.</summary>
    internal const string BrothersUltimatum = "{=ASRpFaGF}Nice try, brigand. Surrender or die!";

    private readonly IBanditVoicesSettingsProvider _settings;
    private readonly IRandomSource _random;
    private readonly IDedicatedServerProvider _dedicated;
    private readonly IMessengerConversationState _messenger;
    private readonly IBanditEncounterTextAdapter _text;

    public BanditVoicesService(
        IBanditVoicesSettingsProvider settings,
        IRandomSource random,
        IDedicatedServerProvider dedicated,
        IMessengerConversationState messenger,
        IBanditEncounterTextAdapter text)
    {
        _settings = settings;
        _random = random;
        _dedicated = dedicated;
        _messenger = messenger;
        _text = text;
    }

    /// <summary>
    /// On a first meeting on land, rolls once and sets the faction's greeting and the matching vanilla ultimatum together; the
    /// greeting row decides which factions speak, so a faction without one, or a roll at or above the chance, leaves vanilla's text.
    /// </summary>
    internal void Apply()
    {
        if (!_settings.Enabled || _dedicated.IsDedicatedServer || _messenger.IsOpen)
            return;
        if (_text.EncounteredAtSea || !_text.IsFirstMeeting())
            return;

        var factionId = _text.EncounteredFactionId;
        if (factionId == null)
            return;

        var roll = _random.NextDouble();
        if (roll >= Chance)
            return;

        var harmless = roll < Chance / 2;
        if (_text.TrySetGreeting(GreetingTextId, factionId + (harmless ? "_harmless" : "_brothers")))
            _text.SetUltimatum(harmless ? HarmlessUltimatum : BrothersUltimatum);
    }
}
