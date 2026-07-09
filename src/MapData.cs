namespace hammap;

/// <summary>Aggregated licensee count for a single resolved city.</summary>
public sealed record CityStat(string City, double Lat, double Lon, int Count);

/// <summary>The full dataset served to the map front-end.</summary>
public sealed record MapData(
    int TotalEntries,
    int HungarianEntries,
    int Mapped,
    int Unresolved,
    IReadOnlyList<string> UnresolvedCities,
    IReadOnlyList<CityStat> Cities);

/// <summary>
/// Joins the callbook against the gazetteer and aggregates per-city counts.
/// Cities that cannot be resolved to real coordinates are counted and listed,
/// never placed at a guessed location.
/// </summary>
public static class Aggregator
{
    public static MapData Build(IReadOnlyList<CallbookEntry> entries, Gazetteer gazetteer)
    {
        // Restrict to Hungary as requested.
        var hungarian = entries
            .Where(e => e.Country.Contains("Hungary", StringComparison.OrdinalIgnoreCase)
                     || e.Country.Contains("Magyar", StringComparison.OrdinalIgnoreCase)
                     || string.IsNullOrWhiteSpace(e.Country))
            .ToList();

        // Aggregate by normalized city name to merge spelling variants.
        var buckets = new Dictionary<string, (Place place, string display, int count)>();
        var unresolved = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

        foreach (var e in hungarian)
        {
            if (string.IsNullOrWhiteSpace(e.City))
            {
                Bump(unresolved, "(no city)");
                continue;
            }

            var place = gazetteer.Resolve(e.City);
            if (place is null)
            {
                Bump(unresolved, e.City);
                continue;
            }

            var key = Gazetteer.Normalize(place.Name);
            if (buckets.TryGetValue(key, out var b))
            {
                buckets[key] = (b.place, b.display, b.count + 1);
            }
            else
            {
                buckets[key] = (place, place.Name, 1);
            }
        }

        var cities = buckets.Values
            .Select(b => new CityStat(b.display, b.place.Lat, b.place.Lon, b.count))
            .OrderByDescending(c => c.Count)
            .ToList();

        var unresolvedList = unresolved
            .OrderByDescending(kv => kv.Value)
            .Select(kv => $"{kv.Key} ({kv.Value})")
            .ToList();

        var mapped = cities.Sum(c => c.Count);

        return new MapData(
            TotalEntries: entries.Count,
            HungarianEntries: hungarian.Count,
            Mapped: mapped,
            Unresolved: hungarian.Count - mapped,
            UnresolvedCities: unresolvedList,
            Cities: cities);
    }

    private static void Bump(Dictionary<string, int> map, string key)
        => map[key] = map.TryGetValue(key, out var n) ? n + 1 : 1;
}
