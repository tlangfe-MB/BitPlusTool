using System.Text;
using System.Text.RegularExpressions;
using BitPlusTool.Models;

namespace BitPlusTool.Services;

/// <summary>Portierte Parser-Logik aus dem urspruenglichen HTML-Tool (1:1 Verhalten).</summary>
public static class ExpressionParser
{
    private static readonly Regex InlineMinusRegex = new(@"([A-Z0-9])-(ME\d+[A-Z0-9]*|M\d+[A-Z0-9]*)", RegexOptions.IgnoreCase | RegexOptions.Compiled);
    private static readonly Regex WhitespaceRegex = new(@"\s+", RegexOptions.Compiled);
    private static readonly Regex TrailingSemicolonRegex = new(@";+$", RegexOptions.Compiled);
    private static readonly Regex LeadingMinusRegex = new(@"^-+", RegexOptions.Compiled);
    private static readonly Regex TmeRegex1 = new(@"^TH\d+", RegexOptions.Compiled);
    private static readonly Regex TmeRegex2 = new(@"^S-", RegexOptions.Compiled);
    private static readonly Regex PcvRegex1 = new(@"^M\d+", RegexOptions.Compiled);
    private static readonly Regex PcvRegex2 = new(@"^ME\d+", RegexOptions.Compiled);
    private static readonly Regex SnrRegex = new(@"^SNR(<=|>=|<|>)(.+)$", RegexOptions.IgnoreCase | RegexOptions.Compiled);
    // 2 Buchstaben+4 Ziffern (z. B. "PH6068") oder 3 Buchstaben+3 Ziffern (z. B. "LKR097") - beide Formen
    // kommen als reine TME-Querverweise vor, die in keinem codedata.csv/Katalog stehen.
    private static readonly Regex AmbiguousTmeShapeRegex = new(@"^([A-Z]{2}\d{4}|[A-Z]{3}\d{3})$", RegexOptions.Compiled);

    // Manuelle Einstufung fuer Codes, die wie ein TME aussehen koennten (2 Buchstaben + 4 Ziffern),
    // aber nicht eindeutig ueber TH.../S-... erkennbar sind (z. B. "PH6068"). Wird interaktiv vom
    // Aufrufer (UI) befuellt, siehe IsAmbiguousTmeCandidate/SetTmeOverride/GetTmeOverride.
    private static readonly Dictionary<string, bool> TmeOverrides = new(StringComparer.OrdinalIgnoreCase);

    public static void SetTmeOverride(string code, bool isTme) => TmeOverrides[Tok(code)] = isTme;
    public static bool? GetTmeOverride(string code) => TmeOverrides.TryGetValue(Tok(code), out var v) ? v : null;
    public static void ClearTmeOverrides() => TmeOverrides.Clear();

    /// <summary>Sieht aus wie ein moegliches TME (2 Buchstaben + 4 Ziffern), ist aber nicht sicher TH/M/ME/SNR.
    /// EK/BR/BM sind ueber ihr Prefix bereits eindeutig (siehe TypeOf, das diese Praefixe vor TmeOverrides
    /// prueft) und duerfen daher nie zur Nachfrage fuehren.</summary>
    public static bool IsAmbiguousTmeCandidate(string t)
    {
        t = Tok(t);
        if (SnrRegex.IsMatch(t) || t.StartsWith("SNR", StringComparison.Ordinal)) return false;
        if (t.StartsWith("EK", StringComparison.Ordinal) || t.StartsWith("BR", StringComparison.Ordinal) || t.StartsWith("BM", StringComparison.Ordinal)) return false;
        if (TmeRegex1.IsMatch(t) || TmeRegex2.IsMatch(t)) return false;
        if (PcvRegex1.IsMatch(t) || PcvRegex2.IsMatch(t)) return false;
        if (TmeOverrides.ContainsKey(t)) return false;
        return AmbiguousTmeShapeRegex.IsMatch(t);
    }

