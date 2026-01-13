using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace DidoGest.Core.Entities;

/// <summary>
/// Registri IVA
/// </summary>
public class RegistroIVA
{
    [Key]
    public int Id { get; set; }
    
    [Required]
    [MaxLength(50)]
    public string TipoRegistro { get; set; } = string.Empty; // VENDITE, ACQUISTI, CORRISPETTIVI
    
    [Required]
    public DateTime DataRegistrazione { get; set; }
    
    [Required]
    public int DocumentoId { get; set; }
    
    [Required]
    [MaxLength(20)]
    public string NumeroProtocollo { get; set; } = string.Empty;
    
    [Column(TypeName = "decimal(18,2)")]
    public decimal Imponibile { get; set; }
    
    [Column(TypeName = "decimal(5,2)")]
    public decimal AliquotaIVA { get; set; }
    
    [Column(TypeName = "decimal(18,2)")]
    public decimal ImportoIVA { get; set; }
    
    [Column(TypeName = "decimal(18,2)")]
    public decimal IVADetraibile { get; set; }
    
    [Column(TypeName = "decimal(18,2)")]
    public decimal IVAIndetraibile { get; set; }
    
    public bool EsigibilitaDifferita { get; set; }
    
    public DateTime? DataEsigibilita { get; set; }
    
    [MaxLength(200)]
    public string? Descrizione { get; set; }
    
    // Navigation properties
    public virtual Documento? Documento { get; set; }
}
