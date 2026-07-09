# HamMap

Maps the geographic **distribution** of Hungarian amateur-radio licensees onto an
OpenStreetMap layer — not individual QTHs, but per-town aggregate counts shown as
graduated circles and an optional heatmap.

## What it does

1. Parses the NMHH amateur call sign book
   ([`call_sign_book.xml`](https://nmhh.hu/amator/call_sign_book.xml), MS
   SpreadsheetML, ISO-8859-2).
2. Resolves each licensee's **city** to real coordinates using the offline
   [GeoNames](https://www.geonames.org/) Hungary gazetteer (CC-BY 4.0).
3. Aggregates licensees per town and serves the result as JSON.
4. A Leaflet + OpenStreetMap front-end renders the distribution.

Coordinates are **never fabricated**. City names that cannot be matched against the
gazetteer (foreign addresses, hamlets, source typos) are counted and listed in the
`unresolved` fields rather than placed at a guessed location. On the current dataset
**3,189 of 3,201** Hungarian records (99.6%) map cleanly across 652 towns.

## Run

```bash
cd src
dotnet run
```

Then open the URL printed in the console (e.g. `http://localhost:5xxx`). The data is
loaded and aggregated once at startup.

## Endpoints

- `/` — the interactive map (graduated circles + heatmap toggle).
- `/api/distribution` — the aggregated dataset as JSON.

### `/api/distribution` shape

```jsonc
{
  "totalEntries": 3263,        // all rows in the callbook
  "hungarianEntries": 3201,    // rows with a Hungarian address
  "mapped": 3189,              // licensees placed on the map
  "unresolved": 12,            // Hungarian rows whose city was not matched
  "unresolvedCities": ["Berlin (1)", "..."],
  "cities": [
    { "city": "Budapest", "lat": 47.49835, "lon": 19.04045, "count": 763 }
  ]
}
```

## Refreshing the data

Both source files are bundled under `src/data/` so the app runs fully offline and
reproducibly. To update them:

```bash
# Callbook
curl -L -o src/data/callbook.xml https://nmhh.hu/amator/call_sign_book.xml

# GeoNames Hungary gazetteer
curl -L -o HU.zip https://download.geonames.org/export/dump/HU.zip
unzip -o HU.zip HU.txt -d src/data
```

## Project layout

| File | Purpose |
|------|---------|
| `src/Callbook.cs`   | Parses the SpreadsheetML callbook (ISO-8859-2). |
| `src/Gazetteer.cs`  | Offline GeoNames lookup; diacritic-insensitive name matching. |
| `src/MapData.cs`    | Joins callbook to gazetteer, aggregates per-town counts. |
| `src/Program.cs`    | Minimal ASP.NET host: loads data at startup, serves JSON + static site. |
| `src/wwwroot/`      | Leaflet/OpenStreetMap front-end. |
| `src/data/`         | Bundled callbook + GeoNames snapshot. |

## Data sources & licensing

- Call sign book: **NMHH** (Hungarian National Media and Infocommunications Authority).
- Coordinates: **GeoNames**, licensed **CC-BY 4.0**.
