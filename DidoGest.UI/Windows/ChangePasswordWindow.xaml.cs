using System;
using System.Windows;
using DidoGest.Core.Entities;
using DidoGest.Data;
using DidoGest.UI.Services;

namespace DidoGest.UI.Windows;

public partial class ChangePasswordWindow : Window
{
    private readonly UtenteSistema _user;
    private readonly AuthService _auth = new();

    public ChangePasswordWindow(UtenteSistema user)
    {
        _user = user;
        InitializeComponent();
        LblTitle.Text = $"Cambia password (utente: {_user.Username})";
    }

    private void BtnCancel_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }

    private void BtnSave_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            var p1 = Pwd1.Password ?? string.Empty;
            var p2 = Pwd2.Password ?? string.Empty;

            if (string.IsNullOrWhiteSpace(p1) || p1.Length < 6)
            {
                LblError.Text = "Password troppo corta (minimo 6 caratteri).";
                return;
            }

            if (!string.Equals(p1, p2, StringComparison.Ordinal))
            {
                LblError.Text = "Le password non coincidono.";
                return;
            }

            using var ctx = DidoGestDb.CreateContext();
            _auth.ChangePassword(ctx, _user.Id, p1, clearMustChange: true);

            // Aggiorna sessione
            var refreshed = ctx.UtentiSistema.Find(_user.Id);
            if (refreshed != null)
                UserSession.Set(refreshed);

            DialogResult = true;
            Close();
        }
        catch (Exception ex)
        {
            UiLog.Error("ChangePasswordWindow.BtnSave_Click", ex);
            LblError.Text = $"Errore: {ex.Message}";
        }
    }
}
