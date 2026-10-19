using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using TAOM.Adapters;

namespace TAOM.Features.ShaderSourceInstall;

public sealed class ShaderSourceInstallResult
{
    public List<string> Copied { get; } = new();
    public int UpToDate { get; set; }
    public List<(string Name, string Message)> Failed { get; } = new();
}

/// <summary>
/// Copies the module's shader sources (<c>*.rs</c>, <c>*.rsh</c>) into the game's shader folder, which
/// is the only place the engine compiles from. Writes a file only when it is missing or its bytes
/// differ, never deletes or touches any other file, and reports a failing file without stopping the rest.
/// A module folder that is missing or holds no <c>.rs</c> file is reported as a failure.
/// </summary>
public sealed class ShaderSourceInstallService
{
    private readonly IShaderSourceFileSystem _fileSystem;

    public ShaderSourceInstallService(IShaderSourceFileSystem fileSystem)
    {
        _fileSystem = fileSystem;
    }

    public ShaderSourceInstallResult Install(string moduleSourcesFolder, string gameSourcesFolder)
    {
        var result = new ShaderSourceInstallResult();
        IReadOnlyList<string> files;
        try
        {
            files = _fileSystem.ListFiles(moduleSourcesFolder);
        }
        catch (Exception e)
        {
            result.Failed.Add((moduleSourcesFolder, e.Message));
            return result;
        }

        var sources = files.Where(IsShaderSource).ToList();
        if (!sources.Any(f => f.EndsWith(".rs", StringComparison.OrdinalIgnoreCase)))
        {
            // A module folder with no .rs file means the install itself is broken, not that there is nothing to copy.
            result.Failed.Add((moduleSourcesFolder, "no .rs shader source found in the module folder"));
            return result;
        }

        foreach (var file in sources)
        {
            try
            {
                InstallOne(file, gameSourcesFolder, result);
            }
            catch (Exception e)
            {
                result.Failed.Add((Path.GetFileName(file), e.Message));
            }
        }

        return result;
    }

    private void InstallOne(string file, string gameSourcesFolder, ShaderSourceInstallResult result)
    {
        var bytes = _fileSystem.ReadAllBytes(file);
        var name = Path.GetFileName(file);
        var target = Path.Combine(gameSourcesFolder, name);
        if (_fileSystem.FileExists(target) && _fileSystem.ReadAllBytes(target).SequenceEqual(bytes))
        {
            result.UpToDate++;
            return;
        }

        _fileSystem.WriteAllBytes(target, bytes);
        result.Copied.Add(name);
    }

    private static bool IsShaderSource(string file) =>
        file.EndsWith(".rs", StringComparison.OrdinalIgnoreCase)
        || file.EndsWith(".rsh", StringComparison.OrdinalIgnoreCase);
}
