using System.Text.Json;
using System.Text.RegularExpressions;

var builder = WebApplication.CreateBuilder(args);
var app = builder.Build();

// Serve the static frontend from wwwroot/index.html
app.UseDefaultFiles();
app.UseStaticFiles();


var dataDir = Environment.GetEnvironmentVariable("DATA_DIR")
              ?? app.Environment.ContentRootPath;
Directory.CreateDirectory(dataDir);
var dataFile = Path.Combine(dataDir, "data.json");

var saveLock = new object();

// initialize store (load from file if exists)
var store = new Dictionary<string, object>(StringComparer.Ordinal);
if (File.Exists(dataFile))
{
    try
    {
        var json = File.ReadAllText(dataFile);
        var loaded = JsonSerializer.Deserialize<Dictionary<string, object>>(json);
        if (loaded is not null)
        {
            foreach (var kv in loaded) store[kv.Key] = kv.Value;
        }
    }
    catch { /* ignore corrupt file; start empty */ }
}

void Save()
{
    lock (saveLock)
    {
        var json = JsonSerializer.Serialize(store, new JsonSerializerOptions { WriteIndented = true });
        File.WriteAllText(dataFile, json);
    }
}

// ===== helpers =====
const string Alphabet = "abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789";
string GenerateCode(int len = 6)
{
    var rnd = Random.Shared;
    string code;
    do
    {
        code = new string(Enumerable.Range(0, len)
            .Select(_ => Alphabet[rnd.Next(Alphabet.Length)]).ToArray());
    } while (store.ContainsKey(code));
    return code;
}

bool IsValidAbsoluteHttpUrl(string? input)
{
    if (string.IsNullOrWhiteSpace(input)) return false;
    if (!Uri.TryCreate(input, UriKind.Absolute, out var uri)) return false;
    return uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps;
}

var codeRegex = new Regex("^[A-Za-z0-9_-]{4,20}$", RegexOptions.Compiled);

// ===== endpoints =====

// POST /shorten  { url, code? } -> { code, shortUrl, url }
app.MapPost("/shorten", async (HttpRequest req) =>
{
    using var reader = new StreamReader(req.Body);
    var body = await reader.ReadToEndAsync();
    using var doc = JsonDocument.Parse(body);
    var root = doc.RootElement;

    var url = root.TryGetProperty("url", out var uProp) ? uProp.GetString() : null;
    var custom = root.TryGetProperty("code", out var cProp) ? cProp.GetString() : null;

    if (!IsValidAbsoluteHttpUrl(url))
        return Results.BadRequest(new { error = "Invalid URL. Use absolute http(s) URL." });

    var code = string.IsNullOrWhiteSpace(custom) ? GenerateCode() : custom!.Trim();

    if (!codeRegex.IsMatch(code))
        return Results.BadRequest(new { error = "Invalid code. Use 4-20 chars: letters, digits, _ or -." });

    if (store.ContainsKey(code))
        return Results.Conflict(new { error = "Code already exists. Try another." });

    var link = new { Url = url!, CreatedAt = DateTime.UtcNow, Hits = 0 };
    store[code] = link;
    Save();

    var baseUrl = $"{req.Scheme}://{req.Host}";
    return Results.Ok(new { code, shortUrl = $"{baseUrl}/{code}", url });
});

// GET /{code}  -> 302 to original URL (increments hits)
app.MapGet("/{code}", (string code) =>
{
    if (!codeRegex.IsMatch(code)) return Results.NotFound();
    if (!store.TryGetValue(code, out var linkObj)) return Results.NotFound();
    
    // Handle the link object
    var linkJson = JsonSerializer.Serialize(linkObj);
    var link = JsonSerializer.Deserialize<JsonElement>(linkJson);
    
    if (link.TryGetProperty("Url", out var urlProp))
    {
        var url = urlProp.GetString();
        if (!string.IsNullOrEmpty(url))
        {
            // Increment hits
            var hits = link.TryGetProperty("Hits", out var hitsProp) ? hitsProp.GetInt32() : 0;
            var updatedLink = new { Url = url, CreatedAt = DateTime.UtcNow, Hits = hits + 1 };
            store[code] = updatedLink;
            Save();
            return Results.Redirect(url);
        }
    }
    
    return Results.NotFound();
});

// (Optional) list for demo/debug UI
app.MapGet("/api/links", () =>
{
    var list = store.Select(kv => {
        var linkJson = JsonSerializer.Serialize(kv.Value);
        var link = JsonSerializer.Deserialize<JsonElement>(linkJson);
        
        var url = link.TryGetProperty("Url", out var urlProp) ? urlProp.GetString() : "";
        var createdAt = link.TryGetProperty("CreatedAt", out var dateProp) ? dateProp.GetDateTime() : DateTime.UtcNow;
        var hits = link.TryGetProperty("Hits", out var hitsProp) ? hitsProp.GetInt32() : 0;
        
        return new { code = kv.Key, url = url, createdAt = createdAt, hits = hits };
    }).OrderByDescending(x => x.createdAt);
    
    return Results.Ok(list);
});

app.Run();

