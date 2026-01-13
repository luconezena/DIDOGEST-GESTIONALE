using System;
using System.Windows;
using DidoGest.Data;
using DidoGest.Data.Services;
using DidoGest.UI.Services;

namespace DidoGest.UI.Windows;

public partial class LoginWindow : Window
{
    private readonly AuthService _auth = new();

    public LoginWindow()
    {
        InitializeComponent();
        Loaded += (_, _) => TxtUser.Focus();
    }

    public bool LoggedIn { get; private set; }

    private void BtnExit_Click(object sender, RoutedEventArgs e)
    {
        LoggedIn = false;
        DialogResult = false;
        Close();
    }

    private void BtnLogin_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            var username = (TxtUser.Text ?? string.Empty).Trim();
            var password = (Pwd.Password ?? string.Empty);

            if (string.IsNullOrWhiteSpace(username) || string.IsNullOrWhiteSpace(password))
            {
                LblError.Text = "Inserisci utente e password.";
                return;
            }

            using var ctx = DidoGestDb.CreateContext();
            var user = _auth.TryLogin(ctx, username, password);
            if (user == null)
            {
                LblError.Text = "Credenziali non valide.";
                return;
            }

            UserSession.Set(user);

            // Non obbligatorio: avviso consigliato se stai usando password di bootstrap.
            var isBootstrapPassword =
                (string.Equals(user.Username, "admin", StringComparison.OrdinalIgnoreCase) &&
                 PasswordHasher.Verify("admin", user.PasswordHash, user.PasswordSalt))
                ||
                (string.Equals(user.Username, "superadmin", StringComparison.OrdinalIgnoreCase) &&
                 PasswordHasher.Verify("superadmin", user.PasswordHash, user.PasswordSalt));

            if (isBootstrapPassword)
            {
                var res = MessageBox.Show(
                    "Stai usando credenziali iniziali (di bootstrap).\n\n" +
                    "È fortemente consigliato cambiare la password (e, se previsto, anche il nome utente) prima di usare il gestionale in modo reale.\n\n" +
                    "Vuoi cambiare la password adesso?",
                    "Sicurezza - Consiglio",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Warning);

                if (res == MessageBoxResult.Yes)
                {
                    var chg = new ChangePasswordWindow(user);
                    chg.Owner = this;
                    chg.ShowDialog();
                }
            }

            LoggedIn = true;
            DialogResult = true;
            Close();
        }
        catch (Exception ex)
        {
            UiLog.Error("LoginWindow.BtnLogin_Click", ex);
            LblError.Text = $"Errore accesso: {ex.Message}";
        }
    }

    private void BtnReset_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            var w = new ResetPasswordWindow();
            w.Owner = this;
            w.ShowDialog();
        }
        catch (Exception ex)
        {
            UiLog.Error("LoginWindow.BtnReset_Click", ex);
        }
    }
}
