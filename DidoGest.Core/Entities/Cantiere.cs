using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace DidoGest.Core.Entities;

/// <summary>
/// Cantieri
/// </summary>
public class Cantiere
{
    [Key]
    public int Id { get; set; }
    
    [Required]
    [MaxLength(50)]
    public string CodiceCantiere { get; set; } = string.Empty;
    
    [Required]
    [MaxLength(200)]
    public string Descrizione { get; set; } = string.Empty;
    
    [Required]
    public int ClienteId { get; set; }
    
    [MaxLength(200)]
    public string? Indirizzo { get; set; }
    
    [MaxLength(100)]
    public string? Citta { get; set; }
    
    [Required]
    public DateTime DataInizio { get; set; }
    
    public DateTime? DataFine { get; set; }
    
    [Column(TypeName = "decimal(18,2)")]
    public decimal ImportoPreventivato { get; set; }
    
    [Column(TypeName = "decimal(18,2)")]
    public decimal CostiSostenuti { get; set; }
    
    [Column(TypeName = "decimal(18,2)")]
    public decimal RicaviMaturati { get; set; }
    
    [Column(TypeName = "decimal(18,2)")]
    public decimal Margine => RicaviMaturati - CostiSostenuti;
    
    [MaxLength(50)]
    public string? StatoCantiere { get; set; } // APERTO, IN_CORSO, SOSPESO, COMPLETATO, FATTURATO
    
    [MaxLength(100)]
    public string? ResponsabileCantiere { get; set; }
    
    [MaxLength(1000)]
    public string? Note { get; set; }
    
    [MaxLength(50)]
    public string? UtenteCreazione { get; set; }
    
    public DateTime DataCreazione { get; set; } = DateTime.Now;
    
    // Navigation properties
    public virtual Cliente? Cliente { get; set; }
    public virtual ICollection<CantiereIntervento>? Interventi { get; set; }
}
