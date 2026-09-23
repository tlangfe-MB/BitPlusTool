using System.IO;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media;
using BitPlusTool.Models;
using BitPlusTool.Services;
using Microsoft.Win32;

namespace BitPlusTool;

public partial class MainWindow : Window
{
    private bool _fileNameAuto = true;
    private bool _suppressFileNameEvent;
    private string? _importedXml;
    private readonly Dictionary<string, TmeRefState> _tmeRefs = new(StringComparer.OrdinalIgnoreCase);

    private sealed class TmeRefState
    {
        public string? Codebedingung;
        public string? FileName;
    }

    private sealed record TmeRefDisplay(string Code, string StatusText, string Codebedingung, Visibility CodeVisibility);

    private bool _isDarkTheme;
    private double _zoom = 1.0;
    private double _baseWidth;
    private double _baseHeight;

    public MainWindow()
    {
        InitializeComponent();
        var version = System.Reflection.Assembly.GetExecutingAssembly().GetName().Version;
        Title += " – v" + version;
        _ = CodeCatalog.RefreshIfStaleAsync(); // Codeliste im Hintergrund aktualisieren, blockiert den Start nicht.
        Loaded += (_, _) => { _baseWidth = Width; _baseHeight = Height; Run(); };
    }

    private void ThemeToggleButton_Click(object sender, RoutedEventArgs e)
    {
        _isDarkTheme = !_isDarkTheme;
        var source = new Uri(_isDarkTheme ? "Themes/DarkTheme.xaml" : "Themes/LightTheme.xaml", UriKind.Relative);
        var newDict = new ResourceDictionary { Source = source };

        var app = Application.Current.Resources;
        // Die Theme-Dictionary ist immer die erste eingebundene MergedDictionary (siehe App.xaml).
        app.MergedDictionaries[0] = newDict;

        SunIcon.Visibility = _isDarkTheme ? Visibility.Collapsed : Visibility.Visible;
        SunRays.Visibility = _isDarkTheme ? Visibility.Collapsed : Visibility.Visible;
        MoonIcon.Visibility = _isDarkTheme ? Visibility.Visible : Visibility.Collapsed;
        ThemeToggleText.Text = _isDarkTheme ? "Hell" : "Dunkel";
    }

    private void RunButton_Click(object sender, RoutedEventArgs e) => Run();

    private void TmBox_TextChanged(object sender, TextChangedEventArgs e) => Run();

    private void BaureiheChip_Checked(object sender, RoutedEventArgs e) => Run();

