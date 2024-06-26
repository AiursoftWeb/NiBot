using System.Numerics;
using CoenM.ImageHash;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

namespace NiBot.Dedup.Models;

public class MappedImage
{
    public int Id { get; set; }
    public string PhysicalPath { get; }
    public ulong Hash { get; }

    private MappedImage(string physicalPath, ulong hash)
    {
        PhysicalPath = physicalPath;
        Hash = hash;
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