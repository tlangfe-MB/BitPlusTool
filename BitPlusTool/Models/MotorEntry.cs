namespace BitPlusTool.Models;

/// <summary>Eine Zeile aus der Baumuster-/Motoren-Uebersicht: welche Codebedingung (Motor-Kombination)
/// zu welchem Fahrzeug/Motor gehoert (Verkaufsbezeichnung, Leistung, Motor-Konzept usw.).</summary>
public class MotorEntry
{
    public string Codebedingung { get; set; } = "";
    public HashSet<string> Tokens { get; set; } = new(StringComparer.OrdinalIgnoreCase);
    public string Lk { get; set; } = "";
    public string BmAa { get; set; } = "";
    public string Benennung { get; set; } = "";
    public string EntwBez { get; set; } = "";
    public string Baureihe { get; set; } = "";
    public string VerkBez { get; set; } = "";
    public string Antriebsart { get; set; } = "";
    public string Kw { get; set; } = "";
    public string Ps { get; set; } = "";
    public string MotorKonzept { get; set; } = "";
    public string MotorArt { get; set; } = "";
    public string ZylAnzahl { get; set; } = "";
}
