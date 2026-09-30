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
        for (int i = 0; i < v.Rows.Count; i++)
        {
            var r = v.Rows[i];
            Console.WriteLine("    " + (i + 1) + ". " + r.Op + " " + string.Join(" / ", r.Tokens));
        }
    }
    Console.WriteLine();
}

Show("Klammer = lokales ODER, 1 Block (bestaetigt, wie SNR-Realdaten)", "(299+460/494)/(299);");
Show("3+ Alternativen ohne Klammern werden jetzt automatisch erkannt (3 Bloecke)", "(B01+M654/M177+M40+1U2/M256);");
Show("MIT eigener Klammer pro Motor: 3 Bloecke (B01+M654 / M177+M40+1U2 / M256)", "((B01+M654)/(M177+M40+1U2)/(M256));");
Show("Bare ohne Klammern (2 Bloecke erwartet, unveraendert)", "M256/M654+B01/M177+M40+1U2;");
Show("Klassisches 3-Block-Beispiel (Regressionstest)", "(M256+M30+M016+M005-M010)/(M139+M20+M014+M005+ME10)/(M177+M40+M014+M005+M010);");
Show("Real-XML-Fall SNR+R7I/R8L+Ausschluss: MUSS 1 Block bleiben (siehe SnrTypeCheck)", "(SNR>=93120230210T416+R7I/R8L+-PH6068);");

Show("Bug-Report Motor-Varianten OHNE innere Klammern (jetzt korrekt 2 Bloecke: A08+M254+1U2+(460/494/835) / A12+M254+1U2)",
    "(A08+M254+1U2+460/A08+M254+1U2+494/A08+M254+1U2+835/A12+M254+1U2)+-M256+-(A08+S43+460)+-(A08+S43+494)+-(A08+S43+835)+-ME10;");

Show("Bug-Report Motor-Varianten MIT inneren Klammern (4 Bloecke, so geloest)",
    "((A08+M254+1U2+460)/(A08+M254+1U2+494)/(A08+M254+1U2+835)/(A12+M254+1U2))+-M256+-(A08+S43+460)+-(A08+S43+494)+-(A08+S43+835)+-ME10;");



