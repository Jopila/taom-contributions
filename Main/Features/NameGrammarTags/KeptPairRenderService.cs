using System;
using System.Collections.Generic;
using System.Linq;
using TAOM.Adapters;

namespace TAOM.Features.NameGrammarTags;

/// <summary>Renders every kept pair of one language for a few real kingdoms, bound the way vanilla's code binds them, and writes the report that <c>taom.print_kept_pairs</c> names (#1023).</summary>
public sealed class KeptPairRenderService
{
    private const string AsBound = "as bound";

    private readonly IKeptPairSourceAdapter _source;
    private readonly IGrammarRendererAdapter _renderer;
    private readonly IReportFileAdapter _files;

    public KeptPairRenderService(IKeptPairSourceAdapter source, IGrammarRendererAdapter renderer, IReportFileAdapter files)
    {
        _source = source;
        _renderer = renderer;
        _files = files;
    }

    /// <summary>One variant of a row: which text plain kingdom and clan variables carry, and whether FACTION variables hold a clan.</summary>
    private readonly record struct Variant(bool? Informal, bool? FactionAsClan)
    {
        public string Label
        {
            get
            {
                var parts = new List<string>();
                if (Informal.HasValue) parts.Add(Informal.Value ? "InformalName" : "Name");
                if (FactionAsClan.HasValue) parts.Add(FactionAsClan.Value ? "faction as clan" : "faction as kingdom");
                return parts.Count == 0 ? AsBound : string.Join(", ", parts);
            }
        }
    }

    /// <summary>The campaign objects one subject kingdom brings: its clans and fiefs, falling back to the whole campaign's.</summary>
    private sealed class Subject
    {
        public int KingdomIndex;
        public IReadOnlyList<string> Clans = Array.Empty<string>();
        public IReadOnlyList<string> Fiefs = Array.Empty<string>();
    }

    public string Run(string languageDir, int maxSubjects)
    {
        var languageId = _source.LanguageId(languageDir);
        if (languageId == null)
            return $"No language folder '{languageDir}' in the game's Native module.";

        var active = _renderer.ActiveLanguageId;
        if (!string.Equals(active, languageId, StringComparison.Ordinal))
            return $"The game is running in {active}, but {languageDir} is {languageId}. The language's grammar code reads "
                 + $"the tags, so switch the game to {languageId} (Options, Language), then run this again.";

        var pairs = KeptPairFinder.Find(_source.TaomRows(languageDir), _source.VanillaRows(languageDir));
        if (pairs.Count == 0)
            return $"No kept pair in {languageDir}: TAOM carries no language row of a vanilla key.";

        var kingdoms = _renderer.KingdomIds();
        if (kingdoms.Count == 0)
            return "The campaign has no kingdom to bind the names to.";

        var subjectIndexes = Spread(kingdoms.Count, maxSubjects);
        var labels = subjectIndexes.Select(i => _renderer.KingdomLabel(kingdoms[i])).ToList();
        var canary = ProcessorCanary.Evaluate(languageDir, _renderer.RenderCanary);

        var subjects = subjectIndexes.Select(i => LoadSubject(kingdoms, i)).ToList();
        var rendered = new List<RenderedPair>();
        var failed = 0;
        foreach (var pair in pairs)
        {
            for (var s = 0; s < subjects.Count; s++)
            {
                var lines = RenderPair(pair, kingdoms, subjects[s]);
                failed += lines.Count(l => l.Failed);
                rendered.Add(new RenderedPair(pair, labels[s], lines));
            }
        }

        var report = KeptPairReportFormatter.Format(languageDir, languageId, _renderer.ActiveTextProcessor, canary,
            pairs.Count, labels, rendered);
        string path;
        try
        {
            path = _files.Write($"taom_kept_pairs_{languageDir}", report);
        }
        catch (Exception ex)
        {
            return $"Rendered {pairs.Count} kept pair(s) but the report could not be written: {ex.Message}";
        }

        var summary = $"Rendered {pairs.Count} kept pair(s) for {labels.Count} kingdom(s): {string.Join(", ", labels)}.";
        if (failed > 0)
            summary += $" {failed} render(s) failed; they are marked in the report.";
        if (canary.Applicable && !canary.Passed)
            summary += " CANARY FAILED: the text processor does not read the grammar tags, so the report proves nothing.";
        return $"{summary}\nReport: {path}";
    }

