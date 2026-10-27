using System;
using System.Collections.Generic;
using System.Linq;

namespace TAOM.Features.NameGrammarTags;

/// <summary>A vanilla key whose language row TAOM also carries, so TAOM's row is the one the game shows.</summary>
public sealed record KeptPair(string Id, string VanillaText, string TaomText);

/// <summary>One variant of a kept pair rendered for one subject: vanilla's line and TAOM's line, or the failure that replaced them.</summary>
public sealed record RenderedLine(string Variant, string Vanilla, string Taom, bool Failed);

/// <summary>A kept pair rendered for one subject kingdom, with one line pair per variant.</summary>
public sealed record RenderedPair(KeptPair Pair, string Subject, IReadOnlyList<RenderedLine> Lines)
{
    /// <summary>FAILED when any render threw, else DIFFERENT when any variant's two lines differ, else SAME.</summary>
    public string Verdict =>
        Lines.Any(l => l.Failed) ? "FAILED"
        : Lines.Any(l => !string.Equals(l.Vanilla, l.Taom, StringComparison.Ordinal)) ? "DIFFERENT"
        : "SAME";
}