    /// <summary>SNR (Schichtnummer) traegt ihren Vergleichsoperator direkt im Token, z. B. "SNR&lt;93120230210T416".</summary>
    public static bool IsComparisonOp(string op) => op is "<" or "<=" or ">" or ">=";

    public static string NormalizeInlineMinus(string s) => InlineMinusRegex.Replace(s, "$1+-$2");

    public static List<string> SplitTopLevel(string str, char sep)
    {
        var result = new List<string>();
        var buf = new StringBuilder();
        int depth = 0;
        foreach (var c in str)
        {
            if (c == '(') depth++;
            else if (c == ')') depth = Math.Max(0, depth - 1);
            if (c == sep && depth == 0)
            {
                result.Add(buf.ToString());
                buf.Clear();
            }
            else buf.Append(c);
        }
        if (buf.ToString().Trim().Length > 0) result.Add(buf.ToString());
        return result.Select(x => x.Trim()).Where(x => x.Length > 0).ToList();
    }

    public static string BalanceInput(string s)
    {
        s = s.Replace("\r", "");
        s = WhitespaceRegex.Replace(s, "");
        s = TrailingSemicolonRegex.Replace(s, "");
        s = NormalizeInlineMinus(s);
        int open = s.Count(c => c == '(');
        int close = s.Count(c => c == ')');
        if (close > open) s = new string('(', close - open) + s;
        else if (open > close) s += new string(')', open - close);
        return s;
    }

    public static string StripOuter(string s)
    {
        s = s.Trim();
        while (s.StartsWith("(", StringComparison.Ordinal) && s.EndsWith(")", StringComparison.Ordinal))
        {
            int depth = 0; bool ok = true;
            for (int i = 0; i < s.Length; i++)
            {
                if (s[i] == '(') depth++;
                else if (s[i] == ')') depth--;
                if (depth == 0 && i < s.Length - 1) { ok = false; break; }
            }
            if (!ok) break;
            s = s.Substring(1, s.Length - 2).Trim();
        }
        return s;
    }

    public static string Tok(string t)
    {
        t = StripOuter(t.Trim());
        if (t.StartsWith("+", StringComparison.Ordinal)) t = t.Substring(1);
        t = LeadingMinusRegex.Replace(t, "");
        return t.ToUpperInvariant();
    }

    public static TokenType TypeOf(string t)
    {
        t = Tok(t);
        if (SnrRegex.IsMatch(t) || t.StartsWith("SNR", StringComparison.Ordinal)) return TokenType.Snr;
        if (t.StartsWith("EK", StringComparison.Ordinal)) return TokenType.Ek;
        if (t.StartsWith("BR", StringComparison.Ordinal)) return TokenType.Br;
        if (t.StartsWith("BM", StringComparison.Ordinal)) return TokenType.Bm;
        if (TmeRegex1.IsMatch(t) || TmeRegex2.IsMatch(t)) return TokenType.Tme;
        if (PcvRegex1.IsMatch(t) || PcvRegex2.IsMatch(t)) return TokenType.Pcv;
        if (TmeOverrides.TryGetValue(t, out var isTme)) return isTme ? TokenType.Tme : TokenType.Pc;
        // Codes mit unklarer Form (z. B. "PH6068", "LKR097"): steht der Code in der Codeliste, ist es ein
        // PR-Code; TMEs stehen dort nie drin (siehe Validator.CheckCodeCatalogKnown) - automatische
        // Entscheidung statt Rueckfrage per MessageBox (verursachte ein DWM-Render-Artefakt).
        if (AmbiguousTmeShapeRegex.IsMatch(t))
            return CodeCatalog.TryGet(t) is null ? TokenType.Tme : TokenType.Pc;
        return TokenType.Pc;
    }

