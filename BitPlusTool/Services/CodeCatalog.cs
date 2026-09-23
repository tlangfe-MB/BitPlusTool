using System.IO;
using BitPlusTool.Models;

namespace BitPlusTool.Services;

/// <summary>Laedt und cached die Codeliste (Code -&gt; Bezeichnung + gueltige Baureihen) aus der eingebetteten CSV.</summary>
public static class CodeCatalog
{
    private static Dictionary<string, CodeEntry>? _entries;

    public static IReadOnlyDictionary<string, CodeEntry> Entries => _entries ??= Load();

    public static CodeEntry? TryGet(string code)
        => Entries.TryGetValue(code.Trim().ToUpperInvariant(), out var e) ? e : null;

    private static Dictionary<string, CodeEntry> Load()
    {
        var result = new Dictionary<string, CodeEntry>(StringComparer.OrdinalIgnoreCase);
        string text;
        try { text = TemplateProvider.GetResourceText("codedata.csv"); }
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
