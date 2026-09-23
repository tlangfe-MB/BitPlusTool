using System.Text;
using System.IO;
using System.Xml;
using System.Xml.Linq;
using BitPlusTool.Models;

namespace BitPlusTool.Services;

/// <summary>Laedt eingebettete Ressourcen (PLUS-Vorlage, Codeliste) aus der Assembly.</summary>
public static class TemplateProvider
{
    public static string GetTemplateText() => GetResourceText("template.xml");

    public static string GetResourceText(string fileNameSuffix)
    {
        var asm = typeof(TemplateProvider).Assembly;
        var resourceName = asm.GetManifestResourceNames()
            .FirstOrDefault(n => n.EndsWith(fileNameSuffix, StringComparison.OrdinalIgnoreCase))
            ?? throw new InvalidOperationException($"Eingebettete Ressource '{fileNameSuffix}' wurde nicht gefunden.");
        using var stream = asm.GetManifestResourceStream(resourceName)!;
        using var reader = new StreamReader(stream, Encoding.UTF8);
        return reader.ReadToEnd();
    }
}

/// <summary>Befuellt die interne PLUS-XML-Vorlage mit TME, Beschreibung und Ausdruckszeilen (Port aus buildTemplateXml).</summary>
public static class XmlTemplateBuilder
{
    private static readonly XNamespace Ns = "http://tempuri.org/BitVerwaltungTMEDataSet.xsd";
    private static readonly XNamespace DiffGr = "urn:schemas-microsoft-com:xml-diffgram-v1";
    private static readonly XNamespace MsData = "urn:schemas-microsoft-com:xml-msdata";
    private static readonly XNamespace Xs = "http://www.w3.org/2001/XMLSchema";

    public static string Build(List<PlusVariant> variants, string technischesMerkmal, string beschreibung)
    {
        if (string.IsNullOrWhiteSpace(technischesMerkmal))
            throw new InvalidOperationException("Bitte zuerst Technisches Merkmal/TME eingeben, z. B. TH1144.");

        var tm = technischesMerkmal.Trim().ToUpperInvariant();
        var desc = (beschreibung ?? "").Trim();
        if (desc.Length > 50) desc = desc[..50];

        XDocument doc;
        try { doc = XDocument.Parse(TemplateProvider.GetTemplateText()); }
        catch (Exception ex) { throw new InvalidOperationException("Interne Vorlage konnte nicht gelesen werden.", ex); }

        var dataTables = doc.Descendants("DataTable").ToList();
        var headerDT = dataTables.FirstOrDefault(IsHeaderTable)
            ?? throw new InvalidOperationException("Interne Vorlage enthält keinen Header-DataTable.");
        var exprDT = dataTables.FirstOrDefault(IsAusdruckTable)
            ?? throw new InvalidOperationException("Interne Vorlage enthält keinen Ausdruck-DataTable.");

        foreach (var h in DescendantsLocal(headerDT, "TmeHeaderDataImport"))
        {
            SetText(h, "TechnischesMerkmal", tm);
            SetText(h, "TechnischesMerkmalAlt", tm);
            SetText(h, "Beschreibung", desc);
            SetText(h, "Kommentar", desc);
        }

        var ds = DescendantsLocal(exprDT, "BitVerwaltungTMEDataSet").FirstOrDefault()
            ?? throw new InvalidOperationException("Interne Vorlage: Ausdruck-Dataset fehlt.");

        DescendantsLocal(ds, "TmeAusdruckTabelleImport").ToList().ForEach(e => e.Remove());

        int id = 1, rowOrder = 0;
        foreach (var v in variants)
        {
            for (int ri = 0; ri < v.Rows.Count; ri++)
            {
                var r = v.Rows[ri];
                for (int si = 0; si < r.Tokens.Count; si++)
                {
                    var t = r.Tokens[si];
                    var typ = ExpressionParser.TypeOf(t);
                    var val = ExpressionParser.ValueOf(t, typ);
                    var rec = new XElement(Ns + "TmeAusdruckTabelleImport",
                        new XAttribute(DiffGr + "id", $"TmeAusdruckTabelleImport{id}"),
                        new XAttribute(MsData + "rowOrder", rowOrder),
                        new XElement(Ns + "LfdNr", v.Lfd),
                        new XElement(Ns + "ZeilenNr", ri + 1),
                        new XElement(Ns + "Kriterientyp", TypeCode(typ)),
                        new XElement(Ns + "SpaltenNr", si + 1),
                        new XElement(Ns + "Operator", r.Op),
                        new XElement(Ns + "WertS08S71RD", val),
                        new XElement(Ns + "WertS08S81RD", ""),
                        new XElement(Ns + "LaengeWert", val.Length));
                    ds.Add(rec);
                    id++;
                    rowOrder++;
                }
            }
        }

        using var ms = new MemoryStream();
        var settings = new XmlWriterSettings { Indent = true, Encoding = new UTF8Encoding(false) };
        using (var writer = XmlWriter.Create(ms, settings)) doc.Save(writer);
        return Encoding.UTF8.GetString(ms.ToArray());
    }

    private static string TypeCode(TokenType t) => t switch
    {
        TokenType.Tme => "TME",
        TokenType.Pcv => "PCV",
        TokenType.Snr => "SNR",
        TokenType.Ek => "EK",
        TokenType.Br => "BR",
        _ => "PC",
    };

    private static IEnumerable<XElement> DescendantsLocal(XContainer root, string localName)
        => root.Descendants().Where(e => e.Name.LocalName == localName);

    private static bool IsHeaderTable(XElement dataTable)
        => MatchesMainDataTable(dataTable, "TmeHeaderDataImport") || DescendantsLocal(dataTable, "TmeHeaderDataImport").Any();

    private static bool IsAusdruckTable(XElement dataTable)
        => MatchesMainDataTable(dataTable, "TmeAusdruckTabelleImport") || DescendantsLocal(dataTable, "TmeAusdruckTabelleImport").Any();

    private static bool MatchesMainDataTable(XElement dataTable, string name)
    {
        foreach (var schema in dataTable.Descendants(Xs + "schema"))
        {
            var val = (string?)schema.Attribute(MsData + "MainDataTable") ?? "";
            if (val.Contains(name, StringComparison.Ordinal)) return true;
        }
        return false;
    }

    private static void SetText(XElement parent, string childLocalName, string value)
    {
        var child = parent.Descendants().FirstOrDefault(e => e.Name.LocalName == childLocalName);
        if (child != null) child.Value = value;
    }
}
