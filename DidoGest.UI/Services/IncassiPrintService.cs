using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace DidoGest.UI.Services;

public static class IncassiPrintService
{
    public static void PrintIncassi(Window owner, IEnumerable<object> righePagate, AppSettings settings, DateTime? periodoDa, DateTime? periodoA)
    {
        try
        {
            var rows = (righePagate ?? Enumerable.Empty<object>())
                .Select(r => new RowProxy(r))
                .Where(r => r.Id != 0)
                .ToList();

            if (rows.Count == 0)
            {
                MessageBox.Show(owner, "Nessun incasso da stampare.", "DIDO-GEST", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            var dlg = new PrintDialog();
            if (dlg.ShowDialog() != true)
                return;

            var flow = BuildFlowDocument(rows, settings, periodoDa, periodoA);
            flow.PageHeight = dlg.PrintableAreaHeight;
            flow.PageWidth = dlg.PrintableAreaWidth;
            flow.ColumnWidth = dlg.PrintableAreaWidth;

            var paginator = ((IDocumentPaginatorSource)flow).DocumentPaginator;
            dlg.PrintDocument(paginator, "Incassi periodo");
        }
        catch (Exception ex)
        {
            UiLog.Error("IncassiPrintService.PrintIncassi", ex);
            MessageBox.Show(owner, $"Errore durante la stampa incassi: {ex.Message}", "Errore", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private static FlowDocument BuildFlowDocument(IReadOnlyList<RowProxy> rows, AppSettings settings, DateTime? periodoDa, DateTime? periodoA)
    {
        var fd = new FlowDocument
        {
            PagePadding = new Thickness(40),
            FontSize = 12
        };

        fd.Blocks.Add(new BlockUIContainer(BuildHeader(settings)));

        var periodo = BuildPeriodoLabel(periodoDa, periodoA);
        fd.Blocks.Add(new Paragraph(new Run($"Report incassi{periodo} - {DateTime.Today:dd/MM/yyyy}"))
        {
            FontSize = 14,
            FontWeight = FontWeights.SemiBold,
            Margin = new Thickness(0, 10, 0, 10)
        });

        var ordered = rows
            .OrderBy(r => r.Controparte)
            .ThenBy(r => r.DataPagamento.HasValue ? 0 : 1)
            .ThenByDescending(r => r.DataPagamento)
            .ThenByDescending(r => r.DataDocumento)
            .ToList();

        foreach (var group in ordered.GroupBy(r => r.Controparte))
        {
            var first = group.First();

            var title = new Paragraph
            {
                FontSize = 13,
                FontWeight = FontWeights.Bold,
                Margin = new Thickness(0, 10, 0, 2)
            };
            title.Inlines.Add(new Run(group.Key));
            fd.Blocks.Add(title);

            var contatti = BuildControparteContactsLine(first);
            if (!string.IsNullOrWhiteSpace(contatti))
            {
                fd.Blocks.Add(new Paragraph(new Run(contatti))
                {
                    FontSize = 11,
                    Margin = new Thickness(0, 0, 0, 6)
                });
            }
            else
            {
                fd.Blocks.Add(new Paragraph { Margin = new Thickness(0, 0, 0, 6) });
            }

            fd.Blocks.Add(BuildTable(group.ToList()));

            var subTot = group.Sum(x => x.Importo);
            var sub = new Paragraph
            {
                TextAlignment = TextAlignment.Right,
                Margin = new Thickness(0, 4, 0, 0)
            };
            sub.Inlines.Add(new Run($"Totale cliente: {subTot.ToString("C2", CultureInfo.CurrentCulture)}")
            {
                FontWeight = FontWeights.SemiBold
            });
            fd.Blocks.Add(sub);
        }

        fd.Blocks.Add(new Paragraph(new Run(" ")));

        var tot = ordered.Sum(x => x.Importo);
        var clienti = ordered.Select(x => x.Controparte).Distinct().Count();
        var documenti = ordered.Count;

        var meta = new Paragraph { TextAlignment = TextAlignment.Right, FontSize = 11 };
        meta.Inlines.Add(new Run($"Clienti: {clienti}  -  Documenti: {documenti}"));
        fd.Blocks.Add(meta);

        var totP = new Paragraph { TextAlignment = TextAlignment.Right };
        totP.Inlines.Add(new Run($"Totale complessivo: {tot.ToString("C2", CultureInfo.CurrentCulture)}")
        {
            FontWeight = FontWeights.Bold
        });
        fd.Blocks.Add(totP);

        return fd;
    }

    private static string BuildPeriodoLabel(DateTime? da, DateTime? a)
    {
        if (!da.HasValue && !a.HasValue) return string.Empty;
        if (da.HasValue && a.HasValue) return $" (dal {da:dd/MM/yyyy} al {a:dd/MM/yyyy})";
        if (da.HasValue) return $" (dal {da:dd/MM/yyyy})";
        return $" (fino al {a:dd/MM/yyyy})";
    }

    private static string BuildControparteContactsLine(RowProxy row)
    {
        var parts = new List<string>();
        if (!string.IsNullOrWhiteSpace(row.Telefono)) parts.Add($"Tel. {row.Telefono}");
        if (!string.IsNullOrWhiteSpace(row.Cellulare)) parts.Add($"Cell. {row.Cellulare}");
        if (!string.IsNullOrWhiteSpace(row.Email)) parts.Add(row.Email);
        if (!string.IsNullOrWhiteSpace(row.PEC)) parts.Add($"PEC {row.PEC}");
        return string.Join(" - ", parts);
    }

    private static UIElement BuildHeader(AppSettings settings)
    {
        var grid = new Grid { Margin = new Thickness(0, 0, 0, 10) };

        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

        var logo = TryBuildLogo(settings.LogoStampaPath);
        if (logo != null)
        {
            logo.Margin = new Thickness(0, 0, 15, 0);
            Grid.SetColumn(logo, 0);
            grid.Children.Add(logo);
        }

        var stack = new StackPanel { Orientation = Orientation.Vertical };
        var ragione = string.IsNullOrWhiteSpace(settings.RagioneSociale) ? "DIDO-GEST" : settings.RagioneSociale;
        stack.Children.Add(new TextBlock { Text = ragione, FontSize = 18, FontWeight = FontWeights.Bold });

        var indirizzo = BuildCompanyLine(settings);
        if (!string.IsNullOrWhiteSpace(indirizzo))
            stack.Children.Add(new TextBlock { Text = indirizzo });

        var contatti = BuildContactsLine(settings);
        if (!string.IsNullOrWhiteSpace(contatti))
            stack.Children.Add(new TextBlock { Text = contatti });

        Grid.SetColumn(stack, 1);
        grid.Children.Add(stack);
        return grid;
    }

    private static Image? TryBuildLogo(string? path)
    {
        if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
            return null;

        try
        {
            var bmp = new BitmapImage();
            bmp.BeginInit();
            bmp.CacheOption = BitmapCacheOption.OnLoad;
            bmp.UriSource = new Uri(path, UriKind.Absolute);
            bmp.EndInit();
            bmp.Freeze();

            return new Image
            {
                Source = bmp,
                Width = 140,
                Height = 70,
                Stretch = Stretch.Uniform
            };
        }
        catch
        {
            return null;
        }
    }

    private static string BuildCompanyLine(AppSettings settings)
    {
        var parts = new List<string>();

        if (!string.IsNullOrWhiteSpace(settings.Indirizzo)) parts.Add(settings.Indirizzo);
        var capCity = string.Join(" ", new[] { settings.CAP, settings.Citta }.Where(s => !string.IsNullOrWhiteSpace(s)));
        if (!string.IsNullOrWhiteSpace(capCity)) parts.Add(capCity);
        if (!string.IsNullOrWhiteSpace(settings.Provincia)) parts.Add($"({settings.Provincia})");

        var ids = new List<string>();
        if (!string.IsNullOrWhiteSpace(settings.PartitaIva)) ids.Add($"P.IVA {settings.PartitaIva}");
        if (!string.IsNullOrWhiteSpace(settings.CodiceFiscale)) ids.Add($"CF {settings.CodiceFiscale}");

        var line = string.Join(" - ", parts);
        if (ids.Count > 0)
        {
            if (string.IsNullOrWhiteSpace(line))
                line = string.Join(" - ", ids);
            else
                line = line + " - " + string.Join(" - ", ids);
        }

        return line;
    }

    private static string BuildContactsLine(AppSettings settings)
    {
        var parts = new List<string>();
        if (!string.IsNullOrWhiteSpace(settings.Telefono)) parts.Add($"Tel. {settings.Telefono}");
        if (!string.IsNullOrWhiteSpace(settings.Email)) parts.Add(settings.Email);
        if (!string.IsNullOrWhiteSpace(settings.PEC)) parts.Add($"PEC {settings.PEC}");
        return string.Join(" - ", parts);
    }

    private static Table BuildTable(IReadOnlyList<RowProxy> rows)
    {
        var table = new Table { CellSpacing = 0 };

        table.Columns.Add(new TableColumn { Width = new GridLength(75) });
        table.Columns.Add(new TableColumn { Width = new GridLength(85) });
        table.Columns.Add(new TableColumn { Width = new GridLength(80) });
        table.Columns.Add(new TableColumn { Width = new GridLength(90) });

        var header = new TableRowGroup();
        var hr = new TableRow();
        hr.Cells.Add(HeaderCell("Incasso"));
        hr.Cells.Add(HeaderCell("Numero"));
        hr.Cells.Add(HeaderCell("Data doc"));
        hr.Cells.Add(HeaderCell("Importo"));
        header.Rows.Add(hr);
        table.RowGroups.Add(header);

        var body = new TableRowGroup();
        foreach (var r in rows)
        {
            var row = new TableRow();
            row.Cells.Add(BodyCell(r.DataPagamento?.ToString("dd/MM/yyyy") ?? string.Empty));
            row.Cells.Add(BodyCell(r.NumeroDocumento));
            row.Cells.Add(BodyCell(r.DataDocumento.ToString("dd/MM/yyyy")));
            row.Cells.Add(BodyCellRight(r.Importo.ToString("C2", CultureInfo.CurrentCulture)));
            body.Rows.Add(row);
        }

        table.RowGroups.Add(body);
        return table;
    }

    private static TableCell HeaderCell(string text)
    {
        return new TableCell(new Paragraph(new Run(text))
        {
            FontWeight = FontWeights.Bold,
            Margin = new Thickness(3)
        });
    }

    private static TableCell BodyCell(string text)
    {
        return new TableCell(new Paragraph(new Run(text)) { Margin = new Thickness(3) });
    }

    private static TableCell BodyCellRight(string text)
    {
        return new TableCell(new Paragraph(new Run(text))
        {
            TextAlignment = TextAlignment.Right,
            Margin = new Thickness(3)
        });
    }

    private sealed class RowProxy
    {
        public int Id { get; }
        public string Controparte { get; }
        public string NumeroDocumento { get; }
        public DateTime DataDocumento { get; }
        public DateTime? DataPagamento { get; }
        public decimal Importo { get; }

        public string? Telefono { get; }
        public string? Cellulare { get; }
        public string? Email { get; }
        public string? PEC { get; }

        public RowProxy(object src)
        {
            var t = src.GetType();

            Id = Get<int>(t, src, "Id");
            Controparte = Get<string>(t, src, "ControparteRagioneSociale") ?? "N/D";
            NumeroDocumento = Get<string>(t, src, "NumeroDocumento") ?? string.Empty;
            DataDocumento = Get<DateTime>(t, src, "DataDocumento");
            DataPagamento = Get<DateTime?>(t, src, "DataPagamento");
            Importo = Get<decimal>(t, src, "TotaleDocumento");

            Telefono = Get<string>(t, src, "ControparteTelefono");
            Cellulare = Get<string>(t, src, "ControparteCellulare");
            Email = Get<string>(t, src, "ControparteEmail");
            PEC = Get<string>(t, src, "ContropartePEC");
        }

        private static T Get<T>(Type t, object src, string prop)
        {
            var pi = t.GetProperty(prop);
            if (pi == null) return default!;
            var val = pi.GetValue(src);
            if (val == null) return default!;

            if (val is T typed)
                return typed;

            var targetType = typeof(T);
            var underlying = Nullable.GetUnderlyingType(targetType);

            try
            {
                if (underlying != null)
                {
                    if (underlying.IsInstanceOfType(val))
                        return (T)Activator.CreateInstance(targetType, val)!;

                    var converted = Convert.ChangeType(val, underlying, CultureInfo.InvariantCulture);
                    return (T)Activator.CreateInstance(targetType, converted)!;
                }

                if (targetType == typeof(string))
                    return (T)(object)(Convert.ToString(val, CultureInfo.CurrentCulture) ?? string.Empty);

                var converted2 = Convert.ChangeType(val, targetType, CultureInfo.InvariantCulture);
                return (T)converted2;
            }
            catch
            {
                return default!;
            }
        }
    }
}
