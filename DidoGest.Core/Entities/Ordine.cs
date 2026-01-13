using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace DidoGest.Core.Entities;

/// <summary>
/// Ordini clienti e fornitori
/// </summary>
public class Ordine
{
    [Key]
    public int Id { get; set; }
    
    [Required]
    [MaxLength(50)]
    public string TipoOrdine { get; set; } = string.Empty; // CLIENTE, FORNITORE
    
    [Required]
    [MaxLength(20)]
    public string NumeroOrdine { get; set; } = string.Empty;
    
    [Required]
    public DateTime DataOrdine { get; set; } = DateTime.Now;
    
    public DateTime? DataConsegnaPrevista { get; set; }
    
    public int? ClienteId { get; set; }
    
    public int? FornitoreId { get; set; }
    
    [Column(TypeName = "decimal(18,2)")]
    public decimal Imponibile { get; set; }
    
    [Column(TypeName = "decimal(18,2)")]
    public decimal IVA { get; set; }
    
    [Column(TypeName = "decimal(18,2)")]
    public decimal Totale { get; set; }
    
    [MaxLength(50)]
    public string? StatoOrdine { get; set; } // APERTO, PARZIALMENTE_EVASO, EVASO, ANNULLATO
    
    [MaxLength(100)]
    public string? RiferimentoCliente { get; set; }
    
    [MaxLength(1000)]
    public string? Note { get; set; }
    
    [MaxLength(50)]
    public string? UtenteCreazione { get; set; }
    
    public DateTime DataCreazione { get; set; } = DateTime.Now;
    
    public DateTime? DataModifica { get; set; }
    
    // Navigation properties
    public virtual Cliente? Cliente { get; set; }
    public virtual Fornitore? Fornitore { get; set; }
    public virtual ICollection<OrdineRiga>? Righe { get; set; }
}
