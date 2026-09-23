using System.Text.RegularExpressions;
using System.Xml.Linq;
using BitPlusTool.Models;

namespace BitPlusTool.Services;

/// <summary>Mehrfache Selbstkontrollen: prueft, ob die Codebedingung korrekt gelesen/uebersetzt wurde,
/// bevor eine XML-Datei erzeugt bzw. exportiert wird.</summary>
public static class Validator
{
    public static List<CheckResult> ValidateParsing(string rawInput, ParseResult result)
    {
        return new List<CheckResult>
        {
            CheckParenBalance(rawInput),
            CheckTokenSyntax(result),
            CheckTokenCoverage(rawInput, result),
            CheckDuplicateVariants(result),
            CheckValueLengths(result),
        };
    }

    public static CheckResult CheckParenBalance(string rawInput)
    {
        var cleaned = (rawInput ?? "").Replace("\r", "");
        int open = cleaned.Count(c => c == '(');
        int close = cleaned.Count(c => c == ')');
        if (open == close)
            return new CheckResult { Title = "Klammern balanciert", Severity = CheckSeverity.Ok, Detail = $"{open} öffnende / {close} schließende Klammern." };

        var diff = Math.Abs(open - close);
        var kind = open > close ? "schließende" : "öffnende";
        return new CheckResult
        {
            Title = "Klammern automatisch ergänzt",
            Severity = CheckSeverity.Warning,
            Detail = $"Eingabe war unausgeglichen ({open} öffnende / {close} schließende). {diff} {kind} Klammer(n) wurden automatisch ergänzt – bitte Ausdruck prüfen."
        };
    }

    public static CheckResult CheckTokenSyntax(ParseResult result)
    {
        var bad = new List<string>();
        foreach (var v in result.Variants)
            foreach (var r in v.Rows)
                foreach (var t in r.Tokens)
                    if (!Regex.IsMatch(t, "^[A-Z0-9]+$") && !bad.Contains(t)) bad.Add(t);

        if (bad.Count == 0)
            return new CheckResult { Title = "Token-Syntax", Severity = CheckSeverity.Ok, Detail = "Alle erkannten Werte bestehen nur aus Buchstaben/Ziffern." };

        return new CheckResult
        {
            Title = "Token-Syntax auffällig",
            Severity = CheckSeverity.Error,
            Detail = "Ungewöhnliche Werte erkannt (evtl. Tippfehler oder Klammerfehler): " + string.Join(", ", bad)
        };
    }

    public static CheckResult CheckTokenCoverage(string rawInput, ParseResult result)
    {
        var normalized = ExpressionParser.BalanceInput(rawInput ?? "");
        // Nach Operatoren trennen (nicht nach '-', das kann Teil eines Werts wie "S-1234" sein);
        // ein evtl. fuehrendes Minus pro Stueck wird separat entfernt. Bei SNR-Werten wird der
        // eingebettete Vergleichsoperator entfernt, damit der Vergleich mit den gespeicherten
        // Tokens ("SNR"+Wert, ohne Operator) passt.
        var snrPrefix = new Regex(@"^SNR(<=|>=|<|>)", RegexOptions.IgnoreCase);
        var rawTokens = Regex.Split(normalized, @"[+/()]+")
            .Select(s => Regex.Replace(s.Trim(), "^-+", "").ToUpperInvariant())
            .Where(s => s.Length > 0)
            .Select(s => snrPrefix.Replace(s, "SNR"))
            .ToHashSet();
        var usedTokens = result.Variants.SelectMany(v => v.Rows).SelectMany(r => r.Tokens).ToHashSet();
        var missing = rawTokens.Where(t => !usedTokens.Contains(t)).ToList();

        if (missing.Count == 0)
            return new CheckResult
            {
                Title = "Rückübersetzung vollständig",
                Severity = CheckSeverity.Ok,
                Detail = $"Alle {rawTokens.Count} in der Eingabe gefundenen Werte tauchen im Ergebnis auf."
            };

        return new CheckResult
        {
            Title = "Mögliche fehlende Werte",
            Severity = CheckSeverity.Warning,
            Detail = "Diese Werte aus deiner Eingabe erscheinen in keiner PLUS-Zeile: " + string.Join(", ", missing) + ". Bitte Ausdruck und Klammersetzung prüfen."
        };
    }

