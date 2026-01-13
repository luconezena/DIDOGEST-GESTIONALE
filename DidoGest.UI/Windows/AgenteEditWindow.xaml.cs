using System;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using DidoGest.Core.Entities;
using DidoGest.Data;
using DidoGest.UI.Services;
using Microsoft.EntityFrameworkCore;

namespace DidoGest.UI.Windows;

public partial class AgenteEditWindow : Window
{
    private readonly DidoGestDbContext _context;
    private readonly int? _agenteId;
    private Agente? _agente;

    public AgenteEditWindow(DidoGestDbContext context, int? agenteId = null)
    {
        InitializeComponent();
        _context = context;
        _agenteId = agenteId;

        Loaded += AgenteEditWindow_Loaded;
    }

    private async void AgenteEditWindow_Loaded(object sender, RoutedEventArgs e)
    {
        if (_agenteId.HasValue)
        {
            _agente = await _context.Agenti.FindAsync(_agenteId.Value);
            if (_agente != null)
            {
                LoadAgente();
                Title = "Modifica Agente";
            }
        }
        else
        {
            Title = "Nuovo Agente";
            TxtCodice.Text = await GeneraNuovoCodice();
        }
    }

    private void LoadAgente()
    {
        if (_agente == null) return;

        TxtCodice.Text = _agente.Codice;
        TxtNome.Text = _agente.Nome;
        TxtCognome.Text = _agente.Cognome;
        TxtEmail.Text = _agente.Email;
        TxtTelefono.Text = _agente.Telefono;
        TxtCellulare.Text = _agente.Cellulare;
        TxtPercentuale.Text = _agente.PercentualeProvvigione.ToString("0.00");
        ChkAttivo.IsChecked = _agente.Attivo;
    }

    private async Task<string> GeneraNuovoCodice()
    {
        var ultimo = await _context.Agenti.OrderByDescending(a => a.Id).FirstOrDefaultAsync();

        int numero = 1;
        if (ultimo != null)
        {
            var codice = ultimo.Codice.Replace("AG", "");
            if (int.TryParse(codice, out int n))
            {
                numero = n + 1;
            }
        }

        return $"AG{numero:D5}";
    }

    private async void BtnSalva_Click(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(TxtCodice.Text) || string.IsNullOrWhiteSpace(TxtNome.Text) || string.IsNullOrWhiteSpace(TxtCognome.Text))
        {
            MessageBox.Show("Codice, Nome e Cognome sono obbligatori", "Attenzione",
                MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        if (!decimal.TryParse(TxtPercentuale.Text.Trim(), out var provvigione))
        {
            provvigione = 0m;
        }

        try
        {
            if (_agente == null)
            {
                _agente = new Agente();
                _context.Agenti.Add(_agente);
            }

            _agente.Codice = TxtCodice.Text.Trim();
            _agente.Nome = TxtNome.Text.Trim();
            _agente.Cognome = TxtCognome.Text.Trim();
            _agente.Email = TxtEmail.Text.Trim();
            _agente.Telefono = TxtTelefono.Text.Trim();
            _agente.Cellulare = TxtCellulare.Text.Trim();
            _agente.PercentualeProvvigione = provvigione;
            _agente.Attivo = ChkAttivo.IsChecked == true;

            await _context.SaveChangesAsync();

            DialogResult = true;
            Close();
        }
        catch (Exception ex)
        {
            UiLog.Error("AgenteEditWindow.BtnSalva_Click", ex);
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
