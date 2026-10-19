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

        public bool ListingThrows;

        public IReadOnlyList<string> ListFiles(string folder)
        {
            if (ListingThrows) throw new IOException("listing denied");
            return Files.Keys.Where(p => string.Equals(Path.GetDirectoryName(p), folder, StringComparison.OrdinalIgnoreCase)).ToList();
        }

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
        fs.Files[M("b.rs")] = new byte[] { 5 };
        fs.Files[G("b.rs")] = new byte[] { 5 };

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
    public void Install_MissingSourceFolder_ReportsFailureAndCopiesNothing()
    {
        var fs = new FakeFileSystem();

        var result = new ShaderSourceInstallService(fs).Install(Module, Game);

        Assert.AreEqual(0, result.Copied.Count);
        Assert.AreEqual(0, result.UpToDate);
        Assert.AreEqual(0, fs.Writes.Count);
        Assert.AreEqual(1, result.Failed.Count);
        Assert.AreEqual(Module, result.Failed[0].Name);
    }

    [TestMethod]
    public void Install_NoRsFile_ReportsFailure()
    {
        var fs = new FakeFileSystem();
        fs.Files[M("common.rsh")] = new byte[] { 1 };
        fs.Files[M("notes.txt")] = new byte[] { 1 };

        var result = new ShaderSourceInstallService(fs).Install(Module, Game);

        Assert.AreEqual(1, result.Failed.Count);
        Assert.AreEqual(Module, result.Failed[0].Name);
    }

    [TestMethod]
    public void Install_RsFilePresent_ReportsNoFailure()
    {
        var fs = new FakeFileSystem();
        fs.Files[M("a.rs")] = new byte[] { 1 };

        var result = new ShaderSourceInstallService(fs).Install(Module, Game);

        Assert.AreEqual(0, result.Failed.Count);
    }

    [TestMethod]
    public void Install_ListingThrows_ReportsTheFolderAndCopiesNothing()
    {
        var fs = new FakeFileSystem { ListingThrows = true };
        fs.Files[M("a.rs")] = new byte[] { 1 };

        var result = new ShaderSourceInstallService(fs).Install(Module, Game);

        Assert.AreEqual(0, result.Copied.Count);
        Assert.AreEqual(0, fs.Writes.Count);
        Assert.AreEqual(1, result.Failed.Count);
        Assert.AreEqual(Module, result.Failed[0].Name);
        Assert.AreEqual("listing denied", result.Failed[0].Message);
    }

    [TestMethod]
    public void Install_UpperCaseExtension_IsCopied()
    {
        var fs = new FakeFileSystem();
        fs.Files[M("A.RS")] = new byte[] { 4 };

        var result = new ShaderSourceInstallService(fs).Install(Module, Game);

        CollectionAssert.AreEqual(new[] { "A.RS" }, result.Copied);
        CollectionAssert.AreEqual(new byte[] { 4 }, fs.Files[G("A.RS")]);
        Assert.AreEqual(0, result.Failed.Count);
    }

    [TestMethod]
    public void Install_OtherExtensions_AreIgnored()
    {
        var fs = new FakeFileSystem();
        fs.Files[M("a.rs")] = new byte[] { 1 };
        fs.Files[M("notes.txt")] = new byte[] { 1 };
        fs.Files[M("a.rs.bak")] = new byte[] { 1 };

        var result = new ShaderSourceInstallService(fs).Install(Module, Game);

        CollectionAssert.AreEqual(new[] { "a.rs" }, result.Copied);
        CollectionAssert.AreEqual(new[] { G("a.rs") }, fs.Writes);
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