    public static CheckResult CheckDuplicateVariants(ParseResult result)
    {
        var seen = new Dictionary<string, int>();
        var dups = new List<(int first, int dup)>();
        for (int i = 0; i < result.Variants.Count; i++)
        {
            var key = string.Join("|", result.Variants[i].Rows.Select(r => r.Op + ":" + string.Join(",", r.Tokens)));
            if (seen.TryGetValue(key, out var firstIdx)) dups.Add((firstIdx + 1, i + 1));
            else seen[key] = i;
        }

        if (dups.Count == 0)
            return new CheckResult { Title = "Keine doppelten Nr.-Blöcke", Severity = CheckSeverity.Ok, Detail = $"{result.Variants.Count} Nr.-Block(e) geprüft, alle unterschiedlich." };

        return new CheckResult
        {
            Title = "Doppelte Nr.-Blöcke gefunden",
            Severity = CheckSeverity.Warning,
            Detail = string.Join("; ", dups.Select(d => $"Nr. {d.first} = Nr. {d.dup}"))
        };
    }

    public static CheckResult CheckValueLengths(ParseResult result)
    {
        var longTokens = new List<string>();
        foreach (var v in result.Variants)
            foreach (var r in v.Rows)
                foreach (var t in r.Tokens)
                {
                    var typ = ExpressionParser.TypeOf(t);
                    var tok = ExpressionParser.Tok(t);
                    if (typ == TokenType.Pcv && tok.Length > 6 && !longTokens.Contains(tok)) longTokens.Add(tok);
                }

        if (longTokens.Count == 0)
            return new CheckResult { Title = "Wertlängen ok", Severity = CheckSeverity.Ok, Detail = "Alle PCV-Werte passen in das 6-stellige Format." };

        return new CheckResult
        {
            Title = "PCV-Wert länger als 6 Zeichen",
            Severity = CheckSeverity.Warning,
            Detail = "Diese Werte sind länger als das übliche 6-Zeichen-Format und werden nicht gekürzt: " + string.Join(", ", longTokens)
        };
    }

    public static CheckResult CheckTechnischesMerkmal(string tm)
    {
        if (string.IsNullOrWhiteSpace(tm))
            return new CheckResult { Title = "Technisches Merkmal fehlt", Severity = CheckSeverity.Error, Detail = "Bitte ein TME eingeben, z. B. TH1144." };

        var t = tm.Trim().ToUpperInvariant();
        if (t.Length > 6)
            return new CheckResult { Title = "Technisches Merkmal zu lang", Severity = CheckSeverity.Error, Detail = $"'{t}' hat {t.Length} Zeichen, das Schema erlaubt maximal 6." };
        if (!Regex.IsMatch(t, "^[A-Z0-9]+$"))
            return new CheckResult { Title = "Technisches Merkmal ungültig", Severity = CheckSeverity.Error, Detail = "Nur Buchstaben und Ziffern erlaubt." };

        return new CheckResult { Title = "Technisches Merkmal ok", Severity = CheckSeverity.Ok, Detail = $"'{t}' ({t.Length}/6 Zeichen)." };
    }

    public static CheckResult CheckXmlConsistency(string xml, ParseResult result)
    {
        try
        {
            var doc = XDocument.Parse(xml);
            var expected = result.Variants.SelectMany(v => v.Rows).SelectMany(r => r.Tokens).Count();
            var actual = doc.Descendants().Count(e => e.Name.LocalName == "TmeAusdruckTabelleImport");
            if (expected == actual)
                return new CheckResult { Title = "XML-Zeilenanzahl korrekt", Severity = CheckSeverity.Ok, Detail = $"{actual} Ausdruckszeilen im XML, {expected} erwartet." };

            return new CheckResult
            {
                Title = "XML-Zeilenanzahl weicht ab",
                Severity = CheckSeverity.Error,
                Detail = $"{actual} Ausdruckszeilen im XML, aber {expected} erwartet. Bitte XML nicht verwenden und Eingabe prüfen!"
            };
        }
        catch (Exception ex)
        {
            return new CheckResult { Title = "XML nicht lesbar", Severity = CheckSeverity.Error, Detail = ex.Message };
        }
    }

