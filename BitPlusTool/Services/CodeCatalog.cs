using System.IO;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using BitPlusTool.Models;

namespace BitPlusTool.Services;

/// <summary>Laedt und cached die Codeliste (Code -&gt; Bezeichnung + gueltige Baureihen). Quelle ist,
/// sobald einmal erfolgreich abgerufen, ein lokaler Cache aus der Live-Codetabelle Werk 067; sonst
/// die eingebettete CSV (Stand zum Zeitpunkt des Builds) als Fallback.</summary>
public static class CodeCatalog
{
    private const string SourceUrl = "https://log-idv-067.app.corpintra.net/Codetabellen/Test.js";
    private const int MaxCacheAgeDays = 30;
    private static readonly TimeSpan FetchTimeout = TimeSpan.FromSeconds(5);
    private static readonly string CacheDir = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "BitPlusTool");
    private static readonly string CacheFile = Path.Combine(CacheDir, "codedata_cache.csv");

    private static Dictionary<string, CodeEntry>? _entries;

    public static IReadOnlyDictionary<string, CodeEntry> Entries => _entries ??= Load();

    public static CodeEntry? TryGet(string code)
        => Entries.TryGetValue(code.Trim().ToUpperInvariant(), out var e) ? e : null;

    /// <summary>
    /// Beim Programmstart aufrufen (fire-and-forget, blockiert die UI nicht): aktualisiert den lokalen
    /// Codeliste-Cache aus der Live-Codetabelle Werk 067, wenn er fehlt oder aelter als 30 Tage ist -
    /// mit kurzem Timeout. Bei jedem Fehler (kein Netz, kein Zugriff, Format geaendert) wird still
    /// beim vorhandenen Cache bzw. der eingebetteten Liste geblieben - nie ein harter Fehler nach aussen.
    /// </summary>
    public static async Task RefreshIfStaleAsync()
    {
        try
        {
            if (File.Exists(CacheFile) && (DateTime.Now - File.GetLastWriteTime(CacheFile)).TotalDays < MaxCacheAgeDays)
                return;

            using var http = new HttpClient(new HttpClientHandler { UseDefaultCredentials = true }) { Timeout = FetchTimeout };
            var js = await http.GetStringAsync(SourceUrl).ConfigureAwait(false);
            var csv = ConvertLiveDataToCsv(js);
            if (csv is null) return;

            Directory.CreateDirectory(CacheDir);
            await File.WriteAllTextAsync(CacheFile, csv, new UTF8Encoding(false)).ConfigureAwait(false);
            _entries = null; // naechster Zugriff laedt den frischen Cache neu
        }
        catch
        {
            // Offline, kein Zugriff aufs Firmennetz oder Format der Live-Tabelle geaendert:
            // stillschweigend beim bisherigen Stand bleiben.
        }
    }

    /// <summary>Die Live-Seite liefert die Tabelle als "var tableData = [ ... ];" in Test.js (dieselben
    /// Spalten wie im Download-CSV-Button der Seite) - wird hier ins interne CSV-Format umgewandelt.</summary>
    private static string? ConvertLiveDataToCsv(string js)
    {
        const string marker = "var tableData = ";
        var start = js.IndexOf(marker, StringComparison.Ordinal);
        if (start < 0) return null;
        start += marker.Length;
        var end = js.IndexOf("\nvar ", start, StringComparison.Ordinal);
        var json = (end < 0 ? js[start..] : js[start..end]).Trim().TrimEnd(';');

        var rows = JsonSerializer.Deserialize<List<LiveCodeRow>>(json);
        if (rows is null || rows.Count == 0) return null;

        var sb = new StringBuilder();
        sb.Append("\"CodeArt\";\"CodeVariante\";\"Code\";\"Bezeichnung\";\"Baureihe\";\"Ausfuehrungen\";\"Teilewirksam\";\"BisGueltigkeit\"\r\n");
        foreach (var r in rows)
        {
            sb.Append(CsvField(r.CodeArt)).Append(';').Append(CsvField(r.CodeVariante)).Append(';').Append(CsvField(r.Code)).Append(';')
              .Append(CsvField(r.CodeBennenung)).Append(';').Append(CsvField(r.BR)).Append(';').Append(CsvField(r.Ausfuehrungen)).Append(';')
              .Append(CsvField(r.V)).Append(';').Append(CsvField(r.GueltigBis)).Append("\r\n");
        }
        return sb.ToString();
    }

    private static string CsvField(string? s) => "\"" + (s ?? "").Replace("\"", "\"\"") + "\"";

    private sealed class LiveCodeRow
    {
        public string? CodeArt { get; set; }
        public string? CodeVariante { get; set; }
        public string? Code { get; set; }
        public string? CodeBennenung { get; set; }
        public string? BR { get; set; }
        public string? Ausfuehrungen { get; set; }
        public string? GueltigBis { get; set; }
        public string? V { get; set; }
    }

    private static Dictionary<string, CodeEntry> Load()
    {
        var result = new Dictionary<string, CodeEntry>(StringComparer.OrdinalIgnoreCase);
        string text;
        try { text = File.Exists(CacheFile) ? File.ReadAllText(CacheFile) : TemplateProvider.GetResourceText("codedata.csv"); }
        catch { return result; }

        using var reader = new StringReader(text);
        string? line = reader.ReadLine(); // Header ueberspringen
        while ((line = reader.ReadLine()) != null)
        {
            if (line.Length == 0) continue;
            var fields = SplitCsvLine(line);
            if (fields.Count < 8) continue;

            var codeArt = fields[0];
            var code = fields[2].Trim().ToUpperInvariant();
            var bezeichnung = fields[3];
            var baureihe = fields[4];
            var ausfuehrungen = fields[5];
            var teilewirksam = fields[6];
            var bisGueltigkeit = fields[7];
            if (code.Length == 0) continue;

            if (!result.TryGetValue(code, out var entry))
            {
                entry = new CodeEntry { Code = code, CodeArt = codeArt, Bezeichnung = bezeichnung };
                result[code] = entry;
            }
            entry.Validities.Add(new CodeValidity
            {
                Baureihe = baureihe,
                Ausfuehrungen = ausfuehrungen,
                Teilewirksam = teilewirksam,
                BisGueltigkeit = bisGueltigkeit
            });
        }
        return result;
    }

    private static List<string> SplitCsvLine(string line)
    {
        var fields = new List<string>();
        var buf = new System.Text.StringBuilder();
        bool inQuotes = false;
        for (int i = 0; i < line.Length; i++)
        {
            var c = line[i];
            if (inQuotes)
            {
                if (c == '"')
                {
                    if (i + 1 < line.Length && line[i + 1] == '"') { buf.Append('"'); i++; }
                    else inQuotes = false;
                }
                else buf.Append(c);
            }
            else
            {
                if (c == '"') inQuotes = true;
                else if (c == ';') { fields.Add(buf.ToString()); buf.Clear(); }
                else buf.Append(c);
            }
        }
        fields.Add(buf.ToString());
        return fields;
    }
}
