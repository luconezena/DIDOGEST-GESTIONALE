using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace DidoGest.Core.Entities;

/// <summary>
/// Anagrafica clienti
/// </summary>
public class Cliente
{
    [Key]
    public int Id { get; set; }
    
    [Required]
    [MaxLength(50)]
    public string Codice { get; set; } = string.Empty;
    
    [Required]
    [MaxLength(200)]
    public string RagioneSociale { get; set; } = string.Empty;
    
    [MaxLength(100)]
    public string? Nome { get; set; }
    
    [MaxLength(100)]
    public string? Cognome { get; set; }
    
    [MaxLength(16)]
    public string? CodiceFiscale { get; set; }
    
    [MaxLength(11)]
    public string? PartitaIVA { get; set; }
    
    [MaxLength(200)]
    public string? Indirizzo { get; set; }
    
    [MaxLength(100)]
    public string? Citta { get; set; }
    
    [MaxLength(10)]
    public string? CAP { get; set; }
    
    [MaxLength(2)]
    public string? Provincia { get; set; }
    
    [MaxLength(50)]
    public string? Nazione { get; set; }
    
    [MaxLength(50)]
    public string? Telefono { get; set; }
    
    [MaxLength(50)]
    public string? Cellulare { get; set; }
    
    [MaxLength(100)]
    public string? Email { get; set; }
    
    [MaxLength(100)]
    public string? PEC { get; set; }
    
    [MaxLength(7)]
    public string? CodiceSDI { get; set; }
    
    [Column(TypeName = "decimal(18,2)")]
    public decimal FidoMassimo { get; set; }
    
    public int? GiorniPagamento { get; set; }
    
    [MaxLength(100)]
    public string? Banca { get; set; }
    
    [MaxLength(27)]
    public string? IBAN { get; set; }
    
    public bool Attivo { get; set; } = true;
    
    public DateTime DataCreazione { get; set; } = DateTime.Now;
    
    public DateTime? DataModifica { get; set; }
    
    [MaxLength(500)]
    public string? Note { get; set; }
    
    // Navigation properties
    public virtual ICollection<Documento>? Documenti { get; set; }
    public virtual ICollection<Ordine>? Ordini { get; set; }
    public virtual int? AgenteId { get; set; }
    public virtual Agente? Agente { get; set; }
    public virtual int? ListinoId { get; set; }
    public virtual Listino? Listino { get; set; }
}
