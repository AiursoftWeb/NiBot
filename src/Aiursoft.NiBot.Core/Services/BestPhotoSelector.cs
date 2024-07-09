using Aiursoft.NiBot.Core.Models;

namespace Aiursoft.NiBot.Core.Services;

public class BestPhotoSelector
{
    public MappedImage FindBestPhoto(IEnumerable<MappedImage> group, KeepPreference[] keepPreferences)
    {
        var query = group.OrderByDescending(ConvertKeepPreferenceToExpression.Convert(keepPreferences.First()));
        query = keepPreferences.Skip(1).Aggregate(query,
            (current, keepPreference) =>
                current.ThenByDescending(ConvertKeepPreferenceToExpression.Convert(keepPreference)));
        var bestPhoto = query.First();
        return bestPhoto;
    }
}