using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace DidoGest.Core.Entities;

/// <summary>
/// Archiviazione documentale
/// </summary>
public class DocumentoArchivio
{
    [Key]
    public int Id { get; set; }
    
    [Required]
    [MaxLength(20)]
    public string NumeroProtocollo { get; set; } = string.Empty;
    
    [Required]
    public DateTime DataProtocollo { get; set; } = DateTime.Now;
    
    [Required]
    [MaxLength(200)]
    public string TitoloDocumento { get; set; } = string.Empty;
    
    [MaxLength(100)]
    public string? CategoriaDocumento { get; set; }
    
    [MaxLength(500)]
    public string? Descrizione { get; set; }
    
    [Required]
    [MaxLength(500)]
    public string PercorsoFile { get; set; } = string.Empty;
    
    [MaxLength(50)]
    public string? EstensioneFile { get; set; }
    
    public long? DimensioneFile { get; set; }
    
    public int? ClienteId { get; set; }
    
    public int? FornitoreId { get; set; }
    
    public int? ArticoloId { get; set; }
    
    [MaxLength(50)]
    public string? StatoDocumento { get; set; } // APERTO, CHIUSO, ARCHIVIATO
    
    public DateTime? DataApertura { get; set; }
    
    public DateTime? DataChiusura { get; set; }
    
    [MaxLength(1000)]
    public string? Tags { get; set; }
    
    [MaxLength(50)]
    public string? UtenteCreazione { get; set; }
    
    public DateTime DataCreazione { get; set; } = DateTime.Now;
    
    public DateTime? DataModifica { get; set; }
    
    [MaxLength(500)]
    public string? Note { get; set; }
    
    // Navigation properties
    public virtual Cliente? Cliente { get; set; }
    public virtual Fornitore? Fornitore { get; set; }
    public virtual Articolo? Articolo { get; set; }
}
