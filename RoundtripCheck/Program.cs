using BitPlusTool.Services;

var xmlPath = args.Length > 0 ? args[0] : Path.Combine(Path.GetTempPath(), "roundtrip_test.xml");
var xmlText = File.ReadAllText(xmlPath);

var imported = XmlImporter.Parse(xmlText);
Console.WriteLine("Importiertes TME: " + imported.TechnischesMerkmal);
Console.WriteLine("Bloecke: " + imported.Variants.Count);

var code = ExpressionParser.ToCodeString(imported.Variants);
Console.WriteLine("Rekonstruierte Codebedingung:");
Console.WriteLine(code);

var reparsed = ExpressionParser.Parse(code);
Console.WriteLine("Erneut geparst -> Bloecke: " + reparsed.Variants.Count);

var tm = imported.TechnischesMerkmal ?? "TH0000";
var rebuiltXml = XmlTemplateBuilder.Build(reparsed.Variants, tm, code);

string[] ExtractRows(string xml)
{
    var doc = System.Xml.Linq.XDocument.Parse(xml);
    return doc.Descendants().Where(e => e.Name.LocalName == "TmeAusdruckTabelleImport")
        .Select(e => string.Join("|",
            e.Elements().FirstOrDefault(c => c.Name.LocalName == "LfdNr")?.Value,
            e.Elements().FirstOrDefault(c => c.Name.LocalName == "ZeilenNr")?.Value,
            e.Elements().FirstOrDefault(c => c.Name.LocalName == "SpaltenNr")?.Value,
            e.Elements().FirstOrDefault(c => c.Name.LocalName == "Operator")?.Value,
            e.Elements().FirstOrDefault(c => c.Name.LocalName == "WertS08S71RD")?.Value))
        .ToArray();
}

var a = ExtractRows(xmlText);
var b = ExtractRows(rebuiltXml);

Console.WriteLine();
Console.WriteLine("Original-Zeilen (" + a.Length + "):");
foreach (var r in a) Console.WriteLine("  " + r);
Console.WriteLine("Neu erzeugte Zeilen (" + b.Length + "):");
foreach (var r in b) Console.WriteLine("  " + r);

Console.WriteLine();
Console.WriteLine(a.SequenceEqual(b) ? "RUNDLAUF OK: identisch" : "RUNDLAUF ABWEICHUNG!");
