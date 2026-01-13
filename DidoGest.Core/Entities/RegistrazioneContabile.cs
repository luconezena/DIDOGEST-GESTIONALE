using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace DidoGest.Core.Entities;

/// <summary>
/// Registrazioni contabili
/// </summary>
public class RegistrazioneContabile
{
    [Key]
    public int Id { get; set; }
    
    [Required]
    public DateTime DataRegistrazione { get; set; } = DateTime.Now;
    
    [Required]
    [MaxLength(20)]
    public string NumeroRegistrazione { get; set; } = string.Empty;
    
    [MaxLength(50)]
    public string? CausaleContabile { get; set; }
    
    [MaxLength(500)]
    public string? Descrizione { get; set; }
    
    public int? DocumentoId { get; set; }
    
    [Column(TypeName = "decimal(18,2)")]
    public decimal TotaleDare { get; set; }
    
    [Column(TypeName = "decimal(18,2)")]
    public decimal TotaleAvere { get; set; }
    
    [MaxLength(50)]
    public string? UtenteCreazione { get; set; }
    
    public DateTime DataCreazione { get; set; } = DateTime.Now;
    
    // Navigation properties
    public virtual Documento? Documento { get; set; }
    public virtual ICollection<MovimentoContabile>? Movimenti { get; set; }
}
