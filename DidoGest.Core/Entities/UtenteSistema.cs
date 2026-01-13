using System;

namespace DidoGest.Core.Entities;

public class UtenteSistema
{
    public int Id { get; set; }

    public string Username { get; set; } = string.Empty;

    public byte[] PasswordHash { get; set; } = Array.Empty<byte>();

    public byte[] PasswordSalt { get; set; } = Array.Empty<byte>();

    /// <summary>
    /// ADMIN | SUPERADMIN | OPERATORE
    /// </summary>
    public string Ruolo { get; set; } = "OPERATORE";

    public bool Attivo { get; set; } = true;

    public bool MustChangePassword { get; set; } = true;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
