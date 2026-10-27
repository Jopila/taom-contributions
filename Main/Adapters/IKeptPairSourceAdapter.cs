using System.Collections.Generic;

namespace TAOM.Adapters;

/// <summary>Reads the language rows the kept-pair render compares: vanilla's and TAOM's, for one language folder (BR, IT, ...).</summary>
public interface IKeptPairSourceAdapter
{
    /// <summary>The engine's language id for a folder (<c>Italiano</c> for IT), or null when the Native module has no such folder.</summary>
    string? LanguageId(string languageDir);

    /// <summary>Every row of the language's vanilla files (Native, SandBoxCore, SandBox, StoryMode), the last row for an id winning as in game.</summary>
    IReadOnlyDictionary<string, string> VanillaRows(string languageDir);

    /// <summary>Every row of the language's TAOM files (<c>std_taom_*.xml</c>).</summary>
    IReadOnlyDictionary<string, string> TaomRows(string languageDir);
}
