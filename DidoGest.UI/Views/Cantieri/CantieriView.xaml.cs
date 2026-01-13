using System;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using DidoGest.Data;
using DidoGest.UI.Windows;
using Microsoft.EntityFrameworkCore;

namespace DidoGest.UI.Views.Cantieri;

public partial class CantieriView : UserControl
{
    private readonly DidoGestDbContext _context;

    public CantieriView()
    {
        InitializeComponent();
        _context = DidoGestDb.CreateContext();
        Loaded += async (_, _) => await LoadData();
    }

    private async Task LoadData()
    {
        var q = _context.Cantieri
            .AsNoTracking()
            .Include(c => c.Cliente)
            .OrderBy(c => c.CodiceCantiere)
            .AsQueryable();

        var search = (SearchTextBox.Text ?? string.Empty).Trim();
        if (!string.IsNullOrWhiteSpace(search))
        {
            var s = search.ToLower();
            q = q.Where(x =>
                (x.CodiceCantiere ?? "").ToLower().Contains(s) ||
                (x.Descrizione ?? "").ToLower().Contains(s) ||
                (x.Citta ?? "").ToLower().Contains(s) ||
                (x.Cliente != null ? (x.Cliente.RagioneSociale ?? "").ToLower() : "").Contains(s));
        }

        CantieriDataGrid.ItemsSource = await q.ToListAsync();
    }

    private async void SearchTextBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        if (!IsLoaded) return;
        await LoadData();
    }

    private void BtnNuovo_Click(object sender, RoutedEventArgs e)
    {
        var w = new CantiereEditWindow(null);
        if (w.ShowDialog() == true)
            _ = LoadData();
    }

    private void BtnModifica_Click(object sender, RoutedEventArgs e)
    {
        if (CantieriDataGrid.SelectedItem is not DidoGest.Core.Entities.Cantiere selected) return;
        var w = new CantiereEditWindow(selected.Id);
        if (w.ShowDialog() == true)
            _ = LoadData();
    }

    private void CantieriDataGrid_MouseDoubleClick(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        BtnModifica_Click(sender, e);
    }

    private async void BtnElimina_Click(object sender, RoutedEventArgs e)
    {
        if (CantieriDataGrid.SelectedItem is not DidoGest.Core.Entities.Cantiere selected) return;

        if (MessageBox.Show($"Eliminare il cantiere '{selected.CodiceCantiere}'?", "Conferma", MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes)
            return;

        var entity = await _context.Cantieri.FirstOrDefaultAsync(x => x.Id == selected.Id);
        if (entity == null) return;

        _context.Cantieri.Remove(entity);
        await _context.SaveChangesAsync();
        await LoadData();
    }
}
