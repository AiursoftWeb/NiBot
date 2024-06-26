using System.Numerics;
using Aiursoft.NiBot.Dedup.Util;
using CoenM.ImageHash;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

namespace Aiursoft.NiBot.Dedup.Models;

public class MappedImage
{
    public int Id { get; set; }
    public string PhysicalPath { get; }
    public ulong Hash { get; }
    public long Size { get; }
    public long Resolution { get; }
    public DateTime LastWriteTime { get; }
    public bool IsGrayscale { get; }

    private MappedImage(string physicalPath, ulong hash)
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
        
        // Get the last write time.
        LastWriteTime = file.LastWriteTime;
        
        // Load the image to get the resolution.
        using var img = Image.Load<Rgba32>(physicalPath);
        Resolution = img.Width * img.Height;
        
        // Load the image to check if it's colored or white/black.
        IsGrayscale = GrayscaleChecker.IsImageGrayscale(img, Resolution);
    }
    
    public static Task<MappedImage> CreateAsync(string physicalPath, IImageHash hashAlgo)
    {
        return Task.Run(() =>
        {
            using var img = Image.Load<Rgba32>(physicalPath);
            var hash = hashAlgo.Hash(img);
            return new MappedImage(physicalPath, hash);
        });
    }

    public override string ToString()
    {
        return PhysicalPath;
    }

    public int ImageDiff(MappedImage other)
    {
        return BitOperations.PopCount(Hash ^ other.Hash);
    }
}