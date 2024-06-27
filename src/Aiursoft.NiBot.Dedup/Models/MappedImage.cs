using System.Numerics;
using Aiursoft.NiBot.Dedup.Util;
using CoenM.ImageHash;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

namespace Aiursoft.NiBot.Dedup.Models;

public class MappedImage
{
    private readonly Lazy<bool> _isGrayscale;
    public int Id { get; set; }
    public string PhysicalPath { get; }
    public ulong Hash { get; }
    public long Size { get; }
    public long Resolution { get; }
    public DateTime LastWriteTime { get; }

    public bool IsGrayscale => _isGrayscale.Value;

    private MappedImage(string physicalPath, ulong hash, int resolution)
    {
        PhysicalPath = physicalPath;
        Hash = hash;
        
        // Get the file size.
        var file = new FileInfo(physicalPath);
        if (!file.Exists)
        {
            throw new FileNotFoundException("File not found", physicalPath);
        }
        Size = file.Length;
        Resolution = resolution;
        
        // Get the last write time.
        LastWriteTime = file.LastWriteTime;
        
        _isGrayscale = new Lazy<bool>(() =>
        {
            // Load the image to get the resolution.
            using var img = Image.Load<Rgba32>(PhysicalPath);
            // Load the image to check if it's colored or white/black.
            return GrayscaleChecker.IsImageGrayscale(img, Resolution);
        });
    }
    
    public static Task<MappedImage> CreateAsync(string physicalPath, IImageHash hashAlgo)
    {
        return Task.Run(() =>
        {
            using var img = Image.Load<Rgba32>(physicalPath);
            var resolution = img.Width * img.Height; // The image will be modified when hashing
            var hash = hashAlgo.Hash(img);
            return new MappedImage(physicalPath, hash, resolution);
        });
    }

    public override string ToString()
    {
        return PhysicalPath;
    }
    
    public double ImageSimilarityRatio(MappedImage other)
    {
        return (64 - ImageDiff(other)) / 64.0; // 0 - 1, higher is more similar.
    }

    public int ImageDiff(MappedImage other)
    {
        return BitOperations.PopCount(Hash ^ other.Hash); // 0 - 64, higher is more different.
    }
}