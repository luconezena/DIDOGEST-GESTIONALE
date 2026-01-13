using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace DidoGest.Core.Entities;

/// <summary>
/// Giacenze articoli per magazzino
/// </summary>
public class GiacenzaMagazzino
{
    [Key]
    public int Id { get; set; }
    
    [Required]
    public int ArticoloId { get; set; }
    
    [Required]
    public int MagazzinoId { get; set; }
    
    [Column(TypeName = "decimal(18,2)")]
    public decimal Quantita { get; set; }
    
    [Column(TypeName = "decimal(18,2)")]
    public decimal QuantitaImpegnata { get; set; }
    
    [Column(TypeName = "decimal(18,2)")]
    public decimal QuantitaDisponibile => Quantita - QuantitaImpegnata;
    
    [MaxLength(50)]
    public string? UbicazioneFisica { get; set; }
    
    public DateTime DataUltimoAggiornamento { get; set; } = DateTime.Now;
    
    // Navigation properties
    public virtual Articolo? Articolo { get; set; }
    public virtual Magazzino? Magazzino { get; set; }
}