    public static string ValueOf(string t, TokenType type)
    {
        t = Tok(t);
        if (type == TokenType.Snr) return t.StartsWith("SNR", StringComparison.Ordinal) ? t.Substring(3) : t;
        if (type == TokenType.Ek) return (t.StartsWith("EK", StringComparison.Ordinal) ? t.Substring(2) : t).PadRight(28, '*');
        if (type == TokenType.Br) return t.StartsWith("BR", StringComparison.Ordinal) ? t.Substring(2) : t;
        if (type == TokenType.Bm) return (t.StartsWith("BM", StringComparison.Ordinal) ? t.Substring(2) : t).PadRight(12, '*');
        return type == TokenType.Pcv ? t.PadRight(6, '*') : t;
    }

    /// <summary>Letzte Stelle der BM-AA-Nummer (Baumusteruebersicht Werk Bremen) codiert die Lenkerseite
    /// ("1" = Linkslenker, "2" = Rechtslenker; "5"/"6" = dieselbe Seite fuer eine Sondervariante) - vom
    /// User bestaetigt anhand Beispiel "C2363521". Andere Stellen sind (noch) nicht dekodiert.</summary>
    public static string? DescribeBmLenkung(string t)
    {
        var sig = Tok(t);
        if (sig.StartsWith("BM", StringComparison.Ordinal)) sig = sig.Substring(2);
        if (sig.Length == 0) return null;
        return sig[^1] switch
        {
            '1' or '5' => "Linkslenker",
            '2' or '6' => "Rechtslenker",
            _ => null,
        };
    }

    public static PlusRow MakeRow(string op, IEnumerable<string> tokens)
        => new() { Op = op, Tokens = tokens.Select(Tok).Where(x => x.Length > 0).ToList() };

    /// <summary>
    /// Baut eine einzelne Zeile aus einem Wert. Erkennt SNR-Vergleiche ("SNR&lt;wert") und setzt dafuer den
    /// eingebetteten Vergleichsoperator direkt als Zeilen-Operator (ein evtl. fuehrendes '-' wird dann ignoriert,
    /// da SNR ihren Operator selbst mitbringt). Der gespeicherte Token bleibt "SNR"+Wert (ohne Operator-Zeichen),
    /// damit TypeOf() ihn spaeter weiterhin selbststaendig erkennt.
    /// </summary>
    public static PlusRow MakeSingleValueRow(string raw, bool negated)
    {
        var normalized = Tok(raw);
        var m = SnrRegex.Match(normalized);
        if (m.Success)
            return new PlusRow { Op = m.Groups[1].Value, Tokens = new List<string> { "SNR" + m.Groups[2].Value } };
        return new PlusRow { Op = negated ? "<>" : "=", Tokens = new List<string> { normalized } };
    }