    public static CheckResult CheckCodeCatalogKnown(ParseResult result)
    {
        var tokens = result.Variants.SelectMany(v => v.Rows).SelectMany(r => r.Tokens)
            .Where(t => ExpressionParser.TypeOf(t) is not (TokenType.Snr or TokenType.Ek or TokenType.Br or TokenType.Bm))
            .Distinct().ToList();
        var notInCatalog = tokens.Where(t => CodeCatalog.TryGet(t) is null).ToList();
        var tmeTokens = notInCatalog.Where(t => ExpressionParser.TypeOf(t) == TokenType.Tme).ToList();
        var unknown = notInCatalog.Except(tmeTokens).ToList();

        if (unknown.Count == 0 && tmeTokens.Count == 0)
            return new CheckResult { Title = "Codes in Codeliste bekannt", Severity = CheckSeverity.Ok, Detail = $"Alle {tokens.Count} verwendeten Codes stehen in der Codeliste." };

        if (unknown.Count == 0)
            return new CheckResult
            {
                Title = "TME-Referenzen erkannt",
                Severity = CheckSeverity.Ok,
                Detail = "Technisches Merkmal, kein PR-Code - steht deshalb erwartungsgemaess nicht in der Codeliste, Inhalt aktuell unbekannt: "
                    + string.Join(", ", tmeTokens) + ". Definition im Tab 'TME-Referenzen' importieren."
            };

        var tmeNote = tmeTokens.Count == 0 ? "" :
            " Zusaetzlich erkannt als Technisches Merkmal (kein PR-Code, Inhalt aktuell unbekannt, siehe Tab 'TME-Referenzen'): " + string.Join(", ", tmeTokens) + ".";
        return new CheckResult
        {
            Title = "Unbekannte Codes",
            Severity = CheckSeverity.Warning,
            Detail = "Nicht in der Codeliste gefunden: " + string.Join(", ", unknown) + ". Bitte Schreibweise pruefen (evtl. trotzdem korrekt, falls die Codeliste nicht vollstaendig ist)." + tmeNote
        };
    }

    public static CheckResult CheckBaureihe(ParseResult result, string baureihe)
    {
        if (string.IsNullOrWhiteSpace(baureihe))
            return new CheckResult { Title = "Baureihen-Pruefung uebersprungen", Severity = CheckSeverity.Warning, Detail = "Keine Baureihe angegeben - Codes wurden nicht auf Gueltigkeit fuer eine bestimmte Baureihe geprueft." };

        var bau = baureihe.Trim();
        var tokens = result.Variants.SelectMany(v => v.Rows).SelectMany(r => r.Tokens).Distinct().ToList();
        var invalid = new List<string>();
        foreach (var t in tokens)
        {
            var entry = CodeCatalog.TryGet(t);
            if (entry is null) continue;
            if (!entry.Validities.Any(v => v.Baureihe.Equals(bau, StringComparison.OrdinalIgnoreCase))) invalid.Add(t);
        }

        if (invalid.Count == 0)
            return new CheckResult { Title = $"Codes gueltig fuer Baureihe {bau}", Severity = CheckSeverity.Ok, Detail = $"Alle bekannten Codes sind fuer Baureihe {bau} in der Codeliste hinterlegt." };

        return new CheckResult
        {
            Title = $"Codes nicht fuer Baureihe {bau} hinterlegt",
            Severity = CheckSeverity.Error,
            Detail = "Nicht fuer diese Baureihe in der Codeliste gefunden: " + string.Join(", ", invalid)
        };
    }

    public static CheckResult CheckExpiry(ParseResult result, string baureihe)
    {
        var today = DateOnly.FromDateTime(DateTime.Now);
        var tokens = result.Variants.SelectMany(v => v.Rows).SelectMany(r => r.Tokens).Distinct().ToList();
        var expired = new List<string>();
        foreach (var t in tokens)
        {
            var entry = CodeCatalog.TryGet(t);
            if (entry is null) continue;
            var relevant = string.IsNullOrWhiteSpace(baureihe)
                ? entry.Validities
                : entry.Validities.Where(v => v.Baureihe.Equals(baureihe.Trim(), StringComparison.OrdinalIgnoreCase)).ToList();
            if (relevant.Count > 0 && relevant.All(v => v.IsExpired(today))) expired.Add(t);
        }

        if (expired.Count == 0)
            return new CheckResult { Title = "Keine abgelaufenen Codes", Severity = CheckSeverity.Ok, Detail = "Keiner der verwendeten Codes ist laut Codeliste ('Bis Gueltigkeit') abgelaufen." };

        return new CheckResult
        {
            Title = "Abgelaufene Codes",
            Severity = CheckSeverity.Warning,
            Detail = "Laut Codeliste evtl. bereits abgelaufen: " + string.Join(", ", expired)
        };
    }

