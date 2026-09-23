using BitPlusTool.Services;

void Check(string code, bool expected)
{
    var actual = ExpressionParser.IsAmbiguousTmeCandidate(code);
    var ok = actual == expected ? "OK " : "FAIL";
    Console.WriteLine($"{ok} IsAmbiguousTmeCandidate({code}) = {actual} (erwartet {expected})");
}

Check("PH6068", true);
Check("TH1255", false);
Check("TH0588", false);
Check("R7I", false);
Check("M256", false);
Check("ME10", false);
Check("B01", false);
Check("1U2", false);

Console.WriteLine();
Console.WriteLine("Vorher TypeOf(PH6068) = " + ExpressionParser.TypeOf("PH6068"));
ExpressionParser.SetTmeOverride("PH6068", true);
Console.WriteLine("Nach Override(true) TypeOf(PH6068) = " + ExpressionParser.TypeOf("PH6068"));
Console.WriteLine("ValueOf(PH6068, Tme) = " + ExpressionParser.ValueOf("PH6068", ExpressionParser.TypeOf("PH6068")));
Console.WriteLine("Ist danach noch ambiguous? " + ExpressionParser.IsAmbiguousTmeCandidate("PH6068") + " (erwartet false, da schon beantwortet)");

ExpressionParser.ClearTmeOverrides();
ExpressionParser.SetTmeOverride("XY1234", false);
Console.WriteLine("Override(false) TypeOf(XY1234) = " + ExpressionParser.TypeOf("XY1234") + " (erwartet Pc)");
