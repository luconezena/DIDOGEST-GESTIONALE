using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace DidoGest.Core.Entities;

/// <summary>
/// Documenti (DDT, Fatture, Preventivi, Ordini)
/// </summary>
public class Documento
{
    [Key]
    public int Id { get; set; }
    
    [Required]
    [MaxLength(50)]
    public string TipoDocumento { get; set; } = string.Empty; // DDT, FATTURA, FATTURA_ACCOMPAGNATORIA, PREVENTIVO, ORDINE
    
    [Required]
    [MaxLength(20)]
    public string NumeroDocumento { get; set; } = string.Empty;
    
    [Required]
    public DateTime DataDocumento { get; set; } = DateTime.Now;
    
    public int? ClienteId { get; set; }
    
    public int? FornitoreId { get; set; }
    
    [MaxLength(200)]
    public string? RagioneSocialeDestinatario { get; set; }
    
    [MaxLength(500)]
    public string? IndirizzoDestinatario { get; set; }
    
    [Column(TypeName = "decimal(18,2)")]
    public decimal Imponibile { get; set; }
    
    [Column(TypeName = "decimal(18,2)")]
    public decimal IVA { get; set; }
    
    [Column(TypeName = "decimal(18,2)")]
    public decimal Totale { get; set; }
    
    [Column(TypeName = "decimal(18,2)")]
    public decimal ScontoGlobale { get; set; }
    
    [Column(TypeName = "decimal(18,2)")]
    public decimal SpeseAccessorie { get; set; }
    
    [MaxLength(100)]
    public string? ModalitaPagamento { get; set; }
    
    [MaxLength(100)]
    public string? BancaAppoggio { get; set; }
    
    public DateTime? DataScadenzaPagamento { get; set; }
    
    public bool Pagato { get; set; }

    public DateTime? DataPagamento { get; set; }
    
    [MaxLength(50)]
    public string? PartitaIVADestinatario { get; set; }
    
    [MaxLength(50)]
    public string? CodiceFiscaleDestinatario { get; set; }
    
    [MaxLength(7)]
    public string? CodiceSDI { get; set; }
    
    [MaxLength(100)]
    public string? PECDestinatario { get; set; }
    
    public bool FatturaElettronica { get; set; }
    
    [MaxLength(100)]
    public string? NomeFileXML { get; set; }
    
    public bool XMLInviato { get; set; }
    
    public DateTime? DataInvioXML { get; set; }
    
    [MaxLength(50)]
    public string? IdentificativoSDI { get; set; }
    
    [MaxLength(50)]
    public string? StatoFatturaElettronica { get; set; }
    
    public int? DocumentoOriginaleId { get; set; }

    public int MagazzinoId { get; set; } = 1;
    
    [MaxLength(100)]
    public string? CausaleDocumento { get; set; }
    
    [MaxLength(100)]
    public string? AspettoBeni { get; set; }
    
    [MaxLength(100)]
    public string? TrasportoCura { get; set; }
    
    [MaxLength(100)]
    public string? Vettore { get; set; }
    
    public int? NumeroColli { get; set; }
    
    [Column(TypeName = "decimal(18,2)")]
    public decimal? Peso { get; set; }
    
    public bool ReverseCharge { get; set; }
    
    public bool SplitPayment { get; set; }
    
    [MaxLength(1000)]
    public string? Note { get; set; }
    
    [MaxLength(50)]
    public string? UtenteCreazione { get; set; }
    
    public DateTime DataCreazione { get; set; } = DateTime.Now;
    
    public DateTime? DataModifica { get; set; }
    
    // Navigation properties
    public virtual Cliente? Cliente { get; set; }
    public virtual Fornitore? Fornitore { get; set; }
    public virtual Magazzino? Magazzino { get; set; }
    public virtual ICollection<DocumentoRiga>? Righe { get; set; }
    public virtual ICollection<MovimentoMagazzino>? Movimenti { get; set; }
    public virtual Documento? DocumentoOriginale { get; set; }

    // Collegamenti documenti (es. fattura differita da più DDT)
    public virtual ICollection<DocumentoCollegamento>? CollegamentiOrigini { get; set; }
    public virtual ICollection<DocumentoCollegamento>? CollegamentiComeOrigine { get; set; }
}
