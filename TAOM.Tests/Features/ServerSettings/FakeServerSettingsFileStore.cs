using System;
using System.Collections.Generic;
using TAOM.Features.ServerSettings;

namespace TAOM.Tests.Features.ServerSettings;

/// <summary>In-memory store: scripted read result, recorded reads and writes, optional hooks for re-entrancy and throws.</summary>
internal sealed class FakeServerSettingsFileStore : IServerSettingsFileStore
{
    public ServerSettingsLocation Where { get; set; } =
        new ServerSettingsLocation(@"C:\fake\taom-server-settings.json", @"C:\fake\taom-server-settings.defaults.json", "BANNERLORD_USER_DIR");

    public ServerFileRead Read { get; set; } = new ServerFileRead(ServerFileState.Missing, null, null);

    public Action? OnTryRead { get; set; }

    public string? WriteError { get; set; }

    public int ReadCount;

    public List<(string Path, string Content)> MissingWrites { get; } = new();

    public List<(string Path, string Content)> ChangedWrites { get; } = new();

    public static FakeServerSettingsFileStore WithText(string text) =>
        new FakeServerSettingsFileStore { Read = new ServerFileRead(ServerFileState.Loaded, text, null) };

    public ServerSettingsLocation Locate() => Where;

    public ServerFileRead TryRead(string path)
    {
        System.Threading.Interlocked.Increment(ref ReadCount);
        OnTryRead?.Invoke();
        return Read;
    }

    public string? WriteIfMissing(string path, string content)
    {
        MissingWrites.Add((path, content));
        return WriteError;
    }

    public string? WriteIfChanged(string path, string content)
    {
        ChangedWrites.Add((path, content));
        return WriteError;
    }
}
