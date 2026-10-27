using System;

namespace TAOM.Features.NameGrammarTags;

/// <summary>A one-line probe of a language's text processor: <c>Text</c> with <c>Variable</c> bound to <c>TaggedValue</c> must print <c>Expected</c>.</summary>
public sealed record CanarySpec(string Text, string Variable, string TaggedValue, string Expected);

/// <summary>The outcome of a canary. A language with no canary is not Applicable.</summary>
public sealed record CanaryResult(bool Applicable, bool Passed, string Description);

/// <summary>Proves the text processor reads the grammar tags at all: if it does not, every other line of the report is meaningless (#1026).</summary>
public static class ProcessorCanary
{
    public static CanarySpec? For(string languageDir)
    {
        switch (languageDir.ToUpperInvariant())
        {
            case "IT":
                return new CanarySpec("{.di}{CANARY}", "CANARY", "{.FN}Roma", "di Roma");
            case "BR":
                return new CanarySpec("{#GENDER(CANARY)}MS{#}FS{#}MP{#}FP{#}NS{#}NP{\\#}", "CANARY", "{.FS}Roma", "FS");
            default:
                return null;
        }
    }

    /// <summary>Runs the language's canary through <paramref name="render"/>(text, variable, taggedValue).</summary>
    public static CanaryResult Evaluate(string languageDir, Func<string, string, string, string> render)
    {
        var spec = For(languageDir);
        if (spec == null)
            return new CanaryResult(false, true, $"no canary for {languageDir}");
        try
        {
            var printed = render(spec.Text, spec.Variable, spec.TaggedValue);
            var passed = string.Equals(printed, spec.Expected, StringComparison.Ordinal);
            return new CanaryResult(true, passed,
                $"{spec.Text} with {spec.Variable} = {spec.TaggedValue} printed '{printed}', expected '{spec.Expected}'");
        }
        catch (Exception ex)
        {
            return new CanaryResult(true, false, $"{spec.Text} could not be rendered: {ex.Message}");
        }
    }
}