    /// <summary>
    /// Parst den flachen Inhalt EINER Klammerebene in Bloecke. '+' = neue Zeile, '/' zwischen zwei blanken
    /// Werten = ODER in derselben Zeile (lokale Verschmelzung, KEINE Block-Aufspaltung - siehe Hinweis unten).
    /// Steckt in einem '+'-Teil aber selbst eine geklammerte (Unter-)Gruppe (z. B. "-(A+B)" oder "(A)/(B)/(C)"),
    /// wird diese rekursiv aufgeloest: negierte Gruppen per De-Morgan in UND-verknuepfte Negativ-Zeilen,
    /// nicht negierte Mehrfach-Gruppen als eigene Bloecke, die mit dem bisherigen Ergebnis kreuzmultipliziert
    /// werden (genau wie '+' auf oberster Ebene) - so werden z. B. mehrere explizit geklammerte Varianten
    /// "(A)/(B)/(C)" innerhalb einer Klammer zu 3 Nr.-Bloecken.
    ///
    /// WICHTIG: '/' zwischen zwei blanken (nicht geklammerten) Werten bleibt bewusst eine lokale ODER-Zeile,
    /// auch wenn der Wert davor ueber '+' erreicht wurde (z. B. "SNR&gt;=X+R7I/R8L+-PH6068" -> EIN Block mit
    /// den Zeilen SNR, "R7I/R8L", "-PH6068" - durch echte Mercedes-XML-Daten bestaetigt). Eine generelle
    /// "UND-vor-ODER"-Aufspaltung wuerde SNR/PH6068 faelschlich nur einer Seite zuordnen. AUSNAHME: Gibt es
    /// OHNE Klammern 3 oder mehr Alternativen (mind. 2 '/' auf oberster Ebene) und mindestens eine davon ist
    /// selbst eine '+'-Kette (mehr als ein Wert), ist das ein starkes Indiz fuer echte, komplette Varianten
    /// (z. B. Motor-Varianten "A+B+C/A+B+D/A+B+E") statt einer einfachen Wert-Alternative wie "R7I/R8L" -
    /// dann wird trotzdem jede Alternative ein eigener Block. Bei genau 1 '/' (2 Alternativen) bleibt es
    /// dagegen immer bei der lokalen ODER-Zeile (siehe SNR-Fall oben), das ist die haeufigere, mehrdeutige Form.
    /// </summary>
    public static List<List<PlusRow>> ParseAltToRows(string alt, bool inheritedNeg)
    {
        var stripped = StripOuter(alt);
        var altParts = SplitTopLevel(stripped, '/');
        if (altParts.Count >= 3 && altParts.Any(a => SplitTopLevel(StripOuter(a), '+').Count > 1))
        {
            // Alternativen, die explizit ihre eigene Klammer tragen ("(A+B)/(C+D)/..."), sind ein
            // eindeutiges Signal des Users "jede davon ist ein eigener Block" - werden IMMER einzeln
            // verarbeitet, unabhaengig von gemeinsamen Praefixen (siehe "MIT inneren Klammern"-Testfall).
            // Bare Alternativen ohne eigene Klammer, die sich NUR im letzten Wert unterscheiden
            // (identischer "+"-Praefix, z. B. "A08+M254+1U2+460" / "...+494" / "...+835"), gehoeren
            // dagegen fachlich zusammen und bleiben EIN Block mit einer ODER-Zeile fuer die
            // abweichenden letzten Werte (Bug-Report TH0864: 4 Bloecke waren falsch, richtig sind 2 -
            // ein Block je unterschiedlichem Praefix "A08+M254+1U2" bzw. "A12+M254").
            var byPrefix = new Dictionary<string, List<List<string>>>();
            var order = new List<string>();
            var ownBlocks = new List<string>();
            foreach (var a in altParts)
            {
                if (IsFullyParenthesized(a)) { ownBlocks.Add(a); continue; }
                var tokens = SplitTopLevel(StripOuter(a), '+');
                if (tokens.Count == 0) { ownBlocks.Add(a); continue; }
                var prefixKey = string.Join("+", tokens.Take(tokens.Count - 1));
                if (!byPrefix.TryGetValue(prefixKey, out var list)) { list = new(); byPrefix[prefixKey] = list; order.Add(prefixKey); }
                list.Add(tokens);
            }

            var altBlocks = new List<List<PlusRow>>();
            foreach (var key in order)
            {
                var group = byPrefix[key];
                if (group.Count == 1)
                {
                    var singleAlt = string.Join("+", group[0]);
                    altBlocks.AddRange(ParseAltToRows(singleAlt, inheritedNeg));
                    continue;
                }
                var row = group[0].Take(group[0].Count - 1).Select(t => MakeSingleValueRow(t, inheritedNeg)).ToList();
                row.Add(MakeRow(inheritedNeg ? "<>" : "=", group.Select(g => g[^1])));
                altBlocks.Add(row);
            }
            foreach (var a in ownBlocks)
                altBlocks.AddRange(ParseAltToRows(a, inheritedNeg));

            return altBlocks;
        }

        var parts = SplitTopLevel(stripped, '+');
        var blocks = new List<List<PlusRow>> { new List<PlusRow>() };

        foreach (var p in parts)
        {
            if (string.IsNullOrEmpty(p)) continue;
            bool neg = inheritedNeg;
            string body = p;
            if (p.StartsWith("-", StringComparison.Ordinal)) { neg = !inheritedNeg; body = p.Substring(1); }
            body = body.Trim();

            var parenGroups = TrySplitParenGroups(body);
            if (parenGroups != null)
            {
                if (neg)
                {
                    var flat = new List<PlusRow>();
                    foreach (var g in parenGroups) flat.AddRange(FlattenNegatedGroup(g));
                    blocks = blocks.Select(b => b.Concat(flat).ToList()).ToList();
                }
                else
                {
                    var altBlockGroups = parenGroups.Select(g => ParseAltToRows(g, false)).ToList();
                    var combined = new List<List<PlusRow>>();
                    foreach (var b in blocks)
                        foreach (var altBlocks2 in altBlockGroups)
                            foreach (var altBlock in altBlocks2)
                                combined.Add(b.Concat(altBlock).ToList());
                    blocks = combined;
                }
                continue;
            }

            body = StripOuter(body);
            var subAlts = SplitTopLevel(body, '/');
            var row = subAlts.Count == 1 ? MakeSingleValueRow(subAlts[0], neg) : MakeRow(neg ? "<>" : "=", subAlts);
            blocks = blocks.Select(b => b.Append(row).ToList()).ToList();
        }

        return blocks;
    }

