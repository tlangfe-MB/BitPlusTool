namespace BitPlusTool.Models;

public enum CheckSeverity { Ok, Warning, Error }

/// <summary>Ergebnis einer einzelnen Selbstkontrolle (Validierung von Eingabe/Parsing/Ausgabe).</summary>
public class CheckResult
{
    public string Title { get; set; } = "";
    public CheckSeverity Severity { get; set; } = CheckSeverity.Ok;
    public string Detail { get; set; } = "";
}
