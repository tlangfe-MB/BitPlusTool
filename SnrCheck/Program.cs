using BitPlusTool.Services;

var xmlPath = @"c:\Users\tlangfe\Downloads\TMETH0489PNR0.xml";
var xmlText = File.ReadAllText(xmlPath);

var imported = XmlImporter.Parse(xmlText);
Console.WriteLine("TME: " + imported.TechnischesMerkmal);
Console.WriteLine("Bloecke: " + imported.Variants.Count);
foreach (var v in imported.Variants)
{
    Console.WriteLine("  Nr. " + v.Lfd);
    foreach (var r in v.Rows) Console.WriteLine("    " + r.Op + " " + string.Join(" / ", r.Tokens));
}

var code = ExpressionParser.ToCodeString(imported.Variants);
Console.WriteLine();
Console.WriteLine("Rekonstruiert:");
Console.WriteLine(code);

var reparsed = ExpressionParser.Parse(code);
Console.WriteLine("Erneut geparst -> Bloecke: " + reparsed.Variants.Count);

var tm = imported.TechnischesMerkmal ?? "TH0000";
var rebuiltXml = XmlTemplateBuilder.Build(reparsed.Variants, tm, code);

string[] ExtractRows(string xml) => System.Xml.Linq.XDocument.Parse(xml)
    .Descendants().Where(e => e.Name.LocalName == "TmeAusdruckTabelleImport")
    .Select(e => string.Join("|",
        e.Elements().FirstOrDefault(c => c.Name.LocalName == "LfdNr")?.Value,
        e.Elements().FirstOrDefault(c => c.Name.LocalName == "ZeilenNr")?.Value,
        e.Elements().FirstOrDefault(c => c.Name.LocalName == "SpaltenNr")?.Value,
        e.Elements().FirstOrDefault(c => c.Name.LocalName == "Kriterientyp")?.Value,
        e.Elements().FirstOrDefault(c => c.Name.LocalName == "Operator")?.Value,
        e.Elements().FirstOrDefault(c => c.Name.LocalName == "WertS08S71RD")?.Value,
        e.Elements().FirstOrDefault(c => c.Name.LocalName == "LaengeWert")?.Value))
    .ToArray();

var a = ExtractRows(xmlText);
var b = ExtractRows(rebuiltXml);

Console.WriteLine();
Console.WriteLine("Original (" + a.Length + "):");
foreach (var r in a) Console.WriteLine("  " + r);
Console.WriteLine("Neu (" + b.Length + "):");
foreach (var r in b) Console.WriteLine("  " + r);

Console.WriteLine();
Console.WriteLine(a.SequenceEqual(b) ? "RUNDLAUF OK: identisch" : "RUNDLAUF ABWEICHUNG!");
