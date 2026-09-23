namespace BitPlusTool.Models;

/// <summary>Ein Nr.-Block mit seinen Zeilen, fuer die nebeneinander angeordnete Karten-Ansicht.</summary>
public class PlusBlock
{
    public int Lfd { get; set; }
    public List<PlusDisplayRow> Rows { get; set; } = new();
}
