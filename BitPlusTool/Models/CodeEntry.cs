namespace BitPlusTool.Models;

/// <summary>Eine Baureihen-spezifische Gueltigkeit eines Codes aus der Codeliste.</summary>
public class CodeValidity
{
    public string Baureihe { get; set; } = "";
    public string Ausfuehrungen { get; set; } = "";
    public string Teilewirksam { get; set; } = "";
    public string BisGueltigkeit { get; set; } = "";

    public bool IsExpired(DateOnly today)
    {
        if (string.IsNullOrWhiteSpace(BisGueltigkeit) || BisGueltigkeit.Equals("offen", StringComparison.OrdinalIgnoreCase))
            return false;
        return DateOnly.TryParseExact(BisGueltigkeit, "dd.MM.yyyy", out var end) && end < today;
    }
}

/// <summary>Ein Code (z. B. M256) mit Bezeichnung und den Baureihen, fuer die er gilt.</summary>
public class CodeEntry
{
    public string Code { get; set; } = "";
    public string CodeArt { get; set; } = "";
    public string Bezeichnung { get; set; } = "";
    public List<CodeValidity> Validities { get; set; } = new();
}
