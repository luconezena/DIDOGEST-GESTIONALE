using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace DidoGest.Core.Entities;

/// <summary>
/// Anagrafica magazzini
/// </summary>
public class Magazzino
{
    [Key]
    public int Id { get; set; }
    
    [Required]
    [MaxLength(50)]
    public string Codice { get; set; } = string.Empty;
    
    [Required]
    [MaxLength(200)]
    public string Descrizione { get; set; } = string.Empty;
    
    [MaxLength(200)]
    public string? Indirizzo { get; set; }
    
    [MaxLength(100)]
    public string? Citta { get; set; }
    
    [MaxLength(10)]
    public string? CAP { get; set; }
    
    [MaxLength(50)]
    public string? Telefono { get; set; }
    
    public bool Principale { get; set; }
    
    public bool Attivo { get; set; } = true;
    
    public DateTime DataCreazione { get; set; } = DateTime.Now;
    
    [MaxLength(500)]
    public string? Note { get; set; }
    
    // Navigation properties
    public virtual ICollection<GiacenzaMagazzino>? Giacenze { get; set; }
    public virtual ICollection<MovimentoMagazzino>? Movimenti { get; set; }
}