    /// <summary>Evenly spread kingdom positions: up to <paramref name="max"/> of <paramref name="count"/>, never twice the same.</summary>
    private static IReadOnlyList<int> Spread(int count, int max)
    {
        var take = Math.Min(Math.Max(max, 0), count);
        return Enumerable.Range(0, take).Select(i => i * count / take).ToList();
    }

    private Subject LoadSubject(IReadOnlyList<string> kingdoms, int kingdomIndex)
    {
        var id = kingdoms[kingdomIndex];
        var clans = _renderer.ClanIds(id);
        var fiefs = _renderer.FiefIds(id);
        return new Subject
        {
            KingdomIndex = kingdomIndex,
            Clans = clans.Count > 0 ? clans : _renderer.AllClanIds(),
            Fiefs = fiefs.Count > 0 ? fiefs : _renderer.AllFiefIds(),
        };
    }

    private IReadOnlyList<RenderedLine> RenderPair(KeptPair pair, IReadOnlyList<string> kingdoms, Subject subject)
    {
        var plan = BindingPlanner.Plan(RenderVariableClassifier.VariablesIn(pair.VanillaText)
            .Concat(RenderVariableClassifier.VariablesIn(pair.TaomText)).Distinct());
        var lines = new List<RenderedLine>();
        foreach (var variant in VariantsOf(plan))
        {
            try
            {
                var bindings = plan.Select(b => Bind(b, variant, plan, kingdoms, subject)).ToList();
                var both = _renderer.Render(new[] { pair.VanillaText, "{=" + pair.Id + "}" + pair.VanillaText }, bindings);
                var engineError = both.FirstOrDefault(EngineText.IsError);
                if (engineError != null)
                    throw new InvalidOperationException(engineError);
                lines.Add(new RenderedLine(variant.Label, both[0], both[1], Failed: false));
            }
            catch (Exception ex)
            {
                var why = $"render failed: {ex.Message}";
                lines.Add(new RenderedLine(variant.Label, why, why, Failed: true));
            }
        }
        return lines;
    }

    private static IEnumerable<Variant> VariantsOf(IReadOnlyList<VariableBinding> plan)
    {
        var plain = plan.Any(b => (b.Kind == NameKind.Kingdom || b.Kind == NameKind.Clan) && b.Form == NameForm.Name);
        var faction = plan.Any(b => b.IsFaction);
        foreach (bool? informal in plain ? new bool?[] { false, true } : new bool?[] { null })
            foreach (bool? asClan in faction ? new bool?[] { false, true } : new bool?[] { null })
                yield return new Variant(informal, asClan);
    }

    private RenderBinding Bind(VariableBinding b, Variant variant, IReadOnlyList<VariableBinding> plan,
        IReadOnlyList<string> kingdoms, Subject subject)
    {
        var kind = variant.FactionAsClan == true && b.IsFaction ? NameKind.Clan : b.Kind;
        var form = variant.Informal == true && b.Form == NameForm.Name && (kind == NameKind.Kingdom || kind == NameKind.Clan)
            ? NameForm.Informal : b.Form;
        // A FACTION variable rendered as a clan takes a clan after the row's own clan variables.
        var index = kind == NameKind.Clan && b.Kind != NameKind.Clan
            ? b.Index + plan.Count(p => p.Kind == NameKind.Clan)
            : b.Index;
        return new RenderBinding(b.Variable, kind, form, ObjectFor(kind, index, kingdoms, subject));
    }

    private string ObjectFor(NameKind kind, int index, IReadOnlyList<string> kingdoms, Subject subject)
    {
        switch (kind)
        {
            case NameKind.Kingdom:
                return kingdoms[(subject.KingdomIndex + index) % kingdoms.Count];
            case NameKind.Clan:
                return Pick(subject.Clans, index, "clan");
            case NameKind.Settlement:
                return Pick(subject.Fiefs, index, "fief");
            case NameKind.Culture:
                return _renderer.CultureId(kingdoms[(subject.KingdomIndex + index) % kingdoms.Count]);
            default:
                throw new ArgumentOutOfRangeException(nameof(kind), kind, "Only name variables are bound.");
        }
    }

    private static string Pick(IReadOnlyList<string> ids, int index, string what)
    {
        if (ids.Count == 0)
            throw new InvalidOperationException($"the campaign has no {what} to bind");
        return ids[index % ids.Count];
    }
}
