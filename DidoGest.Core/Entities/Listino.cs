using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace DidoGest.Core.Entities;

/// <summary>
/// Listini prezzi
/// </summary>
public class Listino
{
    [Key]
    public int Id { get; set; }
    
    [Required]
    [MaxLength(50)]
    public string Codice { get; set; } = string.Empty;
    
    [Required]
    [MaxLength(200)]
    public string Descrizione { get; set; } = string.Empty;
    
    public DateTime DataInizioValidita { get; set; }
    
    public DateTime? DataFineValidita { get; set; }
    
    public bool Attivo { get; set; } = true;
    
    public DateTime DataCreazione { get; set; } = DateTime.Now;
    
    [MaxLength(500)]
    public string? Note { get; set; }
    
    // Navigation properties
    public virtual ICollection<ArticoloListino>? ArticoliListino { get; set; }
    public virtual ICollection<Cliente>? Clienti { get; set; }
}
