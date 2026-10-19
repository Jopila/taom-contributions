using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Adapters;
using TAOM.Features.ShaderSourceInstall;

namespace TAOM.Tests.Features.ShaderSourceInstall;

[TestClass]
public class ShaderSourceInstallServiceTests
{
    private const string Module = @"C:\mod";
    private const string Game = @"C:\game";

    private sealed class FakeFileSystem : IShaderSourceFileSystem
    {
        public readonly Dictionary<string, byte[]> Files = new(StringComparer.OrdinalIgnoreCase);
        public readonly HashSet<string> FailingWrites = new(StringComparer.OrdinalIgnoreCase);
        public readonly List<string> Writes = new();

        public IReadOnlyList<ShaderSourceFile> ListFiles(string folder) =>
            Files.Keys.Where(p => string.Equals(Path.GetDirectoryName(p), folder, StringComparison.OrdinalIgnoreCase))
                .Select(p => new ShaderSourceFile(Path.GetFileName(p), p)).ToList();

        public byte[] ReadAllBytes(string path) => Files[path];

        public void WriteAllBytes(string path, byte[] bytes)
        {
            if (FailingWrites.Contains(Path.GetFileName(path))) throw new UnauthorizedAccessException("denied");
            Writes.Add(path);
            Files[path] = bytes;
        }

        public bool FileExists(string path) => Files.ContainsKey(path);
    }

    private static string M(string name) => Path.Combine(Module, name);
    private static string G(string name) => Path.Combine(Game, name);

    [TestMethod]
    public void Install_MissingTarget_Copies()
    {
        var fs = new FakeFileSystem();
        fs.Files[M("a.rs")] = new byte[] { 1, 2 };

        var result = new ShaderSourceInstallService(fs).Install(Module, Game);

        CollectionAssert.AreEqual(new[] { "a.rs" }, result.Copied);
        CollectionAssert.AreEqual(new byte[] { 1, 2 }, fs.Files[G("a.rs")]);
    }

    [TestMethod]
    public void Install_EqualBytes_SkipsWrite()
    {
        var fs = new FakeFileSystem();
        fs.Files[M("a.rs")] = new byte[] { 1, 2 };
        fs.Files[G("a.rs")] = new byte[] { 1, 2 };

        var result = new ShaderSourceInstallService(fs).Install(Module, Game);

        Assert.AreEqual(0, result.Copied.Count);
        Assert.AreEqual(1, result.UpToDate);
        Assert.AreEqual(0, fs.Writes.Count);
    }

    [TestMethod]
    public void Install_DifferentBytes_Overwrites()
    {
        var fs = new FakeFileSystem();
        fs.Files[M("a.rsh")] = new byte[] { 9 };
        fs.Files[G("a.rsh")] = new byte[] { 1 };

        var result = new ShaderSourceInstallService(fs).Install(Module, Game);

        CollectionAssert.AreEqual(new[] { "a.rsh" }, result.Copied);
        CollectionAssert.AreEqual(new byte[] { 9 }, fs.Files[G("a.rsh")]);
    }

    [TestMethod]
    public void Install_OneWriteFails_OthersStillCopiedAndFailureReported()
    {
        var fs = new FakeFileSystem();
        fs.Files[M("a.rs")] = new byte[] { 1 };
        fs.Files[M("b.rs")] = new byte[] { 2 };
        fs.Files[M("c.rs")] = new byte[] { 3 };
        fs.FailingWrites.Add("b.rs");

        var result = new ShaderSourceInstallService(fs).Install(Module, Game);

        CollectionAssert.AreEquivalent(new[] { "a.rs", "c.rs" }, result.Copied);
        Assert.AreEqual(1, result.Failed.Count);
        Assert.AreEqual("b.rs", result.Failed[0].Name);
        Assert.AreEqual("denied", result.Failed[0].Message);
    }

    [TestMethod]
    public void Install_EmptyOrMissingSourceFolder_DoesNothing()
    {
        var fs = new FakeFileSystem();

        var result = new ShaderSourceInstallService(fs).Install(Module, Game);

        Assert.AreEqual(0, result.Copied.Count);
        Assert.AreEqual(0, result.UpToDate);
        Assert.AreEqual(0, result.Failed.Count);
        Assert.AreEqual(0, fs.Writes.Count);
    }

    [TestMethod]
    public void Install_OtherExtensions_AreIgnored()
    {
        var fs = new FakeFileSystem();
        fs.Files[M("notes.txt")] = new byte[] { 1 };
        fs.Files[M("a.rs.bak")] = new byte[] { 1 };

        var result = new ShaderSourceInstallService(fs).Install(Module, Game);

        Assert.AreEqual(0, result.Copied.Count);
        Assert.AreEqual(0, fs.Writes.Count);
    }

    [TestMethod]
    public void Install_UnrelatedTargetFiles_AreNeverTouched()
    {
        var fs = new FakeFileSystem();
        fs.Files[M("a.rs")] = new byte[] { 1 };
        fs.Files[G("vanilla.rs")] = new byte[] { 7 };

        new ShaderSourceInstallService(fs).Install(Module, Game);

        CollectionAssert.AreEqual(new byte[] { 7 }, fs.Files[G("vanilla.rs")]);
        Assert.IsFalse(fs.Writes.Contains(G("vanilla.rs")));
        Assert.IsTrue(fs.Files.ContainsKey(M("a.rs")));
    }
}