    private void FileNameBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        if (_suppressFileNameEvent) return;
        _fileNameAuto = false;
    }

    private void SaveButton_Click(object sender, RoutedEventArgs e)
    {
        var tm = TmBox.Text.Trim().ToUpperInvariant();
        if (string.IsNullOrWhiteSpace(tm))
        {
            MessageBox.Show(this, "Bitte zuerst Technisches Merkmal/TME eingeben, z. B. TH1144.", "TME fehlt", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        Run();

        var xml = XmlBox.Text;
        if (!xml.StartsWith("<?xml", StringComparison.Ordinal))
        {
            MessageBox.Show(this, "Keine gültige XML erzeugt. Bitte Fehler im Tab 'Selbstkontrolle' prüfen.", "Fehler", MessageBoxButton.OK, MessageBoxImage.Error);
            return;
        }

        var rawName = FileNameBox.Text.Trim();
        var baseName = string.IsNullOrEmpty(rawName) ? tm : Path.GetFileNameWithoutExtension(rawName);
        var dlg = new SaveFileDialog
        {
            FileName = baseName + ".xml",
            Filter = "XML-Datei (*.xml)|*.xml|Alle Dateien (*.*)|*.*",
            DefaultExt = ".xml"
        };
        if (dlg.ShowDialog(this) == true)
        {
            File.WriteAllText(dlg.FileName, xml, new UTF8Encoding(false));
            StatusText.Text = $"Gespeichert: {dlg.FileName}";
            StatusText.Foreground = (Brush)FindResource("OkBrush");
        }
    }

    private void ImportButton_Click(object sender, RoutedEventArgs e)
    {
        var dlg = new OpenFileDialog { Filter = "XML-Datei (*.xml)|*.xml|Alle Dateien (*.*)|*.*" };
        if (dlg.ShowDialog(this) != true) return;

        try
        {
            var xmlText = File.ReadAllText(dlg.FileName);
            var imported = XmlImporter.Parse(xmlText);
            var code = ExpressionParser.ToCodeString(imported.Variants);

            _importedXml = xmlText;
            CodeBox.Text = code;

            if (!string.IsNullOrWhiteSpace(imported.TechnischesMerkmal))
            {
                _fileNameAuto = true;
                TmBox.Text = imported.TechnischesMerkmal!.Trim().ToUpperInvariant();
            }
            Run();
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, "Import fehlgeschlagen: " + ex.Message, "Fehler", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void Run()
    {
        if (CodeBox == null) return; // Radio-Buttons koennen Checked schon waehrend InitializeComponent() feuern.
        var code = CodeBox.Text;
        var tm = TmBox.Text.Trim().ToUpperInvariant();

        if (tm.Length > 0 && _fileNameAuto)
        {
            _suppressFileNameEvent = true;
            FileNameBox.Text = tm + ".xml";
            _suppressFileNameEvent = false;
        }
        else if (tm.Length == 0 && _fileNameAuto)
        {
            _suppressFileNameEvent = true;
            FileNameBox.Text = "";
            _suppressFileNameEvent = false;
        }

        var baureihe = BaureihePanel.Children.OfType<RadioButton>().FirstOrDefault(r => r.IsChecked == true)?.Content as string ?? "";
        if (baureihe == "Keine") baureihe = "";

        try
        {
            var result = ExpressionParser.Parse(code);
            ResolveAmbiguousTmeTokens(result);
            RenderPlus(result);
            RenderLogic(result, baureihe);
            RenderTmeRefs(result);

            var checks = new List<CheckResult>(Validator.ValidateParsing(code, result))
            {
                Validator.CheckCodeCatalogKnown(result),
                Validator.CheckBaureihe(result, baureihe),
                Validator.CheckExpiry(result, baureihe),
                Validator.CheckSnrPosition(result)
            };

            if (tm.Length > 0)
            {
                checks.Insert(0, Validator.CheckTechnischesMerkmal(tm));
                try
                {
                    var xml = XmlTemplateBuilder.Build(result.Variants, tm, code);
                    XmlBox.Text = xml;
                    checks.Add(Validator.CheckXmlConsistency(xml, result));
                    checks.Add(Validator.CheckRoundTrip(_importedXml, xml));

                    StatusText.Text = $"OK: {result.Variants.Count} Nr.-Block(e). XML für {tm} erzeugt.";
                    StatusText.Foreground = (Brush)FindResource("OkBrush");
                }
                catch (Exception exXml)
                {
                    XmlBox.Text = "";
                    StatusText.Text = "Fehler beim XML-Export: " + exXml.Message;
                    StatusText.Foreground = (Brush)FindResource("ErrorBrush");
                }
            }
            else
            {
                XmlBox.Text = "";
                StatusText.Text = $"OK: {result.Variants.Count} Nr.-Block(e) ausgewertet. Für XML bitte TME eingeben.";
                StatusText.Foreground = (Brush)FindResource("OkBrush");
            }

            RenderChecks(checks);
        }
        catch (Exception ex)
        {
            XmlBox.Text = "";
            PlusGrid.ItemsSource = null;
            ChecksList.ItemsSource = null;
            StatusText.Text = "Fehler: " + ex.Message;
            StatusText.Foreground = (Brush)FindResource("ErrorBrush");
        }
    }

    /// <summary>Fragt einmalig pro Code nach, ob ein unklarer 2-Buchstaben+4-Ziffern-Code (z. B. "PH6068")
    /// ein TME ist, oder als normaler PC-Code behandelt werden soll. TH.../S-... etc. sind bereits eindeutig.</summary>
    private void ResolveAmbiguousTmeTokens(ParseResult result)
    {
        var candidates = result.Variants.SelectMany(v => v.Rows).SelectMany(r => r.Tokens)
            .Where(ExpressionParser.IsAmbiguousTmeCandidate)
            .Distinct()
            .ToList();

        foreach (var t in candidates)
        {
            var answer = MessageBox.Show(this,
                $"Der Code '{t}' ist nicht eindeutig zuzuordnen.\n\nIst '{t}' ein Technisches Merkmal (TME)?\n\nJa = TME\nNein = normaler Code (PC)",
                "Code-Einstufung: " + t, MessageBoxButton.YesNo, MessageBoxImage.Question);
            ExpressionParser.SetTmeOverride(t, answer == MessageBoxResult.Yes);
        }
    }

    /// <summary>Ein Block gilt als abgelaufen, wenn seine (erste) SNR-Zeile "&lt;" oder "&lt;=" ist -
    /// referenzierte TME in solchen Bloecken muessen nicht mehr aufgeloest werden.</summary>
    private static bool IsBlockExpired(PlusVariant v)
    {
        if (v.Rows.Count == 0) return false;
        var first = v.Rows[0];
        var isSnr = first.Tokens.Any(t => ExpressionParser.TypeOf(t) == TokenType.Snr);
        return isSnr && (first.Op == "<" || first.Op == "<=");
    }

    private void RenderTmeRefs(ParseResult result)
    {
        var allTmeTokens = result.Variants
            .SelectMany(v => v.Rows).SelectMany(r => r.Tokens)
            .Where(t => ExpressionParser.TypeOf(t) == TokenType.Tme)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        var neededTokens = result.Variants
            .Where(v => !IsBlockExpired(v))
            .SelectMany(v => v.Rows).SelectMany(r => r.Tokens)
            .Where(t => ExpressionParser.TypeOf(t) == TokenType.Tme)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        foreach (var t in allTmeTokens)
            if (!_tmeRefs.ContainsKey(t)) _tmeRefs[t] = new TmeRefState();

        var items = allTmeTokens.Select(t =>
        {
            var state = _tmeRefs[t];
            var hasCode = !string.IsNullOrEmpty(state.Codebedingung);
            string status;
            if (hasCode) status = $"Importiert aus {state.FileName}";
            else if (!neededTokens.Contains(t)) status = "Abgelaufen (SNR < / <=) - Import nicht notwendig.";
            else status = "Noch nicht importiert - Definition unbekannt.";
            return new TmeRefDisplay(t, status, state.Codebedingung ?? "", hasCode ? Visibility.Visible : Visibility.Collapsed);
        }).ToList();

        TmeRefList.ItemsSource = items;
    }

    private void ImportTmeRef_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button { CommandParameter: string code }) return;

        var dlg = new OpenFileDialog { Filter = "XML-Datei (*.xml)|*.xml|Alle Dateien (*.*)|*.*", Title = $"XML fuer TME {code} importieren" };
        if (dlg.ShowDialog(this) != true) return;

        try
        {
            var xmlText = File.ReadAllText(dlg.FileName);
            var imported = XmlImporter.Parse(xmlText);
            var codebedingung = ExpressionParser.ToCodeString(imported.Variants);

            if (!string.IsNullOrWhiteSpace(imported.TechnischesMerkmal) &&
                !imported.TechnischesMerkmal!.Trim().Equals(code, StringComparison.OrdinalIgnoreCase))
            {
                var proceed = MessageBox.Show(this,
                    $"Die importierte Datei ist fuer TME '{imported.TechnischesMerkmal}', nicht '{code}'. Trotzdem uebernehmen?",
                    "TME weicht ab", MessageBoxButton.YesNo, MessageBoxImage.Warning);
                if (proceed != MessageBoxResult.Yes) return;
            }

            _tmeRefs[code] = new TmeRefState { Codebedingung = codebedingung, FileName = Path.GetFileName(dlg.FileName) };
            Run();
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, "Import fehlgeschlagen: " + ex.Message, "Fehler", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void RenderPlus(ParseResult result)
    {
        var rows = ExpressionParser.ToDisplayRows(result);
        PlusGrid.ItemsSource = rows
            .GroupBy(r => r.Lfd)
            .Select(g => new PlusBlock { Lfd = g.Key, Rows = g.ToList() })
            .ToList();
    }

    private void ZoomButton_Click(object sender, RoutedEventArgs e)
    {
        _zoom = Math.Clamp(_zoom + (ReferenceEquals(sender, ZoomInButton) ? 0.1 : -0.1), 0.8, 1.8);
        ZoomTransform.ScaleX = _zoom;
        ZoomTransform.ScaleY = _zoom;

        var workArea = SystemParameters.WorkArea;
        Width = Math.Min(_baseWidth * _zoom, workArea.Width - 40);
        Height = Math.Min(_baseHeight * _zoom, workArea.Height - 40);
    }

    private void RenderLogic(ParseResult result, string baureihe)
    {
        var sb = new StringBuilder();
        sb.Append("Normalisiert: ").Append(result.Normalized).Append(";\n\n");
        foreach (var v in result.Variants)
        {
            sb.Append("Nr. ").Append(v.Lfd).Append('\n');
            for (int i = 0; i < v.Rows.Count; i++)
            {
                var r = v.Rows[i];
                var vals = r.Tokens.Select(t => ExpressionParser.ValueOf(t, ExpressionParser.TypeOf(t))).ToList();
                sb.Append("  ").Append(i + 1).Append(". ").Append(ExpressionParser.DisplayDefinition(i, r, vals));
                sb.Append(DescribeTokens(r.Tokens, baureihe));
                sb.Append('\n');
            }
            sb.Append('\n');
        }
        LogicBox.Text = sb.ToString().TrimEnd('\n');
    }

    private static string DescribeTokens(List<string> tokens, string baureihe)
    {
        var parts = new List<string>();
        foreach (var t in tokens)
        {
            var typ = ExpressionParser.TypeOf(t);
            if (typ == TokenType.Snr)
            {
                parts.Add("SNR: Schichtnummer-Grenze");
                continue;
            }
            if (typ == TokenType.Ek)
            {
                parts.Add("EK: Empfaenger-Kennzeichen (steuert interne Verteilung, kein Ausstattungscode) - Wert: " + ExpressionParser.ValueOf(t, typ).TrimEnd('*'));
                continue;
            }
            if (typ == TokenType.Br)
            {
                parts.Add("BR: Baureihe (Zeilenkriterium, kein Ausstattungscode) - Wert: " + ExpressionParser.ValueOf(t, typ));
                continue;
            }
            if (typ == TokenType.Bm)
            {
                parts.Add("BM: Baumuster (Struktur-/Modellvariante, kein Ausstattungscode) - Wert: " + ExpressionParser.ValueOf(t, typ).TrimEnd('*'));
                continue;
            }
            var entry = CodeCatalog.TryGet(t);
            if (entry is null) continue;
            var mark = "";
            if (!string.IsNullOrWhiteSpace(baureihe) && !entry.Validities.Any(v => v.Baureihe.Equals(baureihe.Trim(), StringComparison.OrdinalIgnoreCase)))
                mark = " [nicht fuer Baureihe " + baureihe.Trim() + "]";
            parts.Add($"{entry.Code}: {entry.Bezeichnung}{mark}");
        }
        return parts.Count == 0 ? "" : "   //  " + string.Join(" | ", parts);
    }

    private void RenderChecks(List<CheckResult> checks)
    {
        ChecksList.ItemsSource = checks.Select(ToDisplay).ToList();
    }

    private static CheckDisplayItem ToDisplay(CheckResult c) => c.Severity switch
    {
        CheckSeverity.Ok => new CheckDisplayItem(c.Title, c.Detail, "✓", new SolidColorBrush(Color.FromRgb(0x11, 0x63, 0x29))),
        CheckSeverity.Warning => new CheckDisplayItem(c.Title, c.Detail, "⚠", new SolidColorBrush(Color.FromRgb(0x8A, 0x6D, 0x00))),
        _ => new CheckDisplayItem(c.Title, c.Detail, "✗", new SolidColorBrush(Color.FromRgb(0x9B, 0x1C, 0x1C))),
    };

    private sealed record CheckDisplayItem(string Title, string Detail, string Glyph, Brush Brush);
}
