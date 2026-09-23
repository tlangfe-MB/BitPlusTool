using BitPlusTool.Services;

var cacheFile = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "BitPlusTool", "codedata_cache.csv");
Console.WriteLine("Cache-Datei: " + cacheFile);
Console.WriteLine("Existiert vorher: " + File.Exists(cacheFile));

Console.WriteLine("Vorher: " + CodeCatalog.Entries.Count + " Codes (Quelle: eingebettet oder alter Cache)");

var sw = System.Diagnostics.Stopwatch.StartNew();
await CodeCatalog.RefreshIfStaleAsync();
sw.Stop();
Console.WriteLine($"RefreshIfStaleAsync dauerte: {sw.ElapsedMilliseconds} ms");

Console.WriteLine("Cache-Datei existiert jetzt: " + File.Exists(cacheFile));
if (File.Exists(cacheFile))
    Console.WriteLine("Cache zuletzt geschrieben: " + File.GetLastWriteTime(cacheFile));

Console.WriteLine("Nachher: " + CodeCatalog.Entries.Count + " Codes");
var m256 = CodeCatalog.TryGet("M256");
Console.WriteLine("M256 gefunden: " + (m256 is not null) + (m256 is not null ? " -> " + m256.Bezeichnung : ""));

// Zweiter Aufruf sollte sofort zurueckkommen (Cache frisch genug, kein neuer Netzzugriff).
sw.Restart();
await CodeCatalog.RefreshIfStaleAsync();
sw.Stop();
Console.WriteLine($"Zweiter Aufruf (sollte Cache nutzen, kein Netz): {sw.ElapsedMilliseconds} ms");

Console.WriteLine();
Console.WriteLine("=== MotorCatalog ===");
Console.WriteLine("Eintraege: " + MotorCatalog.Entries.Count);

void CheckMotor(string label, string[] tokens)
{
    var matches = MotorCatalog.FindMatches(tokens);
    Console.WriteLine($"{label} [{string.Join("+", tokens)}] -> {matches.Count} Treffer");
    foreach (var m in matches)
        Console.WriteLine($"    {m.VerkBez} | {m.MotorKonzept} {m.MotorArt}{m.ZylAnzahl} | {m.Kw} KW / {m.Ps} PS | {m.Antriebsart} | Codebedingung=\"{m.Codebedingung}\"");
}

CheckMotor("Exaktes Motor-Codepaket", new[] { "M654", "M20", "M013" });
CheckMotor("Codepaket + zusaetzliche Codes im Block (Teilmengen-Match)", new[] { "M654", "M20", "M013", "BR206", "460" });
CheckMotor("Nur Teil des Codepakets (sollte NICHT matchen)", new[] { "M654", "M20" });
CheckMotor("Unbekannte Kombination (sollte 0 Treffer geben)", new[] { "M999", "M111" });

