using hammap;

var builder = WebApplication.CreateBuilder(args);

// Listen on IPv4 only, port 6502. 0.0.0.0 binds every IPv4 interface and does
// NOT bind IPv6 (that would require an explicit [::] endpoint).
builder.WebHost.UseUrls("http://0.0.0.0:6502");

var app = builder.Build();

// Resolve bundled data files (copied next to the app at build time).
var dataDir = Path.Combine(AppContext.BaseDirectory, "data");
var callbookPath = Path.Combine(dataDir, "callbook.xml");
var gazetteerPath = Path.Combine(dataDir, "HU.txt");

// Load and aggregate once at startup; the dataset is static.
app.Logger.LogInformation("Loading gazetteer from {path}", gazetteerPath);
var gazetteer = Gazetteer.Load(gazetteerPath);
app.Logger.LogInformation("Gazetteer entries: {count}", gazetteer.Count);

app.Logger.LogInformation("Loading callbook from {path}", callbookPath);
var entries = Callbook.Load(callbookPath);
app.Logger.LogInformation("Callbook entries: {count}", entries.Count);

var mapData = Aggregator.Build(entries, gazetteer);
app.Logger.LogInformation(
    "Hungarian: {hu}, mapped: {mapped}, unresolved: {unresolved}, distinct cities: {cities}",
    mapData.HungarianEntries, mapData.Mapped, mapData.Unresolved, mapData.Cities.Count);

// Serve the aggregated distribution as JSON.
app.MapGet("/api/distribution", () => Results.Json(mapData));

// Serve the static map page and assets from wwwroot.
app.UseDefaultFiles();
app.UseStaticFiles();

app.Run();
