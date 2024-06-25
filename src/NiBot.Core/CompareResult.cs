using System.Numerics;

namespace Aiursoft.NiBot.Core;

public class CompareResult(MappedImage left, MappedImage right)
{
    public MappedImage Left { get; } = left;
    public MappedImage Right { get; } = right;
    public double Similarity { get; } = GetSimilarityRatio(left, right);
    
    private static double GetSimilarityRatio(MappedImage left, MappedImage right)
    {
        var similarity = GetSimilarity(left, right);
        return (64 - similarity) / 64.0;
    }
    
    private static int GetSimilarity(MappedImage left, MappedImage right)
    {
        return BitOperations.PopCount(left.Hash ^ right.Hash);
    }
}