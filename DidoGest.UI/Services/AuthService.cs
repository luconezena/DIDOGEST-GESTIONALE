using System;
using System.Linq;
using Microsoft.EntityFrameworkCore;
using DidoGest.Core.Entities;
using DidoGest.Data;
using DidoGest.Data.Services;

namespace DidoGest.UI.Services;

public sealed class AuthService
{
    public void EnsureDefaultUsers(DidoGestDbContext ctx)
    {
        var now = DateTime.UtcNow;

        // Crea utenti di bootstrap se mancanti (non resetta password se esistono già).
        var hasAdmin = ctx.UtentiSistema.AsNoTracking()
            .Any(u => u.Attivo && u.Username.ToLower() == "admin");
        if (!hasAdmin)
        {
            var (adminHash, adminSalt) = PasswordHasher.HashPassword("admin");
            ctx.UtentiSistema.Add(new UtenteSistema
            {
                Username = "admin",
                Ruolo = "ADMIN",
                PasswordHash = adminHash,
                PasswordSalt = adminSalt,
                Attivo = true,
                MustChangePassword = false,
                CreatedAt = now,
                UpdatedAt = now
            });
        }

        // SuperAdmin: account visibile (non backdoor). Credenziali iniziali semplici solo per bootstrap.
        var hasSuperAdmin = ctx.UtentiSistema.AsNoTracking()
            .Any(u => u.Attivo && u.Username.ToLower() == "superadmin");
        if (!hasSuperAdmin)
        {
            var (saHash, saSalt) = PasswordHasher.HashPassword("superadmin");
            ctx.UtentiSistema.Add(new UtenteSistema
            {
                Username = "superadmin",
                Ruolo = "SUPERADMIN",
                PasswordHash = saHash,
                PasswordSalt = saSalt,
                Attivo = true,
                MustChangePassword = false,
                CreatedAt = now,
                UpdatedAt = now
            });
        }

        if (!hasAdmin || !hasSuperAdmin)
            ctx.SaveChanges();
    }

    public UtenteSistema CreateUser(DidoGestDbContext ctx, string username, string password, string ruolo, bool attivo)
    {
        if (ctx == null) throw new ArgumentNullException(nameof(ctx));

        username = (username ?? string.Empty).Trim();
        ruolo = (ruolo ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(username))
            throw new InvalidOperationException("Nome utente obbligatorio.");
        if (string.IsNullOrWhiteSpace(ruolo))
            ruolo = "OPERATORE";

        if (ctx.UtentiSistema.Any(u => u.Username.ToLower() == username.ToLower()))
            throw new InvalidOperationException("Nome utente già esistente.");

        var (hash, salt) = PasswordHasher.HashPassword(password);
        var now = DateTime.UtcNow;

        var u = new UtenteSistema
        {
            Username = username,
            Ruolo = ruolo,
            PasswordHash = hash,
            PasswordSalt = salt,
            Attivo = attivo,
            MustChangePassword = false,
            CreatedAt = now,
            UpdatedAt = now
        };

        ctx.UtentiSistema.Add(u);
        ctx.SaveChanges();
        return u;
    }

    public void UpdateUser(DidoGestDbContext ctx, int userId, string username, string ruolo, bool attivo)
    {
        if (ctx == null) throw new ArgumentNullException(nameof(ctx));

        var u = ctx.UtentiSistema.FirstOrDefault(x => x.Id == userId);
        if (u == null) throw new InvalidOperationException("Utente non trovato.");

        username = (username ?? string.Empty).Trim();
        ruolo = (ruolo ?? string.Empty).Trim();

        if (string.IsNullOrWhiteSpace(username))
            throw new InvalidOperationException("Nome utente obbligatorio.");
        if (string.IsNullOrWhiteSpace(ruolo))
            ruolo = "OPERATORE";

        var exists = ctx.UtentiSistema.Any(x => x.Id != userId && x.Username.ToLower() == username.ToLower());
        if (exists)
            throw new InvalidOperationException("Nome utente già esistente.");

        u.Username = username;
        u.Ruolo = ruolo;
        u.Attivo = attivo;
        u.UpdatedAt = DateTime.UtcNow;
        ctx.SaveChanges();
    }

    public UtenteSistema? TryLogin(DidoGestDbContext ctx, string username, string password)
    {
        var u = ctx.UtentiSistema.FirstOrDefault(x => x.Attivo && x.Username.ToLower() == username.ToLower());
        if (u == null) return null;

        return PasswordHasher.Verify(password, u.PasswordHash, u.PasswordSalt) ? u : null;
    }

    public void ChangePassword(DidoGestDbContext ctx, int userId, string newPassword, bool clearMustChange)
    {
        var u = ctx.UtentiSistema.FirstOrDefault(x => x.Id == userId);
        if (u == null) throw new InvalidOperationException("Utente non trovato.");

        var (hash, salt) = PasswordHasher.HashPassword(newPassword);
        u.PasswordHash = hash;
        u.PasswordSalt = salt;
        if (clearMustChange) u.MustChangePassword = false;
        u.UpdatedAt = DateTime.UtcNow;
        ctx.SaveChanges();
    }

    public void ResetPasswordAsSuperAdmin(DidoGestDbContext ctx, UtenteSistema superAdmin, string targetUsername, string newPassword)
    {
        if (!string.Equals(superAdmin.Ruolo, "SUPERADMIN", StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Permesso negato.");

        var target = ctx.UtentiSistema.FirstOrDefault(x => x.Username.ToLower() == targetUsername.ToLower());
        if (target == null) throw new InvalidOperationException("Utente destinatario non trovato.");

        var (hash, salt) = PasswordHasher.HashPassword(newPassword);
        target.PasswordHash = hash;
        target.PasswordSalt = salt;
        target.MustChangePassword = true;
        target.UpdatedAt = DateTime.UtcNow;
        ctx.SaveChanges();
    }
}
