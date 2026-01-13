using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace DidoGest.Core.Entities;

/// <summary>
/// Contratti di assistenza/manutenzione
/// </summary>
public class Contratto
{
    [Key]
    public int Id { get; set; }
    
    [Required]
    [MaxLength(20)]
    public string NumeroContratto { get; set; } = string.Empty;
    
    [Required]
    public int ClienteId { get; set; }
    
    [Required]
    [MaxLength(200)]
    public string Descrizione { get; set; } = string.Empty;
    
    [Required]
    public DateTime DataInizio { get; set; }
    
    public DateTime? DataFine { get; set; }
    
    [Column(TypeName = "decimal(18,2)")]
    public decimal Importo { get; set; }
    
    public int? MonteOreAcquistato { get; set; }
    
    public int? MonteOreResiduo { get; set; }
    
    [Column(TypeName = "decimal(18,2)")]
    public decimal? CostoOrarioExtra { get; set; }
    
    [MaxLength(50)]
    public string? TipoContratto { get; set; } // TEMPO_DETERMINATO, MONTE_ORE
    
    [MaxLength(50)]
    public string? StatoContratto { get; set; } // ATTIVO, SCADUTO, ANNULLATO
    
    [MaxLength(100)]
    public string? FrequenzaFatturazione { get; set; }
    
    public DateTime? ProssimaFatturazione { get; set; }
    
    [MaxLength(1000)]
    public string? Note { get; set; }
    
    [MaxLength(50)]
    public string? UtenteCreazione { get; set; }
    
    public DateTime DataCreazione { get; set; } = DateTime.Now;
    
    // Navigation properties
    public virtual Cliente? Cliente { get; set; }
}
