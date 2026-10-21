using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using TAOM.Core.Infrastructure;
using TAOM.Core.Logging;
using TAOM.Features.NamedCompanions.Domain;
using TAOM.Features.NamedCompanions.Oaths;

namespace TAOM.Features.NamedCompanions;

public class NamedCompanionConfigProvider : INamedCompanionConfigProvider
{
    private readonly IPathService _pathService;
    private readonly IModLogger _logger;
    private static readonly Regex EnabledPath = new(@"^\[(\d+)\]\.enabled$");

    private IReadOnlyList<NamedCompanionDefinition> _cache;

    public NamedCompanionConfigProvider(IPathService pathService, IModLogger logger)
    {
        _pathService = pathService;
        _logger = logger;
    }

    public IReadOnlyList<NamedCompanionDefinition> GetCompanions()
    {
        if (_cache != null)
            return _cache;

        var path = Path.Combine(_pathService.ModuleDataPath, "named_companions", "named_companion_config.json");

        if (!File.Exists(path))
        {
            _logger.LogWarning($"NamedCompanionConfigProvider: Config not found at {path}");
            _cache = Array.Empty<NamedCompanionDefinition>();
            return _cache;
        }

        try
        {
            var json = File.ReadAllText(path);
            // A syntax error cannot be skipped past, and the typed read below reports a wrong-typed member and a
            // broken token the same way (both are reader exceptions), so check the syntax on its own first: a failure
            // here reaches the catch.
            var root = string.IsNullOrWhiteSpace(json) ? null : JToken.Parse(json);

            // One wrong-typed member must not empty the whole list: skip the member, keep the rest.
            var skipped = 0;
            var unreadableEnabled = new List<int>();
            var settings = new JsonSerializerSettings
            {
                Error = (sender, args) =>
                {
                    skipped++;
                    var memberPath = args.ErrorContext.Path ?? string.Empty;
                    _logger.LogWarning($"NamedCompanionConfigProvider: ignored {memberPath}: {args.ErrorContext.Error.Message}");
                    var match = EnabledPath.Match(memberPath);
                    if (match.Success) unreadableEnabled.Add(int.Parse(match.Groups[1].Value));
                    args.ErrorContext.Handled = true;
                },
            };
            var list = JsonConvert.DeserializeObject<List<NamedCompanionDefinition>>(json, settings);
            if (list == null)
            {
                // An empty file is an empty list; any other text that deserializes to null (a literal null) is not a list.
                if (!string.IsNullOrWhiteSpace(json))
                    _logger.LogError("NamedCompanionConfigProvider: Failed to parse named_companion_config.json: the file does not hold a list");
                _cache = Array.Empty<NamedCompanionDefinition>();
                return _cache;
            }

            // A companion whose enabled flag cannot be read fails closed: it stays off, never silently on.
            // The index is a position in the JSON array, and the list drops an element it could not convert, so match by
            // the entry's character_id read from the parsed tokens, never by list position.
            foreach (var index in unreadableEnabled)
            {
                var idToken = ((root as JArray)?.ElementAtOrDefault(index) as JObject)?["character_id"];
                var id = idToken?.Type == JTokenType.String ? (string)idToken : null;
                if (string.IsNullOrEmpty(id)) continue;
                foreach (var companion in list)
                    if (companion != null && companion.CharacterId == id)
                        companion.Enabled = false;
                _logger.LogWarning($"NamedCompanionConfigProvider: enabled of '{id}' is unreadable; the companion is disabled");
            }
            if (skipped > 0)
                _logger.LogWarning($"NamedCompanionConfigProvider: skipped {skipped} member(s) that could not be read; see the warnings above");

            list.RemoveAll(c => c == null);
            OathConfigValidator.Validate(list, _logger);
            _cache = list.AsReadOnly();
            _logger.LogInfo($"NamedCompanionConfigProvider: Loaded {_cache.Count} named companions");
            return _cache;
        }
        catch (Exception ex)
        {
            _logger.LogError($"NamedCompanionConfigProvider: Failed to parse named_companion_config.json: {ex.Message}");
            _cache = Array.Empty<NamedCompanionDefinition>();
            return _cache;
        }
    }
}
