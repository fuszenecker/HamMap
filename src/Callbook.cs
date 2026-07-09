using System.Text;
using System.Xml.Linq;

namespace hammap;

/// <summary>One licensee record from the NMHH call sign book.</summary>
public sealed record CallbookEntry(string CallSign, string Country, string City, string Category);

/// <summary>
/// Parser for the NMHH amateur call sign book, published as MS SpreadsheetML XML
/// (https://nmhh.hu/amator/call_sign_book.xml). The file is ISO-8859-2 encoded.
/// </summary>
public static class Callbook
{
    private static readonly XNamespace Ss = "urn:schemas-microsoft-com:office:spreadsheet";

    public static List<CallbookEntry> Load(string path)
    {
        // The declared encoding is ISO-8859-2; register the code page provider so
        // .NET can decode it, then let XDocument read the pre-decoded text.
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
        var latin2 = Encoding.GetEncoding("ISO-8859-2");
        var text = File.ReadAllText(path, latin2);

        var doc = XDocument.Parse(text);
        var rows = doc.Descendants(Ss + "Row").ToList();

        var entries = new List<CallbookEntry>();
        var isHeaderSeen = false;

        foreach (var row in rows)
        {
            var cells = row.Elements(Ss + "Cell")
                .Select(c => c.Element(Ss + "Data")?.Value?.Trim() ?? string.Empty)
                .ToList();

            if (cells.Count < 4) continue;

            // First data-bearing row is the header ("Hívójel / Call sign", ...).
            if (!isHeaderSeen)
            {
                isHeaderSeen = true;
                continue;
            }

            // Column layout: 0 call sign, 1 name, 2 country, 3 city, 4 street,
            // 5 licence no, 6 validity, 7 category, ...
            var callSign = cells[0];
            var country = cells[2];
            var city = cells[3];
            var category = cells.Count > 7 ? cells[7] : string.Empty;

            if (string.IsNullOrWhiteSpace(callSign)) continue;

            entries.Add(new CallbookEntry(callSign, country, city, category));
        }

        return entries;
    }
}