    /// <summary>Vergleicht importiertes Original-XML mit dem aus der rekonstruierten Codebedingung neu erzeugten XML.</summary>
    public static CheckResult CheckRoundTrip(string? importedXml, string? rebuiltXml)
    {
        if (string.IsNullOrEmpty(importedXml))
            return new CheckResult { Title = "Rundlauf-Pruefung", Severity = CheckSeverity.Ok, Detail = "Kein XML-Import aktiv - Pruefung entfaellt." };
        if (string.IsNullOrEmpty(rebuiltXml))
            return new CheckResult { Title = "Rundlauf-Pruefung nicht moeglich", Severity = CheckSeverity.Warning, Detail = "Aus der rekonstruierten Codebedingung konnte noch kein XML erzeugt werden (TME pruefen)." };

        try
        {
            var a = ExtractRowKeys(importedXml);
            var b = ExtractRowKeys(rebuiltXml);
            if (a.SequenceEqual(b))
                return new CheckResult
                {
                    Title = "Rundlauf-Pruefung erfolgreich",
                    Severity = CheckSeverity.Ok,
                    Detail = $"Importiertes XML und das aus der rekonstruierten Codebedingung neu erzeugte XML stimmen in allen {a.Count} Ausdruckszeilen exakt ueberein."
                };

            return new CheckResult
            {
                Title = "Rundlauf-Pruefung: Abweichung",
                Severity = CheckSeverity.Error,
                Detail = $"Neu erzeugtes XML weicht vom importierten Original ab ({b.Count} vs. {a.Count} Zeilen). Die rekonstruierte Codebedingung bildet die Original-Logik evtl. nicht exakt ab."
            };
        }
        catch (Exception ex)
        {
            return new CheckResult { Title = "Rundlauf-Pruefung fehlgeschlagen", Severity = CheckSeverity.Error, Detail = ex.Message };
        }
    }

    private static List<string> ExtractRowKeys(string xml) => XDocument.Parse(xml)
        .Descendants().Where(e => e.Name.LocalName == "TmeAusdruckTabelleImport")
        .Select(e => string.Join("|",
            GetChild(e, "LfdNr"), GetChild(e, "ZeilenNr"), GetChild(e, "SpaltenNr"), GetChild(e, "Operator"), GetChild(e, "WertS08S71RD")))
        .ToList();

    private static string GetChild(XElement e, string name)
        => e.Elements().FirstOrDefault(c => c.Name.LocalName == name)?.Value ?? "";

    /// <summary>SNR (Schichtnummer) steht in der Praxis immer als erste Zeile eines Blocks.</summary>
    public static CheckResult CheckSnrPosition(ParseResult result)
    {
        var offenders = new List<string>();
        foreach (var v in result.Variants)
        {
            for (int i = 0; i < v.Rows.Count; i++)
            {
                var isSnr = v.Rows[i].Tokens.Any(t => ExpressionParser.TypeOf(t) == TokenType.Snr);
                if (isSnr && i != 0) offenders.Add($"Nr. {v.Lfd} Zeile {i + 1}");
            }
        }

        if (offenders.Count == 0)
            return new CheckResult { Title = "SNR-Position ok", Severity = CheckSeverity.Ok, Detail = "SNR-Bedingungen stehen (falls vorhanden) an erster Zeile ihres Blocks." };

        return new CheckResult
        {
            Title = "SNR nicht an erster Zeile",
            Severity = CheckSeverity.Warning,
            Detail = "Unueblich, bitte pruefen: " + string.Join(", ", offenders) + ". SNR (Schichtnummer) steht normalerweise als erste Zeile eines Blocks."
        };
    }
}
