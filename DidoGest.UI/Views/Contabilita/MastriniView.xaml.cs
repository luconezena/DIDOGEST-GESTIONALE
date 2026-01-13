using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using DidoGest.Data;
using DidoGest.UI.Services;
using Microsoft.EntityFrameworkCore;

namespace DidoGest.UI.Views.Contabilita;

public partial class MastriniView : UserControl
{
    private readonly DidoGestDbContext _context;
    private List<Row> _rows = new();

    public MastriniView()
    {
        InitializeComponent();
            _context = DidoGestDb.CreateContext();

        Loaded += MastriniView_Loaded;
    }

    private async void MastriniView_Loaded(object sender, RoutedEventArgs e)
    {
        await LoadConti();
        DpDa.SelectedDate = DateTime.Today.AddMonths(-1);
        DpA.SelectedDate = DateTime.Today;
        await LoadMastrino();
    }

    private async Task LoadConti()
    {
        var conti = await _context.PianiDeiConti
            .AsNoTracking()
            .Where(c => c.Attivo)
            .OrderBy(c => c.Codice)
            .ToListAsync();

        CmbConto.ItemsSource = conti;
        CmbConto.DisplayMemberPath = "Descrizione";
        CmbConto.SelectedIndex = conti.Count > 0 ? 0 : -1;
    }

    private async Task LoadMastrino()
    {
        if (CmbConto.SelectedItem is not DidoGest.Core.Entities.PianoDeiConti conto)
        {
            MastrinoDataGrid.ItemsSource = null;
            return;
        }

        var da = DpDa.SelectedDate;
        var a = DpA.SelectedDate;

        try
        {
            var query = _context.MovimentiContabili
                .AsNoTracking()
                .Include(m => m.Registrazione)
                .Where(m => m.ContoId == conto.Id)
                .AsQueryable();

            if (da.HasValue)
                query = query.Where(m => m.Registrazione!.DataRegistrazione >= da.Value);
            if (a.HasValue)
                query = query.Where(m => m.Registrazione!.DataRegistrazione <= a.Value);

            var list = await query
                .OrderBy(m => m.Registrazione!.DataRegistrazione)
                .ThenBy(m => m.Id)
                .ToListAsync();

            decimal saldo = 0m;
            _rows = new List<Row>();
            foreach (var m in list)
            {
                var data = m.Registrazione?.DataRegistrazione ?? DateTime.MinValue;
                var num = m.Registrazione?.NumeroRegistrazione ?? string.Empty;
                var desc = m.Descrizione ?? m.Registrazione?.Descrizione ?? string.Empty;
                saldo += (m.ImportoDare - m.ImportoAvere);

                _rows.Add(new Row
                {
                    Data = data,
                    NumeroRegistrazione = num,
                    Descrizione = desc,
                    Dare = m.ImportoDare,
                    Avere = m.ImportoAvere,
                    Saldo = saldo
                });
            }

            MastrinoDataGrid.ItemsSource = _rows;

            var totDare = _rows.Sum(r => r.Dare);
            var totAvere = _rows.Sum(r => r.Avere);
            LblDare.Text = totDare.ToString("N2");
            LblAvere.Text = totAvere.ToString("N2");
            LblSaldo.Text = (totDare - totAvere).ToString("N2");
        }
        catch (Exception ex)
        {
            UiLog.Error("MastriniView.LoadMastrino", ex);
            MessageBox.Show($"Errore mastrino: {ex.Message}", "Errore", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private async void BtnAggiorna_Click(object sender, RoutedEventArgs e)
    {
        await LoadMastrino();
    }

    private async void Filter_Changed(object? sender, EventArgs e)
    {
        if (!IsLoaded) return;
        await LoadMastrino();
    }

    private sealed class Row
    {
        public DateTime Data { get; set; }
        public string NumeroRegistrazione { get; set; } = string.Empty;
        public string Descrizione { get; set; } = string.Empty;
        public decimal Dare { get; set; }
        public decimal Avere { get; set; }
        public decimal Saldo { get; set; }
    }
}