    /// <summary>Prueft, ob "a" (optional mit fuehrendem '-') als Ganzes GENAU eine Klammer-Gruppe ist,
    /// z. B. "(A+B)" oder "-(A+B)" - nicht aber "A+B" oder "(A)+B".</summary>
    private static bool IsFullyParenthesized(string a)
    {
        var s = a.Trim();
        if (s.StartsWith("-", StringComparison.Ordinal)) s = s.Substring(1).TrimStart();
        if (s.Length < 2 || s[0] != '(' || s[^1] != ')') return false;
        int depth = 0;
        for (int i = 0; i < s.Length; i++)
        {
            if (s[i] == '(') depth++;
            else if (s[i] == ')') { depth--; if (depth == 0 && i < s.Length - 1) return false; }
        }
        return true;
    }

    /// <summary>Erkennt, ob "body" nur aus einer oder mehreren komplett geklammerten Gruppen besteht, die
    /// ausschliesslich durch '/' getrennt sind (z. B. "(A+B)/(C+D)" oder nur "(A+B)"). Gibt dann die reinen
    /// Klammerinhalte (ohne die aeusseren Klammern) zurueck, sonst null (= normaler flacher Wert-/ODER-Ausdruck).</summary>
    private static List<string>? TrySplitParenGroups(string body)
    {
        if (body.Length == 0 || body[0] != '(') return null;
        var parts = SplitTopLevel(body, '/');
        var inner = new List<string>();
        foreach (var part in parts)
        {
            var t = part.Trim();
            if (t.Length < 2 || t[0] != '(' || t[^1] != ')') return null;
            int depth = 0;
            for (int i = 0; i < t.Length; i++)
            {
                if (t[i] == '(') depth++;
                else if (t[i] == ')') { depth--; if (depth == 0 && i < t.Length - 1) return null; }
            }
            inner.Add(t.Substring(1, t.Length - 2));
        }
        return inner;
    }

