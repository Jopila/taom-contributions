using System.Collections.Generic;

namespace TAOM.Adapters;

/// <summary>One file in a folder listing: its file name and full path.</summary>
public readonly struct ShaderSourceFile
{
    public ShaderSourceFile(string name, string fullPath)
    {
        Name = name;
        FullPath = fullPath;
    }

    public string Name { get; }
    public string FullPath { get; }
}

/// <summary>File access seam for the shader source install, so the logic runs without a disk.</summary>
public interface IShaderSourceFileSystem
{
    IReadOnlyList<ShaderSourceFile> ListFiles(string folder);
    byte[] ReadAllBytes(string path);

    /// <summary>Writes the file, creating the target directory first when it is missing.</summary>
    void WriteAllBytes(string path, byte[] bytes);

    bool FileExists(string path);
}
