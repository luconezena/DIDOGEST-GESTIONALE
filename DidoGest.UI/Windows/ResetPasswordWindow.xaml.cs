using System;
using System.Windows;
using DidoGest.Data;
using DidoGest.UI.Services;

namespace DidoGest.UI.Windows;

public partial class ResetPasswordWindow : Window
{
    private readonly AuthService _auth = new();

    public ResetPasswordWindow()
    {
        InitializeComponent();
    }

    private void BtnClose_Click(object sender, RoutedEventArgs e)
    {
        Close();
    }

    private void BtnReset_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            var su = (TxtSuperUser.Text ?? string.Empty).Trim();
            var sp = PwdSuper.Password ?? string.Empty;
            var target = (TxtTarget.Text ?? string.Empty).Trim();
            var np = PwdNew.Password ?? string.Empty;

            if (string.IsNullOrWhiteSpace(su) || string.IsNullOrWhiteSpace(sp) ||
                string.IsNullOrWhiteSpace(target) || string.IsNullOrWhiteSpace(np))
            {
                LblError.Text = "Compila tutti i campi.";
                return;
            }

            if (np.Length < 6)
            {
                LblError.Text = "Password troppo corta (minimo 6 caratteri).";
                return;
            }

            using var ctx = DidoGestDb.CreateContext();
            var superAdmin = _auth.TryLogin(ctx, su, sp);
            if (superAdmin == null)
            {
                LblError.Text = "Credenziali SuperAdmin non valide.";
                return;
            }

            _auth.ResetPasswordAsSuperAdmin(ctx, superAdmin, target, np);
            MessageBox.Show("Reimpostazione completata. L'utente dovrà cambiare password al primo accesso.",
                "OK", MessageBoxButton.OK, MessageBoxImage.Information);
            Close();
        }
        catch (Exception ex)
        {
            UiLog.Error("ResetPasswordWindow.BtnReset_Click", ex);
            LblError.Text = ex.Message;
        }
    }
}
