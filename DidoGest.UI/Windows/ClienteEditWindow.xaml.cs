using System;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using DidoGest.Core.Entities;
using DidoGest.Data;
using DidoGest.UI.Services;
using Microsoft.EntityFrameworkCore;

namespace DidoGest.UI.Windows;

public partial class ClienteEditWindow : Window
{
    private readonly DidoGestDbContext _context;
    private readonly int? _clienteId;
    private Cliente? _cliente;

    public ClienteEditWindow(DidoGestDbContext context, int? clienteId = null)
    {
        InitializeComponent();
        _context = context;
        _clienteId = clienteId;
        
        Loaded += ClienteEditWindow_Loaded;
    }

    private async void ClienteEditWindow_Loaded(object sender, RoutedEventArgs e)
    {
        if (_clienteId.HasValue)
        {
            _cliente = await _context.Clienti.FindAsync(_clienteId.Value);
            if (_cliente != null)
            {
                LoadCliente();
                Title = "Modifica Cliente";
            }
        }
        else
        {
            Title = "Nuovo Cliente";
            TxtCodice.Text = await GeneraNuovoCodice();
        }
    }

    private void LoadCliente()
    {
        if (_cliente == null) return;

        TxtCodice.Text = _cliente.Codice;
        TxtRagioneSociale.Text = _cliente.RagioneSociale;
        TxtPartitaIVA.Text = _cliente.PartitaIVA;
        TxtCodiceFiscale.Text = _cliente.CodiceFiscale;
        TxtIndirizzo.Text = _cliente.Indirizzo;
        TxtCAP.Text = _cliente.CAP;
        TxtCitta.Text = _cliente.Citta;
        TxtProvincia.Text = _cliente.Provincia;
        TxtNazione.Text = _cliente.Nazione;
        TxtTelefono.Text = _cliente.Telefono;
        TxtEmail.Text = _cliente.Email;
        TxtPEC.Text = _cliente.PEC;
        TxtNote.Text = _cliente.Note;
    }

    private async Task<string> GeneraNuovoCodice()
    {
        var ultimoCliente = await _context.Clienti
            .OrderByDescending(c => c.Id)
            .FirstOrDefaultAsync();
        
        int numero = 1;
        if (ultimoCliente != null)
        {
            var codice = ultimoCliente.Codice.Replace("CLI", "");
            if (int.TryParse(codice, out int n))
            {
                numero = n + 1;
            }
        }
        
        return $"CLI{numero:D5}";
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
            if (_cliente == null)
            {
                _cliente = new Cliente();
                _context.Clienti.Add(_cliente);
            }

            _cliente.Codice = TxtCodice.Text.Trim();
            _cliente.RagioneSociale = TxtRagioneSociale.Text.Trim();
            _cliente.PartitaIVA = TxtPartitaIVA.Text.Trim();
            _cliente.CodiceFiscale = TxtCodiceFiscale.Text.Trim();
            _cliente.Indirizzo = TxtIndirizzo.Text.Trim();
            _cliente.CAP = TxtCAP.Text.Trim();
            _cliente.Citta = TxtCitta.Text.Trim();
            _cliente.Provincia = TxtProvincia.Text.Trim();
            _cliente.Nazione = TxtNazione.Text.Trim();
            _cliente.Telefono = TxtTelefono.Text.Trim();
            _cliente.Email = TxtEmail.Text.Trim();
            _cliente.PEC = TxtPEC.Text.Trim();
            _cliente.Note = (TxtNote.Text ?? string.Empty).Trim();
            // CodiceDestinatario e IsAttivo non presenti nel modello base

            await _context.SaveChangesAsync();
            
            DialogResult = true;
            Close();
        }
        catch (Exception ex)
        {
            UiLog.Error("ClienteEditWindow.BtnSalva_Click", ex);
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
