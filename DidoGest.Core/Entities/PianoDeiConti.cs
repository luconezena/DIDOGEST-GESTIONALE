using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace DidoGest.Core.Entities;

/// <summary>
/// Piano dei conti
/// </summary>
public class PianoDeiConti
{
    [Key]
    public int Id { get; set; }
    
    [Required]
    [MaxLength(50)]
    public string Codice { get; set; } = string.Empty;
    
    [Required]
    [MaxLength(200)]
    public string Descrizione { get; set; } = string.Empty;
    
    [MaxLength(50)]
    public string? TipoConto { get; set; } // ATTIVO, PASSIVO, COSTI, RICAVI
    
    public int? ContoSuperioreId { get; set; }
    
    public int Livello { get; set; }
    
    public bool ContoFoglia { get; set; }
    
    public bool Attivo { get; set; } = true;
    
    public DateTime DataCreazione { get; set; } = DateTime.Now;
    
    [MaxLength(500)]
    public string? Note { get; set; }
    
    // Navigation properties
    public virtual PianoDeiConti? ContoSuperiore { get; set; }
    public virtual ICollection<PianoDeiConti>? SottoConti { get; set; }
    public virtual ICollection<MovimentoContabile>? Movimenti { get; set; }
}
