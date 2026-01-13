using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using DidoGest.Core.Entities;
using DidoGest.Data;
using DidoGest.UI.Services;

namespace DidoGest.UI.Windows;

public partial class UserManagementWindow : Window
{
    private readonly AuthService _auth = new();
    private List<UtenteSistema> _users = new();
    private bool _isNewMode;

    public UserManagementWindow()
    {
        InitializeComponent();
        Loaded += (_, _) => Init();
    }

    private void Init()
    {
        CmbRuolo.ItemsSource = new[] { "OPERATORE", "ADMIN", "SUPERADMIN" };
        CmbRuolo.SelectedIndex = 0;
        ChkAttivo.IsChecked = true;

        RefreshUsers();
        SetModeNew();
    }

    private static bool IsAdminOrSuperAdmin(UtenteSistema? u)
    {
        if (u == null) return false;
        return string.Equals(u.Ruolo, "ADMIN", StringComparison.OrdinalIgnoreCase)
               || string.Equals(u.Ruolo, "SUPERADMIN", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsSuperAdmin(UtenteSistema? u)
        => u != null && string.Equals(u.Ruolo, "SUPERADMIN", StringComparison.OrdinalIgnoreCase);

    private bool CanManageTarget(UtenteSistema current, UtenteSistema? target)
    {
        if (IsSuperAdmin(current)) return true;

        // ADMIN: gestisce solo OPERATORE (e non può toccare admin/superadmin)
        if (string.Equals(current.Ruolo, "ADMIN", StringComparison.OrdinalIgnoreCase))
        {
            if (target == null) return true; // creazione: verrà limitata dal ruolo scelto
            return string.Equals(target.Ruolo, "OPERATORE", StringComparison.OrdinalIgnoreCase);
        }

        return false;
    }

    private bool RoleAllowedForCurrent(UtenteSistema current, string requestedRole)
    {
        if (IsSuperAdmin(current)) return true;
        if (string.Equals(current.Ruolo, "ADMIN", StringComparison.OrdinalIgnoreCase))
            return string.Equals(requestedRole, "OPERATORE", StringComparison.OrdinalIgnoreCase);
        return false;
    }

    private void RefreshUsers()
    {
        using var ctx = DidoGestDb.CreateContext();
        _users = ctx.UtentiSistema
            .OrderBy(u => u.Username)
            .ToList();
        GridUsers.ItemsSource = _users;
    }

    private UtenteSistema? SelectedUser => GridUsers.SelectedItem as UtenteSistema;

    private void SetModeNew()
    {
        _isNewMode = true;
        GridUsers.SelectedItem = null;
        TxtUsername.Text = string.Empty;
        CmbRuolo.SelectedItem = "OPERATORE";
        ChkAttivo.IsChecked = true;
        Pwd1.Password = string.Empty;
        Pwd2.Password = string.Empty;
        LblError.Text = "";
    }

    private void SetModeEdit(UtenteSistema u)
    {
        _isNewMode = false;
        TxtUsername.Text = u.Username;
        CmbRuolo.SelectedItem = u.Ruolo;
        ChkAttivo.IsChecked = u.Attivo;
        Pwd1.Password = string.Empty;
        Pwd2.Password = string.Empty;
        LblError.Text = "";
    }

    private void GridUsers_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
    {
        var u = SelectedUser;
        if (u == null)
            return;
        SetModeEdit(u);
    }

    private void BtnNew_Click(object sender, RoutedEventArgs e)
    {
        SetModeNew();
    }

    private void BtnSave_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            var current = UserSession.CurrentUser;
            if (!IsAdminOrSuperAdmin(current))
            {
                MessageBox.Show("Permesso negato.", "DIDO-GEST", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            var username = (TxtUsername.Text ?? string.Empty).Trim();
            var ruolo = (CmbRuolo.SelectedItem as string ?? "OPERATORE").Trim();
            var attivo = ChkAttivo.IsChecked == true;

            if (!RoleAllowedForCurrent(current!, ruolo))
            {
                LblError.Text = "Non hai permessi per impostare questo ruolo.";
                return;
            }

            if (string.IsNullOrWhiteSpace(username))
            {
                LblError.Text = "Nome utente obbligatorio.";
                return;
            }

            var p1 = Pwd1.Password ?? string.Empty;
            var p2 = Pwd2.Password ?? string.Empty;

            if (!string.IsNullOrEmpty(p1) || !string.IsNullOrEmpty(p2))
            {
                if (p1.Length < 6)
                {
                    LblError.Text = "Password troppo corta (minimo 6 caratteri).";
                    return;
                }

                if (!string.Equals(p1, p2, StringComparison.Ordinal))
                {
                    LblError.Text = "Le password non coincidono.";
                    return;
                }
            }

            using var ctx = DidoGestDb.CreateContext();

            if (_isNewMode)
            {
                if (string.IsNullOrEmpty(p1))
                {
                    LblError.Text = "Per creare un utente serve una password (minimo 6 caratteri).";
                    return;
                }

                if (!CanManageTarget(current!, null))
                {
                    LblError.Text = "Permesso negato.";
                    return;
                }

                _auth.CreateUser(ctx, username, p1, ruolo, attivo);
                MessageBox.Show("Utente creato.", "DIDO-GEST", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            else
            {
                var target = SelectedUser;
                if (target == null)
                {
                    LblError.Text = "Seleziona un utente o premi Nuovo.";
                    return;
                }

                if (!CanManageTarget(current!, target))
                {
                    LblError.Text = "Non hai permessi per modificare questo utente.";
                    return;
                }

                if (current!.Id == target.Id && attivo == false)
                {
                    LblError.Text = "Non puoi disattivare l'utente attualmente loggato.";
                    return;
                }

                _auth.UpdateUser(ctx, target.Id, username, ruolo, attivo);

                if (!string.IsNullOrEmpty(p1))
                {
                    _auth.ChangePassword(ctx, target.Id, p1, clearMustChange: true);
                }

                MessageBox.Show("Modifiche salvate.", "DIDO-GEST", MessageBoxButton.OK, MessageBoxImage.Information);
            }

            RefreshUsers();

            // Se ho modificato l'utente corrente, aggiorna la sessione.
            var refreshedCurrent = ctx.UtentiSistema.FirstOrDefault(u => u.Id == current!.Id);
            if (refreshedCurrent != null)
                UserSession.Set(refreshedCurrent);

            SetModeNew();
        }
        catch (Exception ex)
        {
            UiLog.Error("UserManagementWindow.BtnSave_Click", ex);
            LblError.Text = ex.Message;
        }
    }
}
