using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;

namespace DidoGest.UI.Services;

public static class SollecitiPrintService
{
    public static void PrintSolleciti(Window owner, IEnumerable<object> righe, AppSettings settings)
    {
        // Supporta direttamente la lista tipizzata della view, senza introdurre dipendenze circolari.
        var rows = righe
            .Select(r => new RowProxy(r))
            .Where(r => r.Id != 0)
            .ToList();

        var dlg = new PrintDialog();
        if (dlg.ShowDialog() != true)
            return;

        var flow = BuildFlowDocument(rows, settings);
        flow.PageHeight = dlg.PrintableAreaHeight;
        flow.PageWidth = dlg.PrintableAreaWidth;
        flow.ColumnWidth = dlg.PrintableAreaWidth;

        var paginator = ((IDocumentPaginatorSource)flow).DocumentPaginator;
        dlg.PrintDocument(paginator, "Solleciti Incassi");
    }

    private static FlowDocument BuildFlowDocument(IReadOnlyList<RowProxy> rows, AppSettings settings)
    {
        var fd = new FlowDocument
        {
            PagePadding = new Thickness(40),
            FontSize = 12
        };

        fd.Blocks.Add(new BlockUIContainer(BuildHeader(settings)));
        fd.Blocks.Add(new Paragraph(new Run($"Report solleciti incassi - {DateTime.Today:dd/MM/yyyy}"))
        {
            FontSize = 14,
            FontWeight = FontWeights.SemiBold,
            Margin = new Thickness(0, 10, 0, 10)
        });

        var ordered = rows
            .OrderBy(r => r.Controparte)
            .ThenBy(r => r.Scadenza.HasValue ? 0 : 1)
            .ThenBy(r => r.Scadenza)
            .ThenBy(r => r.DataDocumento)
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
                // Mantieni spaziatura visiva coerente anche senza contatti
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
        var totP = new Paragraph { TextAlignment = TextAlignment.Right };
        totP.Inlines.Add(new Run($"Totale complessivo: {tot.ToString("C2", CultureInfo.CurrentCulture)}")
        {
            FontWeight = FontWeights.Bold
        });
        fd.Blocks.Add(totP);

        return fd;
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
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

        var stack = new StackPanel { Orientation = Orientation.Vertical };
        var ragione = string.IsNullOrWhiteSpace(settings.RagioneSociale) ? "DIDO-GEST" : settings.RagioneSociale;
        stack.Children.Add(new TextBlock { Text = ragione, FontSize = 18, FontWeight = FontWeights.Bold });

        var indirizzo = BuildCompanyLine(settings);
        if (!string.IsNullOrWhiteSpace(indirizzo))
            stack.Children.Add(new TextBlock { Text = indirizzo });

        var contatti = BuildContactsLine(settings);
        if (!string.IsNullOrWhiteSpace(contatti))
            stack.Children.Add(new TextBlock { Text = contatti });

        Grid.SetColumn(stack, 0);
        grid.Children.Add(stack);
        return grid;
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

        table.Columns.Add(new TableColumn { Width = new GridLength(70) });
        table.Columns.Add(new TableColumn { Width = new GridLength(60) });
        table.Columns.Add(new TableColumn { Width = new GridLength(85) });
        table.Columns.Add(new TableColumn { Width = new GridLength(80) });
        table.Columns.Add(new TableColumn { Width = new GridLength(1, GridUnitType.Star) });
        table.Columns.Add(new TableColumn { Width = new GridLength(90) });

        var header = new TableRowGroup();
        var hr = new TableRow();
        hr.Cells.Add(HeaderCell("Scad."));
        hr.Cells.Add(HeaderCell("Gg"));
        hr.Cells.Add(HeaderCell("Numero"));
        hr.Cells.Add(HeaderCell("Data"));
        hr.Cells.Add(HeaderCell("Stato"));
        hr.Cells.Add(HeaderCell("Importo"));
        header.Rows.Add(hr);
        table.RowGroups.Add(header);

        var body = new TableRowGroup();
        foreach (var r in rows)
        {
            var row = new TableRow();
            row.Cells.Add(BodyCell(r.Scadenza?.ToString("dd/MM/yyyy") ?? string.Empty));
            row.Cells.Add(BodyCellRight(r.GiorniAllaScadenza?.ToString(CultureInfo.CurrentCulture) ?? string.Empty));
            row.Cells.Add(BodyCell(r.NumeroDocumento));
            row.Cells.Add(BodyCell(r.DataDocumento.ToString("dd/MM/yyyy")));
            row.Cells.Add(BodyCell(r.Stato));
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
        })
        ;
    }

    private static TableCell BodyCell(string text)
    {
        return new TableCell(new Paragraph(new Run(text)) { Margin = new Thickness(3) })
        ;
    }

    private static TableCell BodyCellRight(string text)
    {
        return new TableCell(new Paragraph(new Run(text))
        {
            TextAlignment = TextAlignment.Right,
            Margin = new Thickness(3)
        })
        ;
    }

    private sealed class RowProxy
    {
        public int Id { get; }
        public string Controparte { get; }
        public string NumeroDocumento { get; }
        public DateTime DataDocumento { get; }
        public DateTime? Scadenza { get; }
        public int? GiorniAllaScadenza { get; }
        public string Stato { get; }
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
            Scadenza = Get<DateTime?>(t, src, "DataScadenzaPagamento");
            GiorniAllaScadenza = Get<int?>(t, src, "GiorniAllaScadenza");
            Stato = Get<string>(t, src, "StatoPagamento") ?? string.Empty;
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
