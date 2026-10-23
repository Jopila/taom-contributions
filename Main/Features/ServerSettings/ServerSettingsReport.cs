using System.Collections.Generic;
using TAOM.Core.Logging;

namespace TAOM.Features.ServerSettings;

/// <summary>What the server found at the settings path, and what the reader made of it.</summary>
internal enum ServerFileState
{
    /// <summary>No file at the path: every setting keeps its server default.</summary>
    Missing,

    /// <summary>Read and parsed as one JSON object.</summary>
    Loaded,

    /// <summary>Present but empty or whitespace: zero keys, not an error.</summary>
    Empty,

    /// <summary>Not JSON, not an object, or a duplicate key: every setting keeps its server default.</summary>
    Malformed,

    /// <summary>The file could not be read (an IO error or a directory at the path): every setting keeps its server default.</summary>
    Unreadable,

    /// <summary>The loader itself threw: no settings instance is served, so each provider's own fallback applies.</summary>
    LoaderFailed,
}

internal enum ServerSettingsLevel
{
    Info,
    Warning,
    Error,
}

internal sealed class ServerSettingsLine
{
    public ServerSettingsLine(ServerSettingsLevel level, string text)
    {
        Level = level;
        Text = text;
    }

    public ServerSettingsLevel Level { get; }

    public string Text { get; }
}

/// <summary>The settings file path, the defaults reference beside it, and which rule chose the folder.</summary>
internal sealed class ServerSettingsLocation
{
    public ServerSettingsLocation(string path, string defaultsPath, string rule)
    {
        Path = path;
        DefaultsPath = defaultsPath;
        Rule = rule;
    }

    public string Path { get; }

    public string DefaultsPath { get; }

    /// <summary>"BANNERLORD_USER_DIR", "TAOM module folder" or "current directory".</summary>
    public string Rule { get; }
}

/// <summary>One load of the server settings file: its state, its counts, and every line to log.</summary>
internal sealed class ServerSettingsReport
{
    internal const string Tag = "[ServerSettings] ";

    public ServerSettingsReport(ServerFileState state) => State = state;

    public ServerFileState State { get; }

    public ServerSettingsLocation? Location { get; set; }

    public int Applied { get; set; }

    public int Reverted { get; set; }

    public int Unknown { get; set; }

    public List<ServerSettingsLine> Lines { get; } = new();

    public void Add(ServerSettingsLevel level, string text) => Lines.Add(new ServerSettingsLine(level, text));

    /// <summary>The one line an operator reads at boot: where the file is, why there, and what it changed.</summary>
    public string BootLine()
    {
        const string head = "TAOM server settings (MCM is not loaded on a dedicated server): ";
        var where = Location == null ? "an unresolved path" : Location.Path + " (" + Location.Rule + ")";
        switch (State)
        {
            case ServerFileState.Loaded:
            case ServerFileState.Empty:
                return head + "loaded " + Applied + " key(s) from " + where + ", " + Reverted + " reverted, " + Unknown + " unknown";
            case ServerFileState.Missing:
                return head + "no file at " + where + ", every setting is at its server default";
            case ServerFileState.LoaderFailed:
                return head + "could not load the server settings from " + where
                       + ", no settings instance is served, so each provider's own fallback applies";
            default:
                return head + "could not use " + where + ", every setting is at its server default";
        }
    }

    public void WriteTo(IModLogger logger)
    {
        foreach (var line in Lines)
        {
            switch (line.Level)
            {
                case ServerSettingsLevel.Error: logger.LogError(Tag + line.Text); break;
                case ServerSettingsLevel.Warning: logger.LogWarning(Tag + line.Text); break;
                default: logger.LogInfo(Tag + line.Text); break;
            }
        }
    }
}
