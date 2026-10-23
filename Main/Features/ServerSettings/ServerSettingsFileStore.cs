using System;
using System.IO;
using System.Text;

namespace TAOM.Features.ServerSettings;

/// <summary>
/// Finds and touches <c>taom-server-settings.json</c>. The folder is <c>BANNERLORD_USER_DIR</c> when set and not
/// blank (where the launcher puts <c>server-config.json</c>), else the TAOM module folder, else the current
/// directory. Every method returns failures as values; none throws.
/// </summary>
internal sealed class ServerSettingsFileStore : IServerSettingsFileStore
{
    internal const string FileName = "taom-server-settings.json";
    internal const string DefaultsFileName = "taom-server-settings.defaults.json";
    internal const string UserDirVariable = "BANNERLORD_USER_DIR";

    private static readonly Encoding Utf8NoBom = new UTF8Encoding(false);

    private readonly Func<string, string?> _environment;
    private readonly string? _moduleRoot;
    private readonly string _currentDirectory;
    private readonly Func<string, Stream> _createNew;

    public ServerSettingsFileStore()
        : this(Environment.GetEnvironmentVariable, ModuleRootOfThisAssembly(), Environment.CurrentDirectory)
    {
    }

    /// <param name="createNew">Test seam for the template's creation step; the default opens with <c>FileMode.CreateNew</c>.</param>
    internal ServerSettingsFileStore(Func<string, string?> environment, string? moduleRoot, string currentDirectory,
        Func<string, Stream>? createNew = null)
    {
        _environment = environment;
        _moduleRoot = moduleRoot;
        _currentDirectory = currentDirectory;
        _createNew = createNew ?? (path => new FileStream(path, FileMode.CreateNew, FileAccess.Write));
    }

    public ServerSettingsLocation Locate()
    {
        var userDir = _environment(UserDirVariable);
        if (!string.IsNullOrWhiteSpace(userDir))
            return At(userDir!, UserDirVariable);
        if (!string.IsNullOrWhiteSpace(_moduleRoot))
            return At(_moduleRoot!, "TAOM module folder");
        return At(_currentDirectory, "current directory");
    }

    public ServerFileRead TryRead(string path)
    {
        try
        {
            if (!File.Exists(path))
                return Directory.Exists(path)
                    ? new ServerFileRead(ServerFileState.Unreadable, null, path + " is a directory, not a file")
                    : new ServerFileRead(ServerFileState.Missing, null, null);
            return new ServerFileRead(ServerFileState.Loaded, File.ReadAllText(path, Encoding.UTF8), null);
        }
        catch (Exception ex)
        {
            return new ServerFileRead(ServerFileState.Unreadable, null, ex.GetType().Name + ": " + ex.Message);
        }
    }

    public string? WriteIfMissing(string path, string content)
    {
        Stream stream;
        try
        {
            if (File.Exists(path)) return null;
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            // CreateNew, so a file that appeared since the check is left alone.
            stream = _createNew(path);
        }
        catch (IOException) when (File.Exists(path))
        {
            // Creation only: another process made the file first, and its copy stands.
            return null;
        }
        catch (Exception ex)
        {
            return WriteError(path, ex);
        }

        // This process created the file. A failed write would leave an empty or partial template that every later
        // boot keeps, so remove it and report the failure.
        try
        {
            using (var writer = new StreamWriter(stream, Utf8NoBom))
                writer.Write(content);
            return null;
        }
        catch (Exception ex)
        {
            stream.Dispose();
            TryDelete(path);
            return WriteError(path, ex);
        }
    }

    public string? WriteIfChanged(string path, string content)
    {
        try
        {
            if (File.Exists(path) && string.Equals(File.ReadAllText(path, Encoding.UTF8), content, StringComparison.Ordinal))
                return null;
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            File.WriteAllText(path, content, Utf8NoBom);
            return null;
        }
        catch (Exception ex)
        {
            return WriteError(path, ex);
        }
    }

    private static string WriteError(string path, Exception ex) =>
        "could not write " + path + ": " + ex.GetType().Name + ": " + ex.Message;

    private static void TryDelete(string path)
    {
        try
        {
            File.Delete(path);
        }
        catch (Exception)
        {
            // Best effort: the returned error already names the failed write.
        }
    }

    private static ServerSettingsLocation At(string folder, string rule) =>
        new(Path.Combine(folder, FileName), Path.Combine(folder, DefaultsFileName), rule);

    // TAOM.dll sits in <module>\bin\Win64_Shipping_Server (or _Client), two folders below the module root.
    private static string? ModuleRootOfThisAssembly()
    {
        try
        {
            var dll = typeof(ServerSettingsFileStore).Assembly.Location;
            if (string.IsNullOrEmpty(dll)) return null;
            return Directory.GetParent(Path.GetDirectoryName(dll)!)?.Parent?.FullName;
        }
        catch (Exception)
        {
            return null;
        }
    }
}
