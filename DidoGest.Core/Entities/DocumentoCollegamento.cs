using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace DidoGest.Core.Entities;

/// <summary>
/// Collegamento tra documenti (es. Fattura differita che raggruppa più DDT)
/// </summary>
public class DocumentoCollegamento
{
    [Key]
    public int Id { get; set; }

    /// <summary>
    /// Documento “destinazione” (es. Fattura)
    /// </summary>
    public int DocumentoId { get; set; }

    /// <summary>
    /// Documento “origine” (es. DDT)
    /// </summary>
    public int DocumentoOrigineId { get; set; }

    [ForeignKey(nameof(DocumentoId))]
    public virtual Documento? Documento { get; set; }

    [ForeignKey(nameof(DocumentoOrigineId))]
    public virtual Documento? DocumentoOrigine { get; set; }
}
