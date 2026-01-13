using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace DidoGest.Core.Entities;

/// <summary>
/// Righe documento
/// </summary>
public class DocumentoRiga
{
    [Key]
    public int Id { get; set; }
    
    [Required]
    public int DocumentoId { get; set; }
    
    public int NumeroRiga { get; set; }
    
    public int? ArticoloId { get; set; }
    
    [MaxLength(200)]
    public string Descrizione { get; set; } = string.Empty;
    
    [Column(TypeName = "decimal(18,2)")]
    public decimal Quantita { get; set; }
    
    [MaxLength(20)]
    public string? UnitaMisura { get; set; }
    
    [Column(TypeName = "decimal(18,2)")]
    public decimal PrezzoUnitario { get; set; }
    
    [Column(TypeName = "decimal(5,2)")]
    public decimal Sconto1 { get; set; }
    
    [Column(TypeName = "decimal(5,2)")]
    public decimal Sconto2 { get; set; }
    
    [Column(TypeName = "decimal(5,2)")]
    public decimal Sconto3 { get; set; }
    
    [Column(TypeName = "decimal(18,2)")]
    public decimal PrezzoNetto { get; set; }
    
    [Column(TypeName = "decimal(5,2)")]
    public decimal AliquotaIVA { get; set; }
    
    [Column(TypeName = "decimal(18,2)")]
    public decimal Imponibile { get; set; }
    
    [Column(TypeName = "decimal(18,2)")]
    public decimal ImportoIVA { get; set; }
    
    [Column(TypeName = "decimal(18,2)")]
    public decimal Totale { get; set; }
    
    [MaxLength(50)]
    public string? NumeroSerie { get; set; }
    
    [MaxLength(50)]
    public string? Lotto { get; set; }
    
    public bool RigaDescrittiva { get; set; }
    
    [MaxLength(500)]
    public string? Note { get; set; }
    
    // Navigation properties
    public virtual Documento? Documento { get; set; }
    public virtual Articolo? Articolo { get; set; }
    public virtual ICollection<MovimentoMagazzino>? Movimenti { get; set; }
}
