namespace BitPlusTool.Models;

/// <summary>Eine flache Zeile fuer die PLUS-Ansicht (DataGrid), gruppiert nach Lfd (Nr.-Block).</summary>
public class PlusDisplayRow
{
    public int Lfd { get; set; }
    public int Zeile { get; set; }
    public string Typ { get; set; } = "";
    public string Operator { get; set; } = "";
    public string Werte { get; set; } = "";
    public string Definition { get; set; } = "";
}
