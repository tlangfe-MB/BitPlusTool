using System.IO;
using BitPlusTool.Models;

namespace BitPlusTool.Services;

/// <summary>Laedt die Baumuster-/Motoren-Uebersicht (Codebedingung -&gt; Verkaufsbezeichnung, Leistung,
/// Motor-Konzept usw.) aus der eingebetteten motordata.csv (Quelle: Baumusterübersicht Werk Bremen).</summary>
public static class MotorCatalog
{
    private static List<MotorEntry>? _entries;

    public static IReadOnlyList<MotorEntry> Entries => _entries ??= Load();

    /// <summary>Liefert alle Referenz-Motoren, deren Codebedingung komplett in den Tokens des uebergebenen
    /// Nr.-Blocks enthalten ist (Teilmenge) - nur dann gilt der "Zusammenhang" als sicher genug fuer eine
    /// automatische Zuordnung (statt bei jeder Ueberschneidung zu raten). Ist ein Treffer selbst nur eine
    /// Teilmenge eines ANDEREN, spezifischeren Treffers (z. B. "M654+M20" vs. "M654+M20+M013"), wird nur
    /// der genauere behalten - sonst wuerden z. B. bei M013 sowohl der C220D-Basiseintrag als auch der
    /// spezifischere C200D-Eintrag gleichzeitig auftauchen.</summary>
    public static List<MotorEntry> FindMatches(IEnumerable<string> blockTokens)
    {
        var set = new HashSet<string>(blockTokens.Select(Norm), StringComparer.OrdinalIgnoreCase);
        var candidates = Entries.Where(e => e.Tokens.Count > 0 && e.Tokens.IsSubsetOf(set)).ToList();
        return candidates.Where(c => !candidates.Any(other => !ReferenceEquals(other, c) && c.Tokens.IsProperSubsetOf(other.Tokens))).ToList();
    }

    private static string Norm(string t) => ExpressionParser.Tok(t);

    private static List<MotorEntry> Load()
    {
        var result = new List<MotorEntry>();
        string text;
        try { text = TemplateProvider.GetResourceText("motordata.csv"); }
        catch { return result; }

        using var reader = new StringReader(text);
        string? line = reader.ReadLine(); // Header ueberspringen (AA;Codebedingung;LK;BM-AA;Benennung;Entw.-Bez.;Verk.-Bez.;Antriebsart;KW;PS;Antrag;Motor-Konzept;Motor-Art;Zyl.-Anz.)
        while ((line = reader.ReadLine()) != null)
        {
            if (line.Length == 0) continue;
            var f = SplitCsvLine(line);
            if (f.Count < 14) continue;

            var codebedingung = f[1];
            if (codebedingung.Length == 0) continue;

            var tokens = codebedingung.TrimEnd(';').Split('+', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Select(Norm).Where(t => t.Length > 0).ToHashSet(StringComparer.OrdinalIgnoreCase);

            result.Add(new MotorEntry
            {
                Codebedingung = codebedingung,
                Tokens = tokens,
                Lk = f[2],
                BmAa = f[3],
                Benennung = f[4],
                EntwBez = f[5],
                VerkBez = f[6],
                Antriebsart = f[7],
                Kw = f[8],
                Ps = f[9],
                MotorKonzept = f[11],
                MotorArt = f[12],
                ZylAnzahl = f[13]
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
