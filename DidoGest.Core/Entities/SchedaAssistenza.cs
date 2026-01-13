using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace DidoGest.Core.Entities;

/// <summary>
/// Schede assistenza/riparazioni
/// </summary>
public class SchedaAssistenza
{
    [Key]
    public int Id { get; set; }
    
    [Required]
    [MaxLength(20)]
    public string NumeroScheda { get; set; } = string.Empty;
    
    [Required]
    public DateTime DataApertura { get; set; } = DateTime.Now;
    
    public DateTime? DataChiusura { get; set; }
    
    [Required]
    public int ClienteId { get; set; }
    
    [MaxLength(200)]
    public string DescrizioneProdotto { get; set; } = string.Empty;
    
    [MaxLength(50)]
    public string? Matricola { get; set; }
    
    [MaxLength(100)]
    public string? Modello { get; set; }
    
    [MaxLength(500)]
    public string? DifettoDichiarato { get; set; }
    
    [MaxLength(500)]
    public string? DifettoRiscontrato { get; set; }
    
    public bool InGaranzia { get; set; }
    
    [MaxLength(50)]
    public string? StatoLavorazione { get; set; } // APERTA, IN_LAVORAZIONE, SOSPESA, COMPLETATA, CONSEGNATA
    
    [MaxLength(100)]
    public string? TecnicoAssegnato { get; set; }
    
    [Column(TypeName = "decimal(18,2)")]
    public decimal CostoLavorazione { get; set; }
    
    [Column(TypeName = "decimal(18,2)")]
    public decimal CostoMateriali { get; set; }
    
    [Column(TypeName = "decimal(18,2)")]
    public decimal TotaleIntervento { get; set; }
    
    public int? DocumentoCarico { get; set; }
    
    public int? DocumentoScarico { get; set; }
    
    [MaxLength(1000)]
    public string? Note { get; set; }
    
    [MaxLength(50)]
    public string? UtenteCreazione { get; set; }
    
    public DateTime DataCreazione { get; set; } = DateTime.Now;
    
    // Navigation properties
    public virtual Cliente? Cliente { get; set; }
    public virtual ICollection<AssistenzaIntervento>? Interventi { get; set; }
}
