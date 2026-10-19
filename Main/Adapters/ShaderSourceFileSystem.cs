using System.Collections.Generic;
using System.IO;

namespace TAOM.Adapters;

public sealed class ShaderSourceFileSystem : IShaderSourceFileSystem
{
    public IReadOnlyList<ShaderSourceFile> ListFiles(string folder)
    {
        var files = new List<ShaderSourceFile>();
        if (!Directory.Exists(folder)) return files;
        foreach (var path in Directory.GetFiles(folder))
            files.Add(new ShaderSourceFile(Path.GetFileName(path), path));
        return files;
    }

    public byte[] ReadAllBytes(string path) => File.ReadAllBytes(path);

    public void WriteAllBytes(string path, byte[] bytes)
    {
        var directory = Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(directory)) Directory.CreateDirectory(directory);
        File.WriteAllBytes(path, bytes);
    }

    public bool FileExists(string path) => File.Exists(path);
}
