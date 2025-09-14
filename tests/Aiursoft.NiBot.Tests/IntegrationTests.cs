using Aiursoft.CommandFramework;
using Aiursoft.CommandFramework.Models;
using Aiursoft.CSTools.Tools;
using Aiursoft.NiBot.Dedup;

namespace Aiursoft.NiBot.Tests;

[TestClass]
public class IntegrationTests
{
    private readonly string _imageAssets = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "assets");
    private string _tempAssetsFolder = Path.Combine(Path.GetTempPath(), $"NiBot-UT-{Guid.NewGuid()}");

    private NestedCommandApp Program => new NestedCommandApp()
        .WithGlobalOptions(CommonOptionsProvider.DryRunOption)
        .WithGlobalOptions(CommonOptionsProvider.VerboseOption)
        .WithFeature(new DedupHandler())
        .WithFeature(new DedupCopyHandler())
        .WithFeature(new CompareHandler());

    [TestInitialize]
    public void Initialize()
    {
        _tempAssetsFolder = Path.Combine(Path.GetTempPath(), $"NiBot-UT-{Guid.NewGuid()}");
        _imageAssets.CopyFilesRecursively(_tempAssetsFolder);
        Console.WriteLine($"Temp folder: {_tempAssetsFolder} was filled with assets from {_imageAssets}");
    }

    [TestCleanup]
    public void Cleanup()
    {
        FolderDeleter.DeleteByForce(_tempAssetsFolder);
        Console.WriteLine($"Temp folder deleted: {_tempAssetsFolder}");
    }

    [TestMethod]
    public async Task InvokeHelp()
    {
        var result = await Program.TestRunAsync(["--help"]);

        Assert.AreEqual(0, result.ProgramReturn);
        Assert.IsTrue(result.Output.Contains("Options:"));
        Assert.IsTrue(string.IsNullOrWhiteSpace(result.Error));
    }

    [TestMethod]
    public async Task InvokeVersion()
    {
        var result = await Program.TestRunAsync(["--version"]);
        Assert.AreEqual(0, result.ProgramReturn);
    }

    [TestMethod]
    public async Task InvokeUnknown()
    {
        var result = await Program.TestRunAsync(["--wtf"]);
        Assert.AreEqual(1, result.ProgramReturn);
    }

    [TestMethod]
    public async Task InvokeWithoutArg()
    {
        var result = await Program.TestRunAsync([]);
        Assert.AreEqual(1, result.ProgramReturn);
    }

    [TestMethod]
    public async Task InvokeCompare()
    {
        var image1 = Path.Combine(_tempAssetsFolder, "mc", "p1.png");
        var image2 =
            Path.Combine(_tempAssetsFolder, "mc", "p2.png"); // P2 quality is better. P1 and P2 similarity is 96.88%
        var result = await Program.TestRunAsync(["compare", "-i", image1, "-i", image2, "-v"]);
        Assert.AreEqual(0, result.ProgramReturn);
    }

    [TestMethod]
    public async Task InvokeDedup()
    {
        var dedupPath = Path.Combine(_tempAssetsFolder, "mc");
        var result =
            await Program.TestRunAsync([
                "dedup", "--path", dedupPath, "--yes", "--duplicate-similar", "90"
            ]); // P2 quality is better. P1 and P2 similarity is 96.88%

        Assert.AreEqual(0, result.ProgramReturn);
        var resultFiles = Directory.GetFiles(dedupPath, "*", SearchOption.TopDirectoryOnly);

        // Only one file left.
        Assert.AreEqual(1, resultFiles.Length);

        // .trash exists.
        var trashFolder = Path.Combine(dedupPath, ".trash");
        Assert.IsTrue(Directory.Exists(trashFolder));

        // .trash contains the soft-deleted file.
        var trashFiles = Directory.GetFiles(trashFolder, "*", SearchOption.TopDirectoryOnly);

        // Two files in trash.
        Assert.AreEqual(2, trashFiles.Length);

        // p1.png in trash.
        Assert.AreEqual(Path.Combine(trashFolder, "p1.png"), trashFiles.First(t => t.Contains("png")));

        // .duplicateReasons.txt exists.
        var duplicateReasonsFile = Path.Combine(trashFolder, ".duplicateReasons.txt");
        Assert.IsTrue(File.Exists(duplicateReasonsFile));

        // .duplicateReasons.txt contains the reason.
        var duplicateReasons = await File.ReadAllTextAsync(duplicateReasonsFile);
        Assert.IsTrue(
            duplicateReasons.Contains(
                "p1.png is moved here as")); //The file p1.png is moved here as .trash\p1.png because it's a duplicate of p2.png.
        Assert.IsTrue(duplicateReasons.Contains("because it's a duplicate of"));
        Assert.IsTrue(duplicateReasons.Contains("p2.png"));
    }

    [TestMethod]
    public async Task InvokeDedupXPlane()
    {
        var dedupPath = Path.Combine(_tempAssetsFolder, "xplane");
        var result =
            await Program.TestRunAsync([
                "dedup", "--path", dedupPath, "--yes", "--duplicate-similar", "90"
            ]); // P2 quality is better. P1 and P2 similarity is 96.88%

        Assert.AreEqual(0, result.ProgramReturn);
        var resultFiles = Directory.GetFiles(dedupPath, "*", SearchOption.TopDirectoryOnly);

        // Only 3 file left.
        Assert.AreEqual(3, resultFiles.Length);

        // .trash exists.
        var trashFolder = Path.Combine(dedupPath, ".trash");
        Assert.IsTrue(Directory.Exists(trashFolder));

        // .trash contains the soft-deleted file.
        var trashFiles = Directory.GetFiles(trashFolder, "*", SearchOption.TopDirectoryOnly);

        // Two files in trash.
        Assert.AreEqual(2, trashFiles.Length);

        // p1.png in trash.
        Assert.AreEqual(Path.Combine(trashFolder, "xp3c.png"), trashFiles.First(t => t.Contains("png")));

        // .duplicateReasons.txt exists.
        var duplicateReasonsFile = Path.Combine(trashFolder, ".duplicateReasons.txt");
        Assert.IsTrue(File.Exists(duplicateReasonsFile));

        // .duplicateReasons.txt contains the reason.
        var duplicateReasons = await File.ReadAllTextAsync(duplicateReasonsFile);
        Assert.IsTrue(
            duplicateReasons.Contains(
                "xp3c.png is moved here as")); //The file p1.png is moved here as .trash\p1.png because it's a duplicate of p2.png.
        Assert.IsTrue(duplicateReasons.Contains("because it's a duplicate of"));
        Assert.IsTrue(duplicateReasons.Contains("xp3.png"));
    }


    [TestMethod]
    public async Task InvokeDedupStrict()
    {
        var dedupPath = Path.Combine(_tempAssetsFolder, "mc");
        var result =
            await Program.TestRunAsync([
                "dedup", "--path", dedupPath, "--yes", "--duplicate-similar", "99"
            ]); // P2 quality is better. P1 and P2 similarity is 96.88%

        Assert.AreEqual(0, result.ProgramReturn);
        var resultFiles = Directory.GetFiles(dedupPath, "*", SearchOption.TopDirectoryOnly);

        // Two files left.
        Assert.AreEqual(2, resultFiles.Length);

        // .trash not exists.
        var trashFolder = Path.Combine(dedupPath, ".trash");
        Assert.IsFalse(Directory.Exists(trashFolder));
    }

    [TestMethod]
    public async Task InvokeDedupCreateLink()
    {
        var dedupPath = Path.Combine(_tempAssetsFolder, "mc");
        var result = await Program.TestRunAsync([
            "dedup", "--path", dedupPath, "--yes", "--duplicate-similar", "90", "--action", "MoveToTrashAndCreateLink"
        ]); // P2 quality is better. P1 and P2 similarity is 96.88%

        Assert.AreEqual(0, result.ProgramReturn);
        var resultFiles = Directory.GetFiles(dedupPath, "*", SearchOption.TopDirectoryOnly);

        // Two files left
        Assert.AreEqual(2, resultFiles.Length, $"The folder {dedupPath} should contain 2 files.");

        // File p1 is actually a link.
        //var firstFileInfo = new FileInfo(resultFiles.First());
        var lastFileInfo = new FileInfo(resultFiles.Last());
        //Assert.IsNotNull(firstFileInfo.LinkTarget, "The first file should be a symbolic link, but its LinkTarget is null.");
        Assert.IsNull(lastFileInfo.LinkTarget, "The last file should be a regular file, but it appears to be a link.");

        // File p1 point to p2.
        var linkTarget = new FileInfo(resultFiles.First()).ResolveLinkTarget(true);
        Assert.AreEqual(resultFiles.Last(), linkTarget?.FullName);

        // .trash exists.
        var trashFolder = Path.Combine(dedupPath, ".trash");
        Assert.IsTrue(Directory.Exists(trashFolder));

        // .trash contains the soft-deleted file.
        var trashFiles = Directory.GetFiles(trashFolder, "*", SearchOption.TopDirectoryOnly);

        // Two files in trash.
        Assert.AreEqual(2, trashFiles.Length);

        // p1.png in trash.
        Assert.AreEqual(Path.Combine(trashFolder, "p1.png"), trashFiles.First(t => t.Contains("png")));

        // .duplicateReasons.txt exists.
        var duplicateReasonsFile = Path.Combine(trashFolder, ".duplicateReasons.txt");
        Assert.IsTrue(File.Exists(duplicateReasonsFile));

        // .duplicateReasons.txt contains the reason.
        var duplicateReasons = await File.ReadAllTextAsync(duplicateReasonsFile);
        Assert.IsTrue(
            duplicateReasons.Contains(
                "p1.png is moved here as")); //The file p1.png is moved here as .trash\p1.png because it's a duplicate of p2.png.
        Assert.IsTrue(duplicateReasons.Contains("because it's a duplicate of"));
        Assert.IsTrue(duplicateReasons.Contains("p2.png"));
    }

    [TestMethod]
    public async Task InvokeDedupCopy()
    {
        var mcPath = Path.Combine(_tempAssetsFolder, "mc");
        var xpPath = Path.Combine(_tempAssetsFolder, "xplane");
        var result = await Program.TestRunAsync([
            "dedup-copy",
            "--source", xpPath,
            "--destination", mcPath,
            "--yes",
            "--duplicate-similar", "90"
        ]);

        Assert.AreEqual(0, result.ProgramReturn);
        var resultFiles = Directory.GetFiles(mcPath, "*", SearchOption.TopDirectoryOnly);

        // 5 files left
        Assert.AreEqual(5, resultFiles.Length, $"The folder {mcPath} should contain 5 files.");

        // All files are real files
        // Assert.IsFalse(File.GetAttributes(resultFiles.First()).HasFlag(FileAttributes.ReparsePoint));
        // Assert.IsFalse(File.GetAttributes(resultFiles.Last()).HasFlag(FileAttributes.ReparsePoint));
        foreach (var file in resultFiles)
        {
            Assert.IsFalse(File.GetAttributes(file).HasFlag(FileAttributes.ReparsePoint));
        }

        // xp3c not exists because it has worse quality than xp3.
        Assert.IsFalse(resultFiles.Any(f => f.Contains("xp3c")));

        var result2 = await Program.TestRunAsync([
            "dedup-copy",
            "--source", xpPath,
            "--destination", mcPath,
            "--yes",
            "--duplicate-similar", "90"
        ]);

        Assert.AreEqual(0, result2.ProgramReturn);

        // After run attempt 2, everything should be the same.
        var resultFiles2 = Directory.GetFiles(mcPath, "*", SearchOption.TopDirectoryOnly);

        Assert.AreEqual(5, resultFiles2.Length);
        for (var i = 0; i < resultFiles.Length; i++)
        {
            Assert.AreEqual(resultFiles[i], resultFiles2[i]);
        }

        // xp3c not exists because it has worse quality than xp3.
        Assert.IsFalse(resultFiles2.Any(f => f.Contains("xp3c")));
    }
}
