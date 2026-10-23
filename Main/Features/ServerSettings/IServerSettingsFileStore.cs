namespace TAOM.Features.ServerSettings;

/// <summary>The result of reading the settings file: Missing, Loaded with its text, or Unreadable with the error.</summary>
internal sealed class ServerFileRead
{
    public ServerFileRead(ServerFileState state, string? text, string? error)
    {
        State = state;
        Text = text;
        Error = error;
    }

    public ServerFileState State { get; }

    public string? Text { get; }

    public string? Error { get; }
}

/// <summary>The feature's only file IO, so the source and the module test against a fake.</summary>
internal interface IServerSettingsFileStore
{
    ServerSettingsLocation Locate();

    /// <summary>Never throws: an IO failure comes back as <see cref="ServerFileState.Unreadable"/>.</summary>
    ServerFileRead TryRead(string path);

    /// <summary>Writes only when no file exists, so an operator's file is never rewritten. Returns the error, or null.</summary>
    string? WriteIfMissing(string path, string content);

    /// <summary>Writes only when the content differs from what is there. Returns the error, or null.</summary>
    string? WriteIfChanged(string path, string content);
}
