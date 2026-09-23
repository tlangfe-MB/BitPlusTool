using System.Xml.Linq;
using BitPlusTool.Models;

namespace BitPlusTool.Services;

/// <summary>Liest eine PLUS-XML-Datei (TmeAusdruckTabelleImport) zurueck in Bloecke/Zeilen (Umkehrung von XmlTemplateBuilder).</summary>
public static class XmlImporter
{
    public record ImportResult(List<PlusVariant> Variants, string? TechnischesMerkmal);

    public static ImportResult Parse(string xmlText)
    {
        XDocument doc;
        try { doc = XDocument.Parse(xmlText); }
        catch (Exception ex) { throw new InvalidOperationException("XML konnte nicht gelesen werden: " + ex.Message, ex); }

        var dataTables = doc.Descendants("DataTable").ToList();
        var exprDT = dataTables.FirstOrDefault(dt => DescendantsLocal(dt, "TmeAusdruckTabelleImport").Any())
            ?? throw new InvalidOperationException("Keine Ausdruck-Tabelle (TmeAusdruckTabelleImport) in dieser XML gefunden.");
        var headerDT = dataTables.FirstOrDefault(dt => DescendantsLocal(dt, "TmeHeaderDataImport").Any());

        string? tm = headerDT is null
            ? null
            : DescendantsLocal(headerDT, "TmeHeaderDataImport").FirstOrDefault() is { } h
                ? GetStr(h, "TechnischesMerkmal")
                : null;

        var records = DescendantsLocal(exprDT, "TmeAusdruckTabelleImport")
            .Select(e => new
            {
                LfdNr = GetInt(e, "LfdNr"),
                ZeilenNr = GetInt(e, "ZeilenNr"),
                SpaltenNr = GetInt(e, "SpaltenNr"),
                Kriterientyp = GetStr(e, "Kriterientyp"),
                Operator = GetStr(e, "Operator"),
                Wert = GetStr(e, "WertS08S71RD")
            })
            .OrderBy(r => r.LfdNr).ThenBy(r => r.ZeilenNr).ThenBy(r => r.SpaltenNr)
            .ToList();

        if (records.Count == 0)
            throw new InvalidOperationException("Die Ausdruck-Tabelle enthaelt keine Zeilen.");

        var variants = new List<PlusVariant>();
        foreach (var lfdGroup in records.GroupBy(r => r.LfdNr).OrderBy(g => g.Key))
        {
            var rows = new List<PlusRow>();
            foreach (var zeileGroup in lfdGroup.GroupBy(r => r.ZeilenNr).OrderBy(g => g.Key))
            {
                var kriterientyp = zeileGroup.First().Kriterientyp;
                var isSnr = kriterientyp.Equals("SNR", StringComparison.OrdinalIgnoreCase);
                // SNR-Werte werden nicht mit '*' aufgefuellt und behalten das "SNR"-Praefix,
                // damit ExpressionParser.TypeOf() sie beim Anzeigen/Exportieren wiedererkennt.
                // EK (Empfaenger-Kennzeichen, steuert interne Verteilung, kein Ausstattungscode),
                // BR (Baureihe als Zeilenkriterium) und BM (Baumuster) bekommen aus demselben Grund
                // ihr Praefix.
                var tokens = zeileGroup.OrderBy(r => r.SpaltenNr)
                    .Select(r => isSnr ? "SNR" + r.Wert
                        : kriterientyp.Equals("EK", StringComparison.OrdinalIgnoreCase) ? "EK" + r.Wert.TrimEnd('*')
                        : kriterientyp.Equals("BR", StringComparison.OrdinalIgnoreCase) ? "BR" + r.Wert.TrimEnd('*')
                        : kriterientyp.Equals("BM", StringComparison.OrdinalIgnoreCase) ? "BM" + r.Wert.TrimEnd('*')
                        : r.Wert.TrimEnd('*'))
                    .ToList();
                var op = isSnr ? zeileGroup.First().Operator : (zeileGroup.First().Operator == "<>" ? "<>" : "=");
                rows.Add(new PlusRow { Op = op, Tokens = tokens });
            }
            variants.Add(new PlusVariant { Lfd = lfdGroup.Key, Rows = rows });
        }

        return new ImportResult(variants, string.IsNullOrWhiteSpace(tm) ? null : tm);
    }

    private static int GetInt(XElement e, string name) => int.TryParse(GetStr(e, name), out var v) ? v : 0;

    private static string GetStr(XElement e, string name)
        => e.Elements().FirstOrDefault(c => c.Name.LocalName == name)?.Value ?? "";

    private static IEnumerable<XElement> DescendantsLocal(XContainer root, string localName)
        => root.Descendants().Where(x => x.Name.LocalName == localName);
}
