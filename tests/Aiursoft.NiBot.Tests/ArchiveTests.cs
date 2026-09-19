using Aiursoft.CommandFramework;
using Aiursoft.NiBot.Archive;
using Aiursoft.NiBot.Dedup;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;

namespace Aiursoft.NiBot.Tests;

[TestClass]
public sealed class ArchiveTests
{
    private string _root = null!;
    private string Source => Path.Combine(_root, "source");
    private string Destination => Path.Combine(_root, "destination");
    private string Cache => Path.Combine(_root, "cache");
    private static string Model => Path.Combine(AppContext.BaseDirectory, "assets", "archive", "mean-test.onnx");

    [TestInitialize]
    public void Setup()
    {
        _root = Path.Combine(Path.GetTempPath(), "nibot-archive-tests-" + Guid.NewGuid());
        Directory.CreateDirectory(Source);
        Directory.CreateDirectory(Destination);
    }

    [TestCleanup]
    public void Cleanup() => Directory.Delete(_root, true);

    private static void Photo(string path, int seed, bool blue = false)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        using var image = new Image<Rgb24>(64, 80);
        var random = new Random(seed);
        for (var y = 0; y < image.Height; y++)
        for (var x = 0; x < image.Width; x++)
        {
            var strong = (byte)random.Next(160, 256);
            var weak = (byte)random.Next(0, 90);
            image[x, y] = blue ? new Rgb24(weak, (byte)random.Next(0, 90), strong) : new Rgb24(strong, (byte)random.Next(0, 90), weak);
        }
        if (Path.GetExtension(path).Equals(".webp", StringComparison.OrdinalIgnoreCase)) image.SaveAsWebp(path);
        else image.SaveAsPng(path);
    }

    private void References()
    {
        for (var i = 0; i < 3; i++)
        {
            Photo(Path.Combine(Destination, "red", $"red{i}.png"), 10 + i);
            Photo(Path.Combine(Destination, "blue", $"blue{i}.png"), 20 + i, blue: true);
        }
    }

    private ArchivePlan Plan(ArchiveSettings? settings = null)
    {
        using var provider = new FakeProvider();
        return new ArchivePlanner(provider, Cache).Create(Source, Destination, settings ?? new ArchiveSettings { DuplicateSimilarity = 100, Neighbors = 3 });
    }

    [TestMethod]
    public void PlansWithoutChangingImagesAndSkipsOldIncludingAdded()
    {
        References();
        Photo(Path.Combine(Destination, "Added", "unclassified.png"), 34);
        File.Copy(Path.Combine(Destination, "red", "red0.png"), Path.Combine(Source, "old.png"));
        File.Copy(Path.Combine(Destination, "Added", "unclassified.png"), Path.Combine(Source, "added.png"));
        Photo(Path.Combine(Source, "new.webp"), 99);
        var before = ArchiveFiles.Images(Destination).ToDictionary(p => p, ArchiveFiles.Hash);
        var plan = Plan();
        Assert.AreEqual(ArchiveDecision.Duplicate, plan.Items.Single(i => i.Source == "old.png").Decision);
        Assert.AreEqual(ArchiveDecision.Duplicate, plan.Items.Single(i => i.Source == "added.png").Decision);
        var fresh = plan.Items.Single(i => i.Source == "new.webp");
        Assert.AreEqual(ArchiveDecision.Ready, fresh.Decision);
        Assert.AreEqual("red", fresh.TargetFolder);
        Assert.HasCount(2, plan.Categories);
        CollectionAssert.AreEquivalent(before.Keys.ToArray(), ArchiveFiles.Images(Destination).ToArray());
        foreach (var pair in before) Assert.AreEqual(pair.Value, ArchiveFiles.Hash(pair.Key));
        Assert.IsFalse(Directory.Exists(Path.Combine(Destination, ".nibot-archive")));
    }

    [TestMethod]
    public void ApplyPreservesOldFilesHandlesNameCollisionAndIsIdempotent()
    {
        References();
        Photo(Path.Combine(Source, "red0.png"), 101);
        var old = ArchiveFiles.Hash(Path.Combine(Destination, "red", "red0.png"));
        var plan = Plan();
        var first = new ArchiveExecutor().Apply(plan);
        Assert.AreEqual("Copied", first.Single().Status);
        Assert.AreNotEqual(Path.Combine(Destination, "red", "red0.png"), first.Single().Destination);
        Assert.AreEqual(old, ArchiveFiles.Hash(Path.Combine(Destination, "red", "red0.png")));
        Assert.IsTrue(File.Exists(Path.Combine(Source, "red0.png")));
        Assert.AreEqual("AlreadyPresent", new ArchiveExecutor().Apply(plan).Single().Status);
        Assert.HasCount(7, ArchiveFiles.Images(Destination).ToArray());
        Assert.HasCount(1, ArchiveFiles.ImportedPaths(Destination));
        Assert.AreEqual(ArchiveDecision.Duplicate, Plan().Items.Single().Decision);
    }

    [TestMethod]
    public void AmbiguousImagesRemainInSourceWithCandidates()
    {
        References();
        Photo(Path.Combine(Source, "ambiguous.png"), 111);
        var plan = Plan();
        Assert.AreEqual(ArchiveDecision.Review, plan.Items.Single().Decision);
        Assert.IsNull(plan.Items.Single().TargetFolder);
        Assert.HasCount(2, plan.Items.Single().Candidates);
        Assert.IsEmpty(new ArchiveExecutor().Apply(plan));
        Assert.HasCount(6, ArchiveFiles.Images(Destination).ToArray());
    }

    [TestMethod]
    public void PerceptualDuplicatesAreReviewedUnlessExplicitlySkipped()
    {
        References();
        var original = Path.Combine(Destination, "red", "red0.png");
        using (var image = Image.Load<Rgb24>(original))
        {
            image.Metadata.ExifProfile = new SixLabors.ImageSharp.Metadata.Profiles.Exif.ExifProfile();
            image.Metadata.ExifProfile.SetValue(SixLabors.ImageSharp.Metadata.Profiles.Exif.ExifTag.Software, "new encoder");
            image.SaveAsPng(Path.Combine(Source, "reencoded.png"));
        }
        Assert.AreNotEqual(ArchiveFiles.Hash(original), ArchiveFiles.Hash(Path.Combine(Source, "reencoded.png")));
        Assert.AreEqual(ArchiveDecision.ReviewDuplicate, Plan().Items.Single().Decision);
        Assert.AreEqual(ArchiveDecision.Duplicate, Plan(new ArchiveSettings { SkipSimilar = true, DuplicateSimilarity = 100 }).Items.Single().Decision);
    }

    [TestMethod]
    public void ChangedSourcesAreNotCopied()
    {
        References();
        Photo(Path.Combine(Source, "new.png"), 99);
        var plan = Plan();
        Photo(Path.Combine(Source, "new.png"), 100);
        Assert.AreEqual("Error", new ArchiveExecutor().Apply(plan).Single().Status);
        Assert.HasCount(6, ArchiveFiles.Images(Destination).ToArray());
    }

    [TestMethod]
    public void NewlyArrivedPerceptualDuplicatePreventsStalePlanCopy()
    {
        References();
        var source = Path.Combine(Source, "new.png");
        Photo(source, 99);
        var plan = Plan();
        using (var image = Image.Load<Rgb24>(source))
        {
            image.Metadata.ExifProfile = new SixLabors.ImageSharp.Metadata.Profiles.Exif.ExifProfile();
            image.Metadata.ExifProfile.SetValue(SixLabors.ImageSharp.Metadata.Profiles.Exif.ExifTag.Software, "arrived later");
            image.SaveAsPng(Path.Combine(Destination, "red", "arrived.png"));
        }
        Assert.AreEqual("ReviewDuplicate", new ArchiveExecutor().Apply(plan).Single().Status);
        Assert.IsFalse(File.Exists(Path.Combine(Destination, "red", "new.png")));
    }

    [TestMethod]
    public void ImportsNeverBecomeReferenceExamplesOnLaterRuns()
    {
        References();
        Photo(Path.Combine(Source, "aaa-new.png"), 99);
        new ArchiveExecutor().Apply(Plan());
        Photo(Path.Combine(Source, "next.png"), 145);
        var next = Plan().Items.Single(i => i.Source == "next.png");
        Assert.IsTrue(next.Candidates.All(c => c.Examples.All(e => !e.Contains("aaa-new", StringComparison.Ordinal))));
        Assert.HasCount(3, next.Candidates.Single(c => c.Folder == "red").Examples);
    }

    [TestMethod]
    public void CanceledPlanningDoesNotWriteToLibrary()
    {
        References();
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        var provider = new FakeProvider();
        var token = cancellation.Token;
        Assert.Throws<OperationCanceledException>(() => new ArchivePlanner(provider, Cache).Create(Source, Destination, new ArchiveSettings(), token));
        Assert.IsFalse(Directory.Exists(Path.Combine(Destination, ".nibot-archive")));
    }

    [TestMethod]
    public void TraversalAndUnexpectedCategoryAreRejectedBeforeWriting()
    {
        References();
        Photo(Path.Combine(Source, "new.png"), 99);
        var plan = Plan();
        var item = plan.Items.Single();
        Assert.Throws<InvalidDataException>(() => new ArchiveExecutor().Apply(plan with { Items = [item with { Source = "../outside.png" }] }));
        Assert.Throws<InvalidDataException>(() => new ArchiveExecutor().Apply(plan with { Items = [item with { TargetFolder = "../outside" }] }));
        Assert.IsFalse(Directory.Exists(Path.Combine(Destination, ".nibot-archive")));
    }

    [TestMethod]
    public void SymlinkDestinationSwapIsRejected()
    {
        if (OperatingSystem.IsWindows()) return;
        References();
        Photo(Path.Combine(Source, "new.png"), 99);
        var plan = Plan();
        var oldFolder = Path.Combine(Destination, "red");
        var outside = Path.Combine(_root, "outside");
        Directory.Move(oldFolder, outside);
        Directory.CreateSymbolicLink(oldFolder, outside);
        Assert.Throws<IOException>(() => new ArchiveExecutor().Apply(plan));
        Directory.Delete(oldFolder);
    }

    [TestMethod]
    public void CacheUsesContentAndModelIdentityAndRecoversFromCorruption()
    {
        Photo(Path.Combine(Source, "new.png"), 99);
        var photo = ArchiveFiles.Index(Source, Path.Combine(Source, "new.png"), new ArchiveSettings());
        using var first = new FakeProvider();
        var cache = new EmbeddingCache(Cache, first);
        cache.Get(photo);
        cache.Get(photo);
        Assert.AreEqual(1, first.Calls);
        using var changedModel = new FakeProvider("different-model");
        new EmbeddingCache(Cache, changedModel).Get(photo);
        Assert.AreEqual(1, changedModel.Calls);
        foreach (var file in Directory.GetFiles(Cache, "*.json", SearchOption.AllDirectories)) File.WriteAllText(file, "broken");
        cache.Get(photo);
        Assert.AreEqual(2, first.Calls);
        Photo(photo.Path, 77);
        var changed = ArchiveFiles.Index(Source, photo.Path, new ArchiveSettings());
        cache.Get(changed);
        Assert.AreEqual(3, first.Calls);
    }

    [TestMethod]
    public void ReportEscapesNamesAndRoundTripsPlan()
    {
        References();
        var name = OperatingSystem.IsWindows() ? "new & name.png" : "<script>&.png";
        Photo(Path.Combine(Source, name), 99);
        var plan = Plan();
        var path = Path.Combine(_root, "report.json");
        ArchiveReport.Write(path, plan);
        Assert.AreEqual(name, ArchiveReport.Read(path).Items.Single().Source);
        var html = File.ReadAllText(path + ".html");
        Assert.DoesNotContain("<script>", html);
        Assert.Contains("&amp;", html);
        Assert.Throws<IOException>(() => ArchiveReport.Write(path, plan));
        Assert.Throws<ArgumentException>(() => ArchiveReport.Write(Path.Combine(Source, "report.json"), plan));
    }

    [TestMethod]
    public void CorruptSourceIsReportedAndInvalidReferenceStopsPlanning()
    {
        References();
        File.WriteAllText(Path.Combine(Source, "broken.jpg"), "not an image");
        Assert.AreEqual(ArchiveDecision.Error, Plan().Items.Single().Decision);
        File.WriteAllText(Path.Combine(Destination, "red", "broken.jpg"), "not an image");
        Assert.Throws<UnknownImageFormatException>(() => Plan());
    }

    [TestMethod]
    public void OverlappingRootsAndInvalidThresholdsAreRejected()
    {
        References();
        var provider = new FakeProvider();
        Assert.Throws<ArgumentException>(() => new ArchivePlanner(provider, Cache).Create(Destination, Destination, new ArchiveSettings()));
        Assert.Throws<ArgumentException>(() => Plan(new ArchiveSettings { MinimumSimilarity = double.NaN }));
        Assert.Throws<ArgumentException>(() => Plan(new ArchiveSettings { Neighbors = 0 }));
        Assert.Throws<InvalidDataException>(() => CategoryMatcher.Normalize([0, 0]));
        Assert.Throws<InvalidDataException>(() => CategoryMatcher.Normalize([float.NaN, 1]));
    }

    [TestMethod]
    public void OnnxProviderExecutesRealGraphWithNormalizedRgbInput()
    {
        var path = Path.Combine(Source, "white.png");
        using (var white = new Image<Rgb24>(240, 300, new Rgb24(255, 255, 255))) white.SaveAsPng(path);
        using var provider = new OnnxClipEmbeddingProvider(Model, 1);
        var actual = provider.Embed(path);
        var expected = CategoryMatcher.Normalize([(1 - 0.48145466f) / 0.26862954f, (1 - 0.4578275f) / 0.26130258f, (1 - 0.40821073f) / 0.27577711f]);
        Assert.HasCount(3, actual);
        for (var i = 0; i < 3; i++) Assert.AreEqual(expected[i], actual[i], 0.001);
    }

    [TestMethod]
    public async Task CommandLinePlansThenAppliesUsingRealOnnxRuntime()
    {
        References();
        Photo(Path.Combine(Source, "new.webp"), 99);
        var app = new NestedCommandApp().WithFeature(new ArchiveHandler()).WithFeature(new ArchiveApplyHandler());
        var report = Path.Combine(_root, "cli.json");
        var preview = await app.TestRunAsync(["archive", "--source", Source, "--destination", Destination,
            "--model", Model, "--cache", Cache, "--report", report, "--duplicate-similar", "100", "--neighbors", "3", "--classification", "nearest", "--min-similarity", "1"]);
        Assert.AreEqual(0, preview.ProgramReturn, preview.StdErr + preview.StdOut);
        Assert.HasCount(6, ArchiveFiles.Images(Destination).ToArray());
        Assert.AreEqual(ArchiveDecision.Ready, ArchiveReport.Read(report).Items.Single().Decision);
        var applied = await app.TestRunAsync(["archive-apply", "--plan", report]);
        Assert.AreEqual(0, applied.ProgramReturn, applied.StdErr + applied.StdOut);
        Assert.IsTrue(File.Exists(Path.Combine(Destination, "red", "new.webp")));
    }

    [TestMethod]
    public void NearestModeClassifiesAmbiguousImagesWithoutDisablingDeduplication()
    {
        References();
        Photo(Path.Combine(Source, "ambiguous.png"), 111);
        File.Copy(Path.Combine(Destination, "red", "red0.png"), Path.Combine(Source, "old.png"));
        var plan = Plan(new ArchiveSettings { ClassificationMode = ArchiveClassificationMode.Nearest });
        var item = plan.Items.Single(x => x.Source == "ambiguous.png");
        Assert.AreEqual(ArchiveDecision.Ready, item.Decision);
        Assert.AreEqual(item.Candidates[0].Folder, item.TargetFolder);
        Assert.AreEqual(ArchiveDecision.Duplicate, plan.Items.Single(x => x.Source == "old.png").Decision);
    }

    [TestMethod]
    public async Task ModelInstallationChecksContentAndDoesNotOverwriteOrLeaveTemporaryFiles()
    {
        var target = Path.Combine(_root, "models", "clip.onnx");
        var hash = ArchiveFiles.Hash(Model);
        await ArchiveModelInstaller.InstallAsync(Model, target, hash);
        await ArchiveModelInstaller.InstallAsync(Model, target, hash);
        Assert.AreEqual(hash, ArchiveFiles.Hash(target));
        await Assert.ThrowsAsync<IOException>(() => ArchiveModelInstaller.InstallAsync(Model, target, new string('0', 64)));
        var other = Path.Combine(_root, "models", "other.onnx");
        await Assert.ThrowsAsync<InvalidDataException>(() => ArchiveModelInstaller.InstallAsync(Model, other, new string('0', 64)));
        Assert.IsFalse(File.Exists(other));
        Assert.HasCount(1, Directory.GetFiles(Path.GetDirectoryName(target)!));
        await Assert.ThrowsAsync<ArgumentException>(() => ArchiveModelInstaller.InstallAsync("http://example.invalid/model", other, hash));
        Assert.HasCount(1, Directory.GetFiles(Path.GetDirectoryName(target)!));
    }

    [TestMethod]
    public void VisualDuplicateHintsCatchCenterCropsButRejectDifferentStructure()
    {
        var original = Path.Combine(Source, "original.png");
        using (var image = new Image<Rgb24>(100, 140))
        {
            for (var y = 0; y < image.Height; y++)
            for (var x = 0; x < image.Width; x++)
                image[x, y] = new Rgb24((byte)(x * 2), (byte)((y * 3 + x * x) % 256), (byte)(y % 256));
            image.SaveAsPng(original);
            image.Mutate(c => c.Crop(new Rectangle(0, 20, 100, 100)));
            image.SaveAsJpeg(Path.Combine(Source, "crop.jpg"));
        }
        Photo(Path.Combine(Source, "different.png"), 912);
        var detector = new VisualDuplicateDetector();
        Assert.IsTrue(detector.IsPossibleDuplicate(original, Path.Combine(Source, "crop.jpg")));
        Assert.IsFalse(detector.IsPossibleDuplicate(original, Path.Combine(Source, "different.png")));
    }

    [TestMethod]
    public async Task InvalidModelIsRejectedEvenWithMatchingChecksum()
    {
        var invalid = Path.Combine(_root, "invalid.onnx");
        File.WriteAllText(invalid, "not an ONNX graph");
        var target = Path.Combine(_root, "models", "model.onnx");
        await Assert.ThrowsAsync<Microsoft.ML.OnnxRuntime.OnnxRuntimeException>(() =>
            ArchiveModelInstaller.InstallAsync(invalid, target, ArchiveFiles.Hash(invalid)));
        Assert.IsFalse(File.Exists(target));
        Assert.IsEmpty(Directory.GetFiles(Path.GetDirectoryName(target)!));
    }

    [TestMethod]
    public async Task ModelCommandInstallsVerifiedFile()
    {
        var target = Path.Combine(_root, "models", "model.onnx");
        var result = await new NestedCommandApp().WithFeature(new ArchiveModelHandler()).TestRunAsync(
            ["archive-model", "--source", Model, "--sha256", ArchiveFiles.Hash(Model), "--destination", target]);
        Assert.AreEqual(0, result.ProgramReturn, result.StdErr + result.StdOut);
        Assert.AreEqual(ArchiveFiles.Hash(Model), ArchiveFiles.Hash(target));
    }

    private sealed class FakeProvider(string fingerprint = "test-model") : IImageEmbeddingProvider
    {
        public string Fingerprint => fingerprint;
        public int Calls { get; private set; }
        public float[] Embed(string path)
        {
            Calls++;
            return Path.GetFileName(path).StartsWith("ambiguous", StringComparison.Ordinal) ? CategoryMatcher.Normalize([1, 1]) :
                Path.GetFileName(path).StartsWith("blue", StringComparison.Ordinal) ? [0, 1] : [1, 0];
        }
        public void Dispose() { }
    }
}
