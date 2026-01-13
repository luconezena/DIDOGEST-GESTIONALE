using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace DidoGest.Core.Entities;

/// <summary>
/// Agenti commerciali
/// </summary>
public class Agente
{
    [Key]
    public int Id { get; set; }
    
    [Required]
    [MaxLength(50)]
    public string Codice { get; set; } = string.Empty;
    
    [Required]
    [MaxLength(100)]
    public string Nome { get; set; } = string.Empty;
    
    [Required]
    [MaxLength(100)]
    public string Cognome { get; set; } = string.Empty;
    
    [MaxLength(50)]
    public string? Telefono { get; set; }
    
    [MaxLength(50)]
    public string? Cellulare { get; set; }
    
    [MaxLength(100)]
    public string? Email { get; set; }
    
    [Column(TypeName = "decimal(5,2)")]
    public decimal PercentualeProvvigione { get; set; }
    
    public bool Attivo { get; set; } = true;
    
    public DateTime DataCreazione { get; set; } = DateTime.Now;
    
    [MaxLength(500)]
    public string? Note { get; set; }
    
    // Navigation properties
    public virtual ICollection<Cliente>? Clienti { get; set; }
}
