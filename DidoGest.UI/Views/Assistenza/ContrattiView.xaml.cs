using System;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using DidoGest.Data;
using DidoGest.UI.Windows;
using Microsoft.EntityFrameworkCore;

namespace DidoGest.UI.Views.Assistenza;

public partial class ContrattiView : UserControl
{
    private readonly DidoGestDbContext _context;

    public ContrattiView()
    {
        InitializeComponent();
        _context = DidoGestDb.CreateContext();
        Loaded += async (_, _) => await LoadData();
    }

    private async Task LoadData()
    {
        var q = _context.Contratti
            .AsNoTracking()
            .Include(c => c.Cliente)
            .OrderByDescending(c => c.DataInizio)
            .AsQueryable();

        var search = (SearchTextBox.Text ?? string.Empty).Trim();
        if (!string.IsNullOrWhiteSpace(search))
        {
            var s = search.ToLower();
            q = q.Where(x =>
                (x.NumeroContratto ?? "").ToLower().Contains(s) ||
                (x.Descrizione ?? "").ToLower().Contains(s) ||
                (x.Cliente != null ? (x.Cliente.RagioneSociale ?? "").ToLower() : "").Contains(s));
        }

        ContrattiDataGrid.ItemsSource = await q.ToListAsync();
    }

    private async void SearchTextBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        if (!IsLoaded) return;
        await LoadData();
    }

    private void BtnNuovo_Click(object sender, RoutedEventArgs e)
    {
        var w = new ContrattoEditWindow(null);
        if (w.ShowDialog() == true)
            _ = LoadData();
    }

    private void BtnModifica_Click(object sender, RoutedEventArgs e)
    {
        if (ContrattiDataGrid.SelectedItem is not DidoGest.Core.Entities.Contratto selected) return;
        var w = new ContrattoEditWindow(selected.Id);
        if (w.ShowDialog() == true)
            _ = LoadData();
    }

    private void ContrattiDataGrid_MouseDoubleClick(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        BtnModifica_Click(sender, e);
    }

    private async void BtnElimina_Click(object sender, RoutedEventArgs e)
    {
        if (ContrattiDataGrid.SelectedItem is not DidoGest.Core.Entities.Contratto selected) return;

        if (MessageBox.Show($"Eliminare il contratto '{selected.NumeroContratto}'?", "Conferma", MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes)
            return;

        var entity = await _context.Contratti.FirstOrDefaultAsync(x => x.Id == selected.Id);
        if (entity == null) return;

        _context.Contratti.Remove(entity);
        await _context.SaveChangesAsync();
        await LoadData();
    }
}
