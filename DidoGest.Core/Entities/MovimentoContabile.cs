using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace DidoGest.Core.Entities;

/// <summary>
/// Movimenti contabili (prima nota)
/// </summary>
public class MovimentoContabile
{
    [Key]
    public int Id { get; set; }
    
    [Required]
    public int RegistrazioneId { get; set; }
    
    [Required]
    public int ContoId { get; set; }
    
    [Column(TypeName = "decimal(18,2)")]
    public decimal ImportoDare { get; set; }
    
    [Column(TypeName = "decimal(18,2)")]
    public decimal ImportoAvere { get; set; }
    
    [MaxLength(500)]
    public string? Descrizione { get; set; }
    
    // Navigation properties
    public virtual RegistrazioneContabile? Registrazione { get; set; }
    public virtual PianoDeiConti? Conto { get; set; }
}
