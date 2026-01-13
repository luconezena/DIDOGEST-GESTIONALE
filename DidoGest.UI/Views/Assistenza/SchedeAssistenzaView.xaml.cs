using System;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using DidoGest.Data;
using DidoGest.UI.Windows;
using Microsoft.EntityFrameworkCore;

namespace DidoGest.UI.Views.Assistenza;

public partial class SchedeAssistenzaView : UserControl
{
    private readonly DidoGestDbContext _context;

    public SchedeAssistenzaView()
    {
        InitializeComponent();
        _context = DidoGestDb.CreateContext();
        Loaded += async (_, _) => await LoadData();
    }

    private async Task LoadData()
    {
        var q = _context.SchedeAssistenza
            .AsNoTracking()
            .Include(s => s.Cliente)
            .OrderByDescending(s => s.DataApertura)
            .AsQueryable();

        var search = (SearchTextBox.Text ?? string.Empty).Trim();
        if (!string.IsNullOrWhiteSpace(search))
        {
            var s = search.ToLower();
            q = q.Where(x =>
                (x.NumeroScheda ?? "").ToLower().Contains(s) ||
                (x.DescrizioneProdotto ?? "").ToLower().Contains(s) ||
                (x.DifettoDichiarato ?? "").ToLower().Contains(s) ||
                (x.DifettoRiscontrato ?? "").ToLower().Contains(s) ||
                (x.Cliente != null ? (x.Cliente.RagioneSociale ?? "").ToLower() : "").Contains(s));
        }

        SchedeDataGrid.ItemsSource = await q.ToListAsync();
    }

    private async void SearchTextBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        if (!IsLoaded) return;
        await LoadData();
    }

    private void BtnNuova_Click(object sender, RoutedEventArgs e)
    {
        var w = new SchedaAssistenzaEditWindow(null);
        if (w.ShowDialog() == true)
        {
            _ = LoadData();
        }
    }

    private void BtnModifica_Click(object sender, RoutedEventArgs e)
    {
        if (SchedeDataGrid.SelectedItem is not DidoGest.Core.Entities.SchedaAssistenza selected) return;
        var w = new SchedaAssistenzaEditWindow(selected.Id);
        if (w.ShowDialog() == true)
        {
            _ = LoadData();
        }
    }

    private void SchedeDataGrid_MouseDoubleClick(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        BtnModifica_Click(sender, e);
    }

    private async void BtnElimina_Click(object sender, RoutedEventArgs e)
    {
        if (SchedeDataGrid.SelectedItem is not DidoGest.Core.Entities.SchedaAssistenza selected) return;

        if (MessageBox.Show($"Eliminare la scheda '{selected.NumeroScheda}'?", "Conferma", MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes)
            return;

        var entity = await _context.SchedeAssistenza.FirstOrDefaultAsync(x => x.Id == selected.Id);
        if (entity == null) return;

        _context.SchedeAssistenza.Remove(entity);
        await _context.SaveChangesAsync();
        await LoadData();
    }
}
