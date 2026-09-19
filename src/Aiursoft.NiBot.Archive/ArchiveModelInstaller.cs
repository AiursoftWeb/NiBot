namespace Aiursoft.NiBot.Archive;

public static class ArchiveModelInstaller
{
    private static readonly HttpClient Client = new(new HttpClientHandler { AllowAutoRedirect = false }) { Timeout = TimeSpan.FromHours(1) };

    public static string DefaultPath => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "NiBot", "models", "clip-vit-large-patch14.onnx");

    public static async Task InstallAsync(string source, string destination, string expectedSha256, CancellationToken cancellationToken = default)
    {
        if (expectedSha256.Length != 64 || expectedSha256.Any(c => !char.IsAsciiHexDigit(c)))
            throw new ArgumentException("Provide the model publisher's SHA-256 checksum (64 hexadecimal characters).");
        destination = Path.GetFullPath(destination);
        ArchiveFiles.AssertNoLinks(destination);
        if (File.Exists(destination))
        {
            if (!ArchiveFiles.Hash(destination).Equals(expectedSha256, StringComparison.OrdinalIgnoreCase))
                throw new IOException("A different model already exists; choose another destination.");
            using var existing = new OnnxClipEmbeddingProvider(destination, 1);
            return;
        }
        Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
        var temporary = destination + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            await using (var output = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None))
            {
                if (Uri.TryCreate(source, UriKind.Absolute, out var uri) && !uri.IsFile)
                {
                    if (uri.Scheme != Uri.UriSchemeHttps) throw new ArgumentException("Model URLs must use HTTPS.");
                    using var response = await Client.GetAsync(uri, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
                    response.EnsureSuccessStatusCode();
                    await response.Content.CopyToAsync(output, cancellationToken);
                }
                else
                {
                    var path = uri?.IsFile == true ? uri.LocalPath : source;
                    ArchiveFiles.AssertNoLinks(path);
                    await using var input = File.OpenRead(path);
                    await input.CopyToAsync(output, cancellationToken);
                }
                await output.FlushAsync(cancellationToken);
            }
            if (!ArchiveFiles.Hash(temporary).Equals(expectedSha256, StringComparison.OrdinalIgnoreCase))
                throw new InvalidDataException("Model checksum mismatch; nothing was installed.");
            using (new OnnxClipEmbeddingProvider(temporary, 1)) { }
            cancellationToken.ThrowIfCancellationRequested();
            ArchiveFiles.AssertNoLinks(destination);
            File.Move(temporary, destination, false);
        }
        finally
        {
            if (File.Exists(temporary)) File.Delete(temporary);
        }
    }
}
