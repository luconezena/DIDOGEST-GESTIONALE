namespace DidoGest.UI.Services;

public sealed class AppSettings
{
    public string? RagioneSociale { get; set; }
    public string? PartitaIva { get; set; }
    public string? CodiceFiscale { get; set; }
    public string? Indirizzo { get; set; }
    public string? CAP { get; set; }
    public string? Citta { get; set; }
    public string? Provincia { get; set; }
    public string? Telefono { get; set; }
    public string? Email { get; set; }
    public string? PEC { get; set; }
    public string? CodiceSDI { get; set; }

    public string? IBAN { get; set; }
    public string? Banca { get; set; }

    public string? PercorsoDatabase { get; set; }
    /// <summary>
    /// "Sqlite" (default) | "SqlServer"
    /// </summary>
    public string? DatabaseProvider { get; set; }

    /// <summary>
    /// Connection string completa per SQL Server (se DatabaseProvider = SqlServer).
    /// Esempio: Server=SERVER\\SQLEXPRESS;Database=DidoGest;Trusted_Connection=True;TrustServerCertificate=True;
    /// </summary>
    public string? SqlServerConnectionString { get; set; }

    // Config guidata SQL Server (opzionale: serve a generare la connection string)
    public string? SqlServerHost { get; set; }
    public string? SqlServerInstance { get; set; }
    public int? SqlServerPort { get; set; }
    public string? SqlServerDatabase { get; set; }
    /// <summary>
    /// "Sql" | "Windows"
    /// </summary>
    public string? SqlServerAuthMode { get; set; }
    public string? SqlServerUserId { get; set; }
    public string? SqlServerPassword { get; set; }

    public string? PercorsoArchivio { get; set; }

    /// <summary>
    /// Se true (default), i dati DEMO vengono garantiti all'avvio (seed idempotente).
    /// Se false, dopo la rimozione DEMO non verranno ricreati automaticamente.
    /// </summary>
    public bool? EnableDemoData { get; set; }

    public string? LogoStampaPath { get; set; }

    // Fatturazione elettronica (integrazione esterna)
    /// <summary>
    /// Se true, abilita il modulo di Fatturazione Elettronica nel menu.
    /// </summary>
    public bool? EnableFatturazioneElettronica { get; set; }

    /// <summary>
    /// "Commercialista" | "Server"
    /// </summary>
    public string? FeModalitaInvio { get; set; }

    /// <summary>
    /// Cartella dove salvare gli XML da consegnare al commercialista (modalità "Commercialista").
    /// </summary>
    public string? FeCartellaCommercialista { get; set; }

    public string? FeProviderNome { get; set; }
    public string? FeApiUrl { get; set; }
    public string? FeApiKey { get; set; }

    // Firma digitale (integrazione esterna)
    public string? FirmaProviderNome { get; set; }
    public string? FirmaCertificatoPfxPath { get; set; }
    public string? FirmaCertificatoPassword { get; set; }
}
