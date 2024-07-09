namespace Aiursoft.NiBot.Core.Models;

public enum KeepPreference
{
    Newest,
    Oldest,
    Largest,
    Smallest,
    HighestResolution,
    LowestResolution,
    GrayScale,
    Colorful
}

public static class ConvertKeepPreferenceToExpression
{
    public static Func<MappedImage, IComparable> Convert(KeepPreference preference)
    {
        return preference switch
        {
            KeepPreference.Newest => t => t.LastWriteTime,
            KeepPreference.Oldest => t => DateTime.MaxValue - t.LastWriteTime,
            KeepPreference.Largest => t => t.Size,
            KeepPreference.Smallest => t => long.MaxValue - t.Size,
            KeepPreference.HighestResolution => t => t.Resolution,
            KeepPreference.LowestResolution => t => -1 * t.Resolution,
            KeepPreference.GrayScale => t => t.IsGrayscale ? 1 : 0,
            KeepPreference.Colorful => t => t.IsGrayscale ? 0 : 1,
            _ => throw new ArgumentOutOfRangeException(nameof(preference), preference, null)
        };
    }    
}