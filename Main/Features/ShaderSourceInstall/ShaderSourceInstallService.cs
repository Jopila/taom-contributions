using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using TAOM.Adapters;

namespace TAOM.Features.ShaderSourceInstall;

/// <summary>One file that could not be installed: its name and the exception message.</summary>
public readonly struct ShaderSourceInstallFailure
{
    public ShaderSourceInstallFailure(string name, string message)
    {
        Name = name;
        Message = message;
    }

    public string Name { get; }
    public string Message { get; }
}

public sealed class ShaderSourceInstallResult
{
    public List<string> Copied { get; } = new();
    public int UpToDate { get; set; }
    public List<ShaderSourceInstallFailure> Failed { get; } = new();
}

/// <summary>
/// Copies the module's shader sources (<c>*.rs</c>, <c>*.rsh</c>) into the game's shader folder, which
/// is the only place the engine compiles from. Writes a file only when it is missing or its bytes
/// differ, never deletes or touches any other file, and reports a failing file without stopping the rest.
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
        IReadOnlyList<ShaderSourceFile> files;
        try
        {
            files = _fileSystem.ListFiles(moduleSourcesFolder);
        }
        catch (Exception e)
        {
            result.Failed.Add(new ShaderSourceInstallFailure(moduleSourcesFolder, e.Message));
            return result;
        }

        foreach (var file in files.Where(IsShaderSource))
        {
            try
            {
                InstallOne(file, gameSourcesFolder, result);
            }
            catch (Exception e)
            {
                result.Failed.Add(new ShaderSourceInstallFailure(file.Name, e.Message));
            }
        }

        return result;
    }

    private void InstallOne(ShaderSourceFile file, string gameSourcesFolder, ShaderSourceInstallResult result)
    {
        var bytes = _fileSystem.ReadAllBytes(file.FullPath);
        var target = Path.Combine(gameSourcesFolder, file.Name);
        if (_fileSystem.FileExists(target) && _fileSystem.ReadAllBytes(target).SequenceEqual(bytes))
        {
            result.UpToDate++;
            return;
        }

        _fileSystem.WriteAllBytes(target, bytes);
        result.Copied.Add(file.Name);
    }

    private static bool IsShaderSource(ShaderSourceFile file) =>
        file.Name.EndsWith(".rs", StringComparison.OrdinalIgnoreCase)
        || file.Name.EndsWith(".rsh", StringComparison.OrdinalIgnoreCase);
}
