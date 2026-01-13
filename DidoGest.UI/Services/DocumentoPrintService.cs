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
using DidoGest.Core.Entities;

namespace DidoGest.UI.Services;

public static class DocumentoPrintService
{
    public static void PrintDocumento(Window owner, Documento doc, IReadOnlyList<DocumentoRiga> righe, AppSettings settings)
    {
        var dlg = new PrintDialog();
        if (dlg.ShowDialog() != true)
            return;

        var flow = BuildFlowDocument(doc, righe, settings);
        flow.PageHeight = dlg.PrintableAreaHeight;
        flow.PageWidth = dlg.PrintableAreaWidth;
        flow.ColumnWidth = dlg.PrintableAreaWidth;

        var paginator = ((IDocumentPaginatorSource)flow).DocumentPaginator;
        dlg.PrintDocument(paginator, $"{doc.TipoDocumento} {doc.NumeroDocumento}");
    }

    private static FlowDocument BuildFlowDocument(Documento doc, IReadOnlyList<DocumentoRiga> righe, AppSettings settings)
    {
        var fd = new FlowDocument
        {
            PagePadding = new Thickness(40),
            FontSize = 12
        };

        fd.Blocks.Add(new BlockUIContainer(BuildHeader(doc, settings)));
        fd.Blocks.Add(new Paragraph(new Run(" ")));

        fd.Blocks.Add(BuildParti(doc));
        fd.Blocks.Add(new Paragraph(new Run(" ")));

        fd.Blocks.Add(BuildRigheTable(righe));
        fd.Blocks.Add(new Paragraph(new Run(" ")));

        fd.Blocks.Add(BuildTotali(doc));

        if (!string.IsNullOrWhiteSpace(doc.Note))
        {
            fd.Blocks.Add(new Paragraph(new Run($"Note: {doc.Note}")));
        }

        return fd;
    }

