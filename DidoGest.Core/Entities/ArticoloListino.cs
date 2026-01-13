using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace DidoGest.Core.Entities;

/// <summary>
/// Prezzi articoli per listino
/// </summary>
public class ArticoloListino
{
    [Key]
    public int Id { get; set; }
    
    [Required]
    public int ListinoId { get; set; }
    
    [Required]
    public int ArticoloId { get; set; }
    
    [Column(TypeName = "decimal(18,2)")]
    public decimal Prezzo { get; set; }
    
    [Column(TypeName = "decimal(5,2)")]
    public decimal ScontoPercentuale { get; set; }
    
    public DateTime DataInizioValidita { get; set; }
    
    public DateTime? DataFineValidita { get; set; }
    
    // Navigation properties
    public virtual Listino? Listino { get; set; }
    public virtual Articolo? Articolo { get; set; }
}
