using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace DidoGest.Core.Entities;

/// <summary>
/// Movimenti di magazzino (carichi, scarichi, trasferimenti)
/// </summary>
public class MovimentoMagazzino
{
    [Key]
    public int Id { get; set; }
    
    [Required]
    public int ArticoloId { get; set; }
    
    [Required]
    public int MagazzinoId { get; set; }
    
    [Required]
    [MaxLength(20)]
    public string TipoMovimento { get; set; } = string.Empty; // CARICO, SCARICO, TRASFERIMENTO
    
    [Column(TypeName = "decimal(18,2)")]
    public decimal Quantita { get; set; }
    
    [Column(TypeName = "decimal(18,2)")]
    public decimal CostoUnitario { get; set; }
    
    public DateTime DataMovimento { get; set; } = DateTime.Now;
    
    [MaxLength(100)]
    public string? NumeroDocumento { get; set; }
    
    public int? DocumentoId { get; set; }
    
    public int? DocumentoRigaId { get; set; }
    
    [MaxLength(50)]
    public string? NumeroSerie { get; set; }
    
    [MaxLength(50)]
    public string? Lotto { get; set; }
    
    public DateTime? DataScadenza { get; set; }
    
    [MaxLength(200)]
    public string? Causale { get; set; }
    
    [MaxLength(500)]
    public string? Note { get; set; }
    
    [MaxLength(50)]
    public string? UtenteCreazione { get; set; }
    
    // Navigation properties
    public virtual Articolo? Articolo { get; set; }
    public virtual Magazzino? Magazzino { get; set; }
    public virtual Documento? Documento { get; set; }
    public virtual DocumentoRiga? DocumentoRiga { get; set; }
}
