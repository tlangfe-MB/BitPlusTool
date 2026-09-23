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
