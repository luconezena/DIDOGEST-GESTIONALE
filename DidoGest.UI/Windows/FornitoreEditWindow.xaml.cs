using System;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using DidoGest.Core.Entities;
using DidoGest.Data;
using DidoGest.UI.Services;
using Microsoft.EntityFrameworkCore;

namespace DidoGest.UI.Windows;

public partial class FornitoreEditWindow : Window
{
    private readonly DidoGestDbContext _context;
    private readonly int? _fornitoreId;
    private Fornitore? _fornitore;

    public FornitoreEditWindow(DidoGestDbContext context, int? fornitoreId = null)
    {
        InitializeComponent();
        _context = context;
        _fornitoreId = fornitoreId;
        
        Loaded += FornitoreEditWindow_Loaded;
    }

    private async void FornitoreEditWindow_Loaded(object sender, RoutedEventArgs e)
    {
        if (_fornitoreId.HasValue)
        {
            _fornitore = await _context.Fornitori.FindAsync(_fornitoreId.Value);
            if (_fornitore != null)
            {
                LoadFornitore();
                Title = "Modifica Fornitore";
            }
        }
        else
        {
            Title = "Nuovo Fornitore";
            TxtCodice.Text = await GeneraNuovoCodice();
        }
    }

    private void LoadFornitore()
    {
        if (_fornitore == null) return;

        TxtCodice.Text = _fornitore.Codice;
        TxtRagioneSociale.Text = _fornitore.RagioneSociale;
        TxtPartitaIVA.Text = _fornitore.PartitaIVA;
        TxtIndirizzo.Text = _fornitore.Indirizzo;
        TxtCAP.Text = _fornitore.CAP;
        TxtCitta.Text = _fornitore.Citta;
        TxtProvincia.Text = _fornitore.Provincia;
        TxtTelefono.Text = _fornitore.Telefono;
        TxtEmail.Text = _fornitore.Email;
    }

    private async Task<string> GeneraNuovoCodice()
    {
        var ultimoFornitore = await _context.Fornitori
            .OrderByDescending(f => f.Id)
            .FirstOrDefaultAsync();
        
        int numero = 1;
        if (ultimoFornitore != null)
        {
            var codice = ultimoFornitore.Codice.Replace("FOR", "");
            if (int.TryParse(codice, out int n))
            {
                numero = n + 1;
            }
        }
        
        return $"FOR{numero:D5}";
    }

    private async void BtnSalva_Click(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(TxtCodice.Text) || string.IsNullOrWhiteSpace(TxtRagioneSociale.Text))
        {
            MessageBox.Show("Codice e Ragione Sociale sono obbligatori", "Attenzione", 
                MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        try
        {
            if (_fornitore == null)
            {
                _fornitore = new Fornitore();
                _context.Fornitori.Add(_fornitore);
            }

            _fornitore.Codice = TxtCodice.Text.Trim();
            _fornitore.RagioneSociale = TxtRagioneSociale.Text.Trim();
            _fornitore.PartitaIVA = TxtPartitaIVA.Text.Trim();
            _fornitore.Indirizzo = TxtIndirizzo.Text.Trim();
            _fornitore.CAP = TxtCAP.Text.Trim();
            _fornitore.Citta = TxtCitta.Text.Trim();
            _fornitore.Provincia = TxtProvincia.Text.Trim();
            _fornitore.Telefono = TxtTelefono.Text.Trim();
            _fornitore.Email = TxtEmail.Text.Trim();

            await _context.SaveChangesAsync();
            
            DialogResult = true;
            Close();
        }
        catch (Exception ex)
        {
            UiLog.Error("FornitoreEditWindow.BtnSalva_Click", ex);
            MessageBox.Show($"Errore salvataggio: {ex.Message}", "Errore", 
                MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void BtnAnnulla_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }
}
