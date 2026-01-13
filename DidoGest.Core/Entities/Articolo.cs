using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace DidoGest.Core.Entities;

/// <summary>
/// Anagrafica articoli di magazzino
/// </summary>
public class Articolo
{
    [Key]
    public int Id { get; set; }
    
    [Required]
    [MaxLength(50)]
    public string Codice { get; set; } = string.Empty;
    
    [Required]
    [MaxLength(200)]
    public string Descrizione { get; set; } = string.Empty;
    
    [MaxLength(500)]
    public string? DescrizioneEstesa { get; set; }
    
    [MaxLength(50)]
    public string? CodiceEAN { get; set; }
    
    [MaxLength(50)]
    public string? CodiceFornitori { get; set; }
    
    [MaxLength(50)]
    public string? UnitaMisura { get; set; }
    
    [Column(TypeName = "decimal(18,2)")]
    public decimal PrezzoAcquisto { get; set; }
    
    [Column(TypeName = "decimal(18,2)")]
    public decimal PrezzoVendita { get; set; }
    
    [Column(TypeName = "decimal(5,2)")]
    public decimal AliquotaIVA { get; set; }
    
    [Column(TypeName = "decimal(18,2)")]
    public decimal ScortaMinima { get; set; }
    
    [Column(TypeName = "decimal(18,2)")]
    public decimal GiacenzaTotale { get; set; }
    
    public bool GestioneTaglie { get; set; }
    
    public bool GestioneColori { get; set; }
    
    public bool GestioneNumeriSerie { get; set; }
    
    public bool GestioneLotti { get; set; }
    
    public bool ArticoloDiServizio { get; set; }
    
    public bool Attivo { get; set; } = true;
    
    [MaxLength(100)]
    public string? Categoria { get; set; }
    
    [MaxLength(100)]
    public string? Sottocategoria { get; set; }
    
    [MaxLength(100)]
    public string? Marca { get; set; }
    
    [MaxLength(500)]
    public string? Immagine { get; set; }
    
    [Column(TypeName = "decimal(18,3)")]
    public decimal? Peso { get; set; }
    
    [Column(TypeName = "decimal(18,2)")]
    public decimal? Volume { get; set; }
    
    public DateTime DataCreazione { get; set; } = DateTime.Now;
    
    public DateTime? DataModifica { get; set; }
    
    [MaxLength(1000)]
    public string? Note { get; set; }
    
    // Navigation properties
    public virtual ICollection<GiacenzaMagazzino>? Giacenze { get; set; }
    public virtual ICollection<MovimentoMagazzino>? Movimenti { get; set; }
    public virtual ICollection<DocumentoRiga>? RigheDocumento { get; set; }
    public virtual ICollection<ArticoloListino>? Listini { get; set; }
    public virtual int? FornitorePredefinitoId { get; set; }
    public virtual Fornitore? FornitorePredefinto { get; set; }
}