    private static UIElement BuildHeader(Documento doc, AppSettings settings)
    {
        var grid = new Grid
        {
            Margin = new Thickness(0, 0, 0, 10)
        };

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

        stack.Children.Add(new TextBlock
        {
            Margin = new Thickness(0, 10, 0, 0),
            Text = $"{doc.TipoDocumento} {doc.NumeroDocumento}  -  {doc.DataDocumento:dd/MM/yyyy}",
            FontSize = 16,
            FontWeight = FontWeights.SemiBold
        });

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

    private static Paragraph BuildParti(Documento doc)
    {
        var p = new Paragraph();

        var destinatario = doc.Cliente != null
            ? doc.Cliente.RagioneSociale
            : (doc.Fornitore != null ? doc.Fornitore.RagioneSociale : doc.RagioneSocialeDestinatario);

        p.Inlines.Add(new Run("Destinatario: ") { FontWeight = FontWeights.Bold });
        p.Inlines.Add(new Run(string.IsNullOrWhiteSpace(destinatario) ? "N/D" : destinatario));
        p.Inlines.Add(new LineBreak());

        if (!string.IsNullOrWhiteSpace(doc.IndirizzoDestinatario))
        {
            p.Inlines.Add(new Run(doc.IndirizzoDestinatario));
            p.Inlines.Add(new LineBreak());
        }

        if (!string.IsNullOrWhiteSpace(doc.ModalitaPagamento))
        {
            p.Inlines.Add(new Run("Pagamento: ") { FontWeight = FontWeights.Bold });
            p.Inlines.Add(new Run(doc.ModalitaPagamento));
        }

        return p;
    }

    private static Table BuildRigheTable(IReadOnlyList<DocumentoRiga> righe)
    {
        var table = new Table();
        table.CellSpacing = 0;

        table.Columns.Add(new TableColumn { Width = new GridLength(30) });
        table.Columns.Add(new TableColumn { Width = new GridLength(80) });
        table.Columns.Add(new TableColumn { Width = new GridLength(1, GridUnitType.Star) });
        table.Columns.Add(new TableColumn { Width = new GridLength(70) });
        table.Columns.Add(new TableColumn { Width = new GridLength(55) });
        table.Columns.Add(new TableColumn { Width = new GridLength(80) });
        table.Columns.Add(new TableColumn { Width = new GridLength(80) });

        var header = new TableRowGroup();
        var hr = new TableRow();
        hr.Cells.Add(HeaderCell("#"));
        hr.Cells.Add(HeaderCell("Cod."));
        hr.Cells.Add(HeaderCell("Descrizione"));
        hr.Cells.Add(HeaderCell("Q.tà"));
        hr.Cells.Add(HeaderCell("UM"));
        hr.Cells.Add(HeaderCell("Prezzo"));
        hr.Cells.Add(HeaderCell("Totale"));
        header.Rows.Add(hr);
        table.RowGroups.Add(header);

        var body = new TableRowGroup();
        foreach (var r in righe.OrderBy(x => x.NumeroRiga))
        {
            if (r.RigaDescrittiva)
            {
                var row = new TableRow();
                row.Cells.Add(BodyCell(string.Empty));
                row.Cells.Add(BodyCell(string.Empty));
                var descr = BodyCell(r.Descrizione ?? string.Empty);
                descr.ColumnSpan = 5;
                row.Cells.Add(descr);
                body.Rows.Add(row);
                continue;
            }

            var qta = r.Quantita.ToString("0.##", CultureInfo.CurrentCulture);
            var prezzo = r.PrezzoUnitario.ToString("C2", CultureInfo.CurrentCulture);
            var tot = r.Totale.ToString("C2", CultureInfo.CurrentCulture);

            var row2 = new TableRow();
            row2.Cells.Add(BodyCell(r.NumeroRiga.ToString()));
            var codice = r.Articolo?.Codice ?? (r.ArticoloId?.ToString() ?? string.Empty);
            row2.Cells.Add(BodyCell(codice));
            row2.Cells.Add(BodyCell(r.Descrizione ?? string.Empty));
            row2.Cells.Add(BodyCellRight(qta));
            row2.Cells.Add(BodyCell(r.UnitaMisura ?? string.Empty));
            row2.Cells.Add(BodyCellRight(prezzo));
            row2.Cells.Add(BodyCellRight(tot));
            body.Rows.Add(row2);
        }

        table.RowGroups.Add(body);
        return table;
    }

    private static Paragraph BuildTotali(Documento doc)
    {
        var p = new Paragraph { TextAlignment = TextAlignment.Right };
        p.Inlines.Add(new Run($"Imponibile: {doc.Imponibile.ToString("C2", CultureInfo.CurrentCulture)}"));
        p.Inlines.Add(new LineBreak());
        p.Inlines.Add(new Run($"IVA: {doc.IVA.ToString("C2", CultureInfo.CurrentCulture)}"));
        p.Inlines.Add(new LineBreak());
        p.Inlines.Add(new Run($"Totale: {doc.Totale.ToString("C2", CultureInfo.CurrentCulture)}") { FontWeight = FontWeights.Bold });
        return p;
    }

    private static TableCell HeaderCell(string text)
    {
        var cell = new TableCell(new Paragraph(new Run(text)) { FontWeight = FontWeights.Bold })
        {
            Padding = new Thickness(4),
            BorderBrush = Brushes.Black,
            BorderThickness = new Thickness(0.5)
        };
        return cell;
    }

    private static TableCell BodyCell(string text)
    {
        var cell = new TableCell(new Paragraph(new Run(text)))
        {
            Padding = new Thickness(4),
            BorderBrush = Brushes.Black,
            BorderThickness = new Thickness(0.5)
        };
        return cell;
    }

    private static TableCell BodyCellRight(string text)
    {
        var cell = new TableCell(new Paragraph(new Run(text)) { TextAlignment = TextAlignment.Right })
        {
            Padding = new Thickness(4),
            BorderBrush = Brushes.Black,
            BorderThickness = new Thickness(0.5)
        };
        return cell;
    }
}
