using System;

namespace TAOM.Features.NameGrammarTags;

/// <summary>The text <c>TextObject.ToString()</c> returns when rendering threw: the engine swallows the exception and prints this instead.</summary>
public static class EngineText
{
    public const string ErrorPrefix = "Error at id: ";

    public static bool IsError(string? text) => text != null && text.StartsWith(ErrorPrefix, StringComparison.Ordinal);
}
