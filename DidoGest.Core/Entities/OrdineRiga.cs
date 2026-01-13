using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace DidoGest.Core.Entities;

/// <summary>
/// Righe ordine
/// </summary>
public class OrdineRiga
{
    [Key]
    public int Id { get; set; }
    
    [Required]
    public int OrdineId { get; set; }
    
    public int NumeroRiga { get; set; }
    
    public int? ArticoloId { get; set; }
    
    [MaxLength(200)]
    public string Descrizione { get; set; } = string.Empty;
    
    [Column(TypeName = "decimal(18,2)")]
    public decimal QuantitaOrdinata { get; set; }
    
    [Column(TypeName = "decimal(18,2)")]
    public decimal QuantitaEvasa { get; set; }
    
    [Column(TypeName = "decimal(18,2)")]
    public decimal QuantitaResiduata => QuantitaOrdinata - QuantitaEvasa;
    
    [MaxLength(20)]
    public string? UnitaMisura { get; set; }
    
    [Column(TypeName = "decimal(18,2)")]
    public decimal PrezzoUnitario { get; set; }
    
    [Column(TypeName = "decimal(5,2)")]
    public decimal Sconto { get; set; }
    
    [Column(TypeName = "decimal(5,2)")]
    public decimal AliquotaIVA { get; set; }
    
    [Column(TypeName = "decimal(18,2)")]
    public decimal Totale { get; set; }
    
    [MaxLength(500)]
    public string? Note { get; set; }
    
    // Navigation properties
    public virtual Ordine? Ordine { get; set; }
    public virtual Articolo? Articolo { get; set; }
}
