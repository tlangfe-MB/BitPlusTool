using BitPlusTool.Services;

void Show(string label, string code)
{
    Console.WriteLine("=== " + label + " ===");
    Console.WriteLine("Eingabe: " + code);
    var result = ExpressionParser.Parse(code);
    Console.WriteLine("Bloecke: " + result.Variants.Count);
    foreach (var v in result.Variants)
    {
        Console.WriteLine("  Nr. " + v.Lfd);
        foreach (var r in v.Rows)
        {
            var vals = r.Tokens.Select(t => ExpressionParser.ValueOf(t, ExpressionParser.TypeOf(t))).ToList();
            Console.WriteLine("    Op=" + r.Op + " Typ=" + ExpressionParser.TypeOf(r.Tokens[0]) + " Werte=" + string.Join(" / ", vals));
        }
    }
    // direkt aus Freitext, ohne XML-Import, ein XML erzeugen -> zeigt ob SNR im normalen Ablauf ankommt
    var xml = XmlTemplateBuilder.Build(result.Variants, "TH9999", code);
    var snrLine = xml.Split('\n').SkipWhile(l => !l.Contains("Kriterientyp>SNR")).Take(1).FirstOrDefault();
    Console.WriteLine("XML enthaelt SNR-Zeile: " + (xml.Contains("Kriterientyp>SNR") ? "JA" : "NEIN"));
    Console.WriteLine();
}

// Ganz normal getippt, kein Import beteiligt:
Show("Direkt getippt, bare", "SNR<93120230210T416+M256+1U2;");
Show("Direkt getippt, mit Klammer-Block + ODER", "(SNR>=93120230210T416+R7I/R8L+-PH6068);");
