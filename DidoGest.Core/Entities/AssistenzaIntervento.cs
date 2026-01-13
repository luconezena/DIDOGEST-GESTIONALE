using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace DidoGest.Core.Entities;

/// <summary>
/// Interventi su schede assistenza
/// </summary>
public class AssistenzaIntervento
{
    [Key]
    public int Id { get; set; }
    
    [Required]
    public int SchedaAssistenzaId { get; set; }
    
    [Required]
    public DateTime DataIntervento { get; set; } = DateTime.Now;
    
    [MaxLength(100)]
    public string? Tecnico { get; set; }
    
    [MaxLength(1000)]
    public string? DescrizioneIntervento { get; set; }
    
    public int? MinutiLavorazione { get; set; }
    
    [Column(TypeName = "decimal(18,2)")]
    public decimal CostoOrario { get; set; }
    
    [Column(TypeName = "decimal(18,2)")]
    public decimal TotaleLavorazione { get; set; }
    
    [MaxLength(500)]
    public string? Note { get; set; }
    
    // Navigation properties
    public virtual SchedaAssistenza? SchedaAssistenza { get; set; }
}
