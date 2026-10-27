using System.Collections.Generic;
using TAOM.Features.NameGrammarTags;

namespace TAOM.Adapters;

/// <summary>Stateless reads of the live campaign by id, and the text render that binds the ids it is given.</summary>
public interface IGrammarRendererAdapter
{
    /// <summary>The id of the language the game is running in (<c>MBTextManager.ActiveTextLanguage</c>).</summary>
    string ActiveLanguageId { get; }

    /// <summary>The type name of the text processor the active language's data builds (<c>LocalizedTextManager.CreateTextProcessorForLanguage</c>), or empty when it cannot be built.</summary>
    string ActiveTextProcessor { get; }

    /// <summary>Ids of the kingdoms that are not eliminated; empty when there is no campaign.</summary>
    IReadOnlyList<string> KingdomIds();

    string KingdomLabel(string kingdomId);

    string CultureId(string kingdomId);

    IReadOnlyList<string> ClanIds(string kingdomId);

    IReadOnlyList<string> AllClanIds();

    /// <summary>Ids of the kingdom's towns and castles.</summary>
    IReadOnlyList<string> FiefIds(string kingdomId);

    IReadOnlyList<string> AllFiefIds();

    /// <summary>Renders each text with every variable bound to the Name, InformalName, FormalName or link TextObject of its object, built once and shared by the texts. <c>TextObject.ToString()</c> catches its own exceptions and returns an "Error at id:" line instead of throwing, so the caller checks for it (<c>EngineText.IsError</c>).</summary>
    IReadOnlyList<string> Render(IReadOnlyList<string> texts, IReadOnlyList<RenderBinding> bindings);

    /// <summary>Renders <paramref name="text"/> with <paramref name="variable"/> bound to a TextObject made from <paramref name="taggedValue"/>.</summary>
    string RenderCanary(string text, string variable, string taggedValue);
}
