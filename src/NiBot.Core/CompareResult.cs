namespace Aiursoft.NiBot.Core;

public class CompareResult(MappedImage left, MappedImage right, double similarity)
{
    public MappedImage Left { get; } = left;
    public MappedImage Right { get; } = right;
    public double Similarity { get; } = similarity;
}