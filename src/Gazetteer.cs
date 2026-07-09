using System.Globalization;
using System.Text;

namespace hammap;

/// <summary>
/// A resolved settlement location.
/// </summary>
public sealed record Place(string Name, double Lat, double Lon, long Population, bool IsPopulatedPlace);

/// <summary>
/// Offline gazetteer of Hungarian settlements, loaded from the GeoNames HU dump
/// (https://download.geonames.org/export/dump/, licensed CC-BY 4.0).
///
/// Only real coordinates from that dataset are ever returned. City names that
/// cannot be found are reported as unresolved rather than being guessed.
/// </summary>
public sealed class Gazetteer
{
    // normalized name -> best matching place (highest population wins)
    private readonly Dictionary<string, Place> _byName;

    private Gazetteer(Dictionary<string, Place> byName) => _byName = byName;

    public int Count => _byName.Count;

    public static Gazetteer Load(string path)
    {
        // GeoNames columns (tab separated), see readme.txt:
        // 0 geonameid, 1 name, 2 asciiname, 3 alternatenames, 4 lat, 5 lon,
        // 6 feature class, 7 feature code, ... 14 population
        var best = new Dictionary<string, Place>();

        foreach (var line in File.ReadLines(path, Encoding.UTF8))
        {
            var f = line.Split('\t');
            if (f.Length < 15) continue;

            var featureClass = f[6];
            // Keep only populated places (P) and administrative regions (A).
            if (featureClass is not ("P" or "A")) continue;

            if (!double.TryParse(f[4], NumberStyles.Float, CultureInfo.InvariantCulture, out var lat)) continue;
            if (!double.TryParse(f[5], NumberStyles.Float, CultureInfo.InvariantCulture, out var lon)) continue;
            long.TryParse(f[14], NumberStyles.Integer, CultureInfo.InvariantCulture, out var pop);

            var place = new Place(f[1], lat, lon, pop, featureClass == "P");

            // Index the primary name, the ascii name, and every alternate name so
            // that accented / alternative spellings all resolve.
            AddCandidate(best, f[1], place);
            AddCandidate(best, f[2], place);
            foreach (var alt in f[3].Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            {
                AddCandidate(best, alt, place);
            }
        }

        return new Gazetteer(best);
    }

    private static void AddCandidate(Dictionary<string, Place> map, string rawName, Place place)
    {
        var key = Normalize(rawName);
        if (key.Length == 0) return;

        // For an ambiguous name, prefer an actual populated place over an
        // administrative region, then prefer the larger population.
        if (!map.TryGetValue(key, out var existing) || IsBetter(place, existing))
        {
            map[key] = place;
        }
    }

    private static bool IsBetter(Place candidate, Place current)
    {
        if (candidate.IsPopulatedPlace != current.IsPopulatedPlace)
        {
            return candidate.IsPopulatedPlace;
        }
        return candidate.Population > current.Population;
    }

    /// <summary>
    /// Resolve a callbook city string to a real location, or null if unknown.
    /// </summary>
    public Place? Resolve(string city)
    {
        var key = Normalize(city);
        if (key.Length == 0) return null;

        if (_byName.TryGetValue(key, out var hit)) return hit;

        // Fall back: strip a trailing qualifier such as a Budapest district
        // ("Budapest I.", "Budapest-Janos") and retry with the base name.
        var cut = key.IndexOfAny([' ', '-']);
        if (cut > 0)
        {
            var baseKey = key[..cut];
            if (baseKey.Length >= 3 && _byName.TryGetValue(baseKey, out var basehit)) return basehit;
        }

        return null;
    }

    /// <summary>
    /// Lowercase, trim, and strip diacritics so "Ráckeresztúr" == "rackeresztur".
    /// </summary>
    public static string Normalize(string s)
    {
        if (string.IsNullOrWhiteSpace(s)) return string.Empty;

        var decomposed = s.Trim().ToLowerInvariant().Normalize(NormalizationForm.FormD);
        var sb = new StringBuilder(decomposed.Length);
        foreach (var ch in decomposed)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(ch) != UnicodeCategory.NonSpacingMark)
            {
                sb.Append(ch);
            }
        }
        return sb.ToString().Normalize(NormalizationForm.FormC);
    }
}