    /// <summary>
    /// Baut Bloecke (Nr.) aus einer Top-Level-Sequenz von Werten/Gruppen, verbunden durch '+' (UND) und '/' (ODER).
    /// Regel fuer '/': Ist der Wert davor ein "frischer" einfacher Einzelwert (nicht ueber '+' erreicht, keine
    /// Klammer-Gruppe), wird ODER in dieselbe Zeile gemischt (z. B. "M256/M654"). Sobald der Wert davor ueber '+'
    /// erreicht wurde oder eine Klammer-Gruppe ist, eroeffnet '/' stattdessen einen neuen Nr.-Block.
    /// Bei geklammerten Gruppen wie "(A)/(B)/(C)" ist deshalb jede Gruppe automatisch ein eigener Block.
    /// </summary>
    private static List<List<PlusRow>> ParseSequence(string s, int start, int end)
    {
        var results = new List<List<PlusRow>>();
        var current = new List<List<PlusRow>>();
        bool fresh = true;
        int pos = start;

        (List<List<PlusRow>> blocks, bool complex) ReadAtom()
        {
            bool neg = false;
            if (pos < end && s[pos] == '-') { neg = true; pos++; }

            if (pos < end && s[pos] == '(')
            {
                int depth = 0; int i = pos; int innerStart = pos + 1;
                for (; i < end; i++)
                {
                    if (s[i] == '(') depth++;
                    else if (s[i] == ')') { depth--; if (depth == 0) break; }
                }
                var innerEnd = i;
                pos = Math.Min(i + 1, end);
                if (neg)
                {
                    var flat = FlattenNegatedGroup(s.Substring(innerStart, innerEnd - innerStart));
                    return (new List<List<PlusRow>> { flat }, true);
                }
                // Direkter Klammerinhalt: '+' = neue Zeile, '/' = ODER innerhalb der Zeile (keine
                // Block-Aufspaltung nach der "frisch/Position"-Regel). Enthaelt der Inhalt aber selbst
                // geklammerte Alternativen ("(A)/(B)/(C)"), liefert ParseAltToRows dafuer mehrere Bloecke.
                var innerBlocks = ParseAltToRows(s.Substring(innerStart, innerEnd - innerStart), false);
                return (innerBlocks, true);
            }

            int tokenStart = pos;
            while (pos < end && s[pos] != '+' && s[pos] != '/' && s[pos] != '(') pos++;
            var raw = s.Substring(tokenStart, pos - tokenStart);
            var row = MakeSingleValueRow(raw, neg);
            return (new List<List<PlusRow>> { new List<PlusRow> { row } }, false);
        }

        void FinalizeCurrent()
        {
            results.AddRange(current);
            current = new List<List<PlusRow>>();
        }

        var (firstBlocks, firstComplex) = ReadAtom();
        current = firstBlocks;
        fresh = !firstComplex;

        while (pos < end)
        {
            char op = s[pos];
            pos++;
            var (nextBlocks, nextComplex) = ReadAtom();

            if (op == '+')
            {
                var combined = new List<List<PlusRow>>();
                foreach (var c in current)
                    foreach (var n in nextBlocks)
                        combined.Add(c.Concat(n).ToList());
                current = combined;
                fresh = false;
            }
            else
            {
                bool canMerge = fresh && !nextComplex && current.Count == 1 && current[0].Count > 0;
                if (canMerge)
                {
                    current[0][^1].Tokens.Add(nextBlocks[0][0].Tokens[0]);
                }
                else
                {
                    FinalizeCurrent();
                    current = nextBlocks;
                    fresh = !nextComplex;
                }
            }
        }

        FinalizeCurrent();
        return results;
    }

    private static List<PlusRow> FlattenNegatedGroup(string inner)
    {
        var altStrings = SplitTopLevel(StripOuter(inner), '/');
        var rows = new List<PlusRow>();
        foreach (var alt in altStrings)
            foreach (var block in ParseAltToRows(alt, true))
                rows.AddRange(block);
        return rows;
    }

    public static ParseResult Parse(string code)
    {
        var normalized = BalanceInput(code ?? "");
        var blocks = normalized.Length == 0
            ? new List<List<PlusRow>>()
            : ParseSequence(normalized, 0, normalized.Length);

        var variants = blocks
            .Select((rows, i) => new PlusVariant { Lfd = i + 1, Rows = rows })
            .ToList();

        return new ParseResult { Normalized = normalized, Variants = variants };
    }

