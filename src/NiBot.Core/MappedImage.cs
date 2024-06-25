using CoenM.ImageHash;

namespace Aiursoft.NiBot.Core;

public class MappedImage
{
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
}