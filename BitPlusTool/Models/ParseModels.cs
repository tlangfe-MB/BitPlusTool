namespace BitPlusTool.Models;

public enum TokenType { Tme, Pcv, Pc, Snr, Ek, Br, Bm }

/// <summary>Eine PLUS-Zeile: Operator (=/&lt;&gt;/&lt;/&lt;=/&gt;/&gt;=) plus eine Liste ODER-verknüpfter Tokens.</summary>
public class PlusRow
{
    public string Op { get; set; } = "=";
    public List<string> Tokens { get; set; } = new();

    public PlusRow Clone() => new() { Op = Op, Tokens = new List<string>(Tokens) };
}

/// <summary>Ein PLUS-Nr.-Block: fortlaufende Nummer plus seine UND-verknüpften Zeilen.</summary>
public class PlusVariant
{
    public int Lfd { get; set; }
    public List<PlusRow> Rows { get; set; } = new();
}

public class ParseResult
{
    public string Normalized { get; set; } = "";
    public List<PlusVariant> Variants { get; set; } = new();
}