    public static string DisplayOp(int i, PlusRow r)
    {
        if (IsComparisonOp(r.Op)) return (i == 0 ? "" : "+") + r.Op;
        if (i == 0) return r.Op == "<>" ? "<>" : "=";
        return r.Op == "<>" ? "+<>" : "+";
    }

    public static string DisplayDefinition(int i, PlusRow r, List<string> vals)
    {
        var op = DisplayOp(i, r);
        return string.Join(" / ", vals.Select(v => op + v));
    }

    /// <summary>
    /// Baut aus Bloecken/Zeilen wieder eine einzeilige Codebedingung (Umkehrung von Parse), z. B. fuer den
    /// XML-Import: mehrere Bloecke werden je in Klammern gesetzt und mit '/' verbunden, damit sie beim
    /// erneuten Parsen garantiert wieder als eigene Bloecke erkannt werden (keine versehentliche ODER-Verschmelzung).
    /// </summary>
    public static string ToCodeString(List<PlusVariant> variants)
    {
        var blocks = variants.Select(v => BuildBlockString(v.Rows)).ToList();
        var body = blocks.Count <= 1
            ? (blocks.FirstOrDefault() ?? "")
            : string.Join("/", blocks.Select(b => "(" + b + ")"));
        return body + ";";
    }

    private static string BuildBlockString(List<PlusRow> rows)
    {
        var parts = new List<string>();
        foreach (var r in rows)
        {
            if (r.Tokens.Count == 0) continue;
            if (IsComparisonOp(r.Op))
            {
                // SNR & Co.: eingebetteter Vergleichsoperator, Token ist "SNR"+Wert -> Operator wieder einfuegen.
                var tok = r.Tokens[0];
                var val = tok.StartsWith("SNR", StringComparison.Ordinal) ? tok.Substring(3) : tok;
                parts.Add("SNR" + r.Op + val);
            }
            else if (r.Tokens.Count == 1)
                parts.Add((r.Op == "<>" ? "-" : "") + r.Tokens[0]);
            else
            {
                // Immer klammern, auch unnegiert: sonst ist ein lokales ODER wie "460/494/835" beim
                // erneuten Parsen nicht von 3+ echten Motor-Varianten-Alternativen unterscheidbar
                // (siehe ExpressionParser.ParseAltToRows-Heuristik) und wuerde faelschlich in mehrere
                // eigene Bloecke aufgespalten (Bug-Report TH0864: 4 -> 6 Bloecke nach Reparse).
                var joined = string.Join("/", r.Tokens);
                parts.Add(r.Op == "<>" ? "-(" + joined + ")" : "(" + joined + ")");
            }
        }
        return string.Join("+", parts);
    }

    /// <summary>Flache Anzeige-/Export-Zeilen (Nr., Zeile, Typ, Operator, Werte, Definition) fuer Grid, CSV, JSON, HTML, Druck.</summary>
    public static List<PlusDisplayRow> ToDisplayRows(ParseResult result)
    {
        var rows = new List<PlusDisplayRow>();
        foreach (var v in result.Variants)
        {
            for (int ri = 0; ri < v.Rows.Count; ri++)
            {
                var r = v.Rows[ri];
                var vals = r.Tokens.Select(t => ValueOf(t, TypeOf(t))).ToList();
                var types = string.Join("/", r.Tokens.Select(t => TypeOf(t).ToString().ToUpperInvariant()).Distinct());
                rows.Add(new PlusDisplayRow
                {
                    Lfd = v.Lfd,
                    Zeile = ri + 1,
                    Typ = types,
                    Operator = DisplayOp(ri, r),
                    Werte = string.Join(" / ", vals),
                    Definition = DisplayDefinition(ri, r, vals)
                });
            }
        }
        return rows;
    }
}
