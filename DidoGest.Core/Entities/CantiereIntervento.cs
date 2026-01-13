using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace DidoGest.Core.Entities;

/// <summary>
/// Interventi/giornate su cantiere
/// </summary>
public class CantiereIntervento
{
    [Key]
    public int Id { get; set; }
    
    [Required]
    public int CantiereId { get; set; }
    
    [Required]
    public DateTime DataIntervento { get; set; }
    
    [MaxLength(100)]
    public string? Operai { get; set; }
    
    public int? NumeroOperai { get; set; }
    
    public int? OreManodopera { get; set; }
    
    [Column(TypeName = "decimal(18,2)")]
    public decimal CostoManodopera { get; set; }
    
    [Column(TypeName = "decimal(18,2)")]
    public decimal CostoMateriali { get; set; }
    
    [Column(TypeName = "decimal(18,2)")]
    public decimal TotaleCosto { get; set; }
    
    [MaxLength(1000)]
    public string? Descrizione { get; set; }
    
    [MaxLength(500)]
    public string? Note { get; set; }
    
    // Navigation properties
    public virtual Cantiere? Cantiere { get; set; }
}
