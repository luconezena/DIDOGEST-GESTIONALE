using Microsoft.EntityFrameworkCore;
using DidoGest.Core.Entities;

namespace DidoGest.Data;

/// <summary>
/// Database context per DIDO-GEST
/// </summary>
public class DidoGestDbContext : DbContext
{
    public DidoGestDbContext(DbContextOptions<DidoGestDbContext> options) : base(options)
    {
    }

    // DbSets
    public DbSet<Cliente> Clienti { get; set; }
    public DbSet<Fornitore> Fornitori { get; set; }
    public DbSet<Articolo> Articoli { get; set; }
    public DbSet<Magazzino> Magazzini { get; set; }
    public DbSet<GiacenzaMagazzino> GiacenzeMagazzino { get; set; }
    public DbSet<MovimentoMagazzino> MovimentiMagazzino { get; set; }
    public DbSet<Listino> Listini { get; set; }
    public DbSet<ArticoloListino> ArticoliListino { get; set; }
    public DbSet<Agente> Agenti { get; set; }
    public DbSet<Documento> Documenti { get; set; }
    public DbSet<DocumentoRiga> DocumentiRighe { get; set; }
    public DbSet<DocumentoCollegamento> DocumentoCollegamenti { get; set; }
    public DbSet<Ordine> Ordini { get; set; }
    public DbSet<OrdineRiga> OrdiniRighe { get; set; }
    public DbSet<PianoDeiConti> PianiDeiConti { get; set; }
    public DbSet<RegistrazioneContabile> RegistrazioniContabili { get; set; }
    public DbSet<MovimentoContabile> MovimentiContabili { get; set; }
    public DbSet<RegistroIVA> RegistriIVA { get; set; }
    public DbSet<SchedaAssistenza> SchedeAssistenza { get; set; }
    public DbSet<AssistenzaIntervento> AssistenzeInterventi { get; set; }
    public DbSet<Contratto> Contratti { get; set; }
    public DbSet<Cantiere> Cantieri { get; set; }
    public DbSet<CantiereIntervento> CantieriInterventi { get; set; }
    public DbSet<DocumentoArchivio> DocumentiArchivio { get; set; }
    public DbSet<UtenteSistema> UtentiSistema { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Configurazione indici unici
        modelBuilder.Entity<Cliente>()
            .HasIndex(c => c.Codice)
            .IsUnique();

        modelBuilder.Entity<Fornitore>()
            .HasIndex(f => f.Codice)
            .IsUnique();

        modelBuilder.Entity<Articolo>()
            .HasIndex(a => a.Codice)
            .IsUnique();

        modelBuilder.Entity<Magazzino>()
            .HasIndex(m => m.Codice)
            .IsUnique();

        modelBuilder.Entity<Listino>()
            .HasIndex(l => l.Codice)
            .IsUnique();

        modelBuilder.Entity<Agente>()
            .HasIndex(a => a.Codice)
            .IsUnique();

        modelBuilder.Entity<UtenteSistema>()
            .HasIndex(u => u.Username)
            .IsUnique();

        // Configurazione relazioni
        modelBuilder.Entity<Cliente>()
            .HasOne(c => c.Agente)
            .WithMany(a => a.Clienti)
            .HasForeignKey(c => c.AgenteId)
            .OnDelete(DeleteBehavior.SetNull);

        modelBuilder.Entity<Cliente>()
            .HasOne(c => c.Listino)
            .WithMany(l => l.Clienti)
            .HasForeignKey(c => c.ListinoId)
            .OnDelete(DeleteBehavior.SetNull);

        modelBuilder.Entity<Articolo>()
            .HasOne(a => a.FornitorePredefinto)
            .WithMany()
            .HasForeignKey(a => a.FornitorePredefinitoId)
            .OnDelete(DeleteBehavior.SetNull);

        modelBuilder.Entity<GiacenzaMagazzino>()
            .HasOne(g => g.Articolo)
            .WithMany(a => a.Giacenze)
            .HasForeignKey(g => g.ArticoloId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<GiacenzaMagazzino>()
            .HasOne(g => g.Magazzino)
            .WithMany(m => m.Giacenze)
            .HasForeignKey(g => g.MagazzinoId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<MovimentoMagazzino>()
            .HasOne(m => m.Articolo)
            .WithMany(a => a.Movimenti)
            .HasForeignKey(m => m.ArticoloId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<MovimentoMagazzino>()
            .HasOne(m => m.Magazzino)
            .WithMany(mag => mag.Movimenti)
            .HasForeignKey(m => m.MagazzinoId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<ArticoloListino>()
            .HasOne(al => al.Listino)
            .WithMany(l => l.ArticoliListino)
            .HasForeignKey(al => al.ListinoId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<ArticoloListino>()
            .HasOne(al => al.Articolo)
            .WithMany(a => a.Listini)
            .HasForeignKey(al => al.ArticoloId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<Documento>()
            .HasOne(d => d.Cliente)
            .WithMany(c => c.Documenti)
            .HasForeignKey(d => d.ClienteId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<Documento>()
            .HasOne(d => d.Fornitore)
            .WithMany(f => f.Documenti)
            .HasForeignKey(d => d.FornitoreId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<Documento>()
            .HasIndex(d => new { d.TipoDocumento, d.NumeroDocumento })
            .IsUnique();

        modelBuilder.Entity<DocumentoCollegamento>()
            .HasIndex(x => new { x.DocumentoId, x.DocumentoOrigineId })
            .IsUnique();

        modelBuilder.Entity<Documento>()
            .HasOne(d => d.Magazzino)
            .WithMany()
            .HasForeignKey(d => d.MagazzinoId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<DocumentoCollegamento>()
            .HasOne(x => x.Documento)
            .WithMany(d => d.CollegamentiOrigini)
            .HasForeignKey(x => x.DocumentoId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<DocumentoCollegamento>()
            .HasOne(x => x.DocumentoOrigine)
            .WithMany(d => d.CollegamentiComeOrigine)
            .HasForeignKey(x => x.DocumentoOrigineId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<DocumentoRiga>()
            .HasOne(dr => dr.Documento)
            .WithMany(d => d.Righe)
            .HasForeignKey(dr => dr.DocumentoId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<DocumentoRiga>()
            .HasOne(dr => dr.Articolo)
            .WithMany(a => a.RigheDocumento)
            .HasForeignKey(dr => dr.ArticoloId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<Ordine>()
            .HasOne(o => o.Cliente)
            .WithMany(c => c.Ordini)
            .HasForeignKey(o => o.ClienteId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<Ordine>()
            .HasOne(o => o.Fornitore)
            .WithMany(f => f.Ordini)
            .HasForeignKey(o => o.FornitoreId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<OrdineRiga>()
            .HasOne(or => or.Ordine)
            .WithMany(o => o.Righe)
            .HasForeignKey(or => or.OrdineId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<PianoDeiConti>()
            .HasOne(p => p.ContoSuperiore)
            .WithMany(p => p.SottoConti)
            .HasForeignKey(p => p.ContoSuperioreId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<SchedaAssistenza>()
            .HasOne(s => s.Cliente)
            .WithMany()
            .HasForeignKey(s => s.ClienteId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<AssistenzaIntervento>()
            .HasOne(ai => ai.SchedaAssistenza)
            .WithMany(s => s.Interventi)
            .HasForeignKey(ai => ai.SchedaAssistenzaId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<Contratto>()
            .HasOne(c => c.Cliente)
            .WithMany()
            .HasForeignKey(c => c.ClienteId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<Cantiere>()
            .HasOne(c => c.Cliente)
            .WithMany()
            .HasForeignKey(c => c.ClienteId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<CantiereIntervento>()
            .HasOne(ci => ci.Cantiere)
            .WithMany(c => c.Interventi)
            .HasForeignKey(ci => ci.CantiereId)
            .OnDelete(DeleteBehavior.Cascade);

        // Seed data iniziale
        SeedInitialData(modelBuilder);
    }

    private void SeedInitialData(ModelBuilder modelBuilder)
    {
        // Magazzino principale
        modelBuilder.Entity<Magazzino>().HasData(
            new Magazzino
            {
                Id = 1,
                Codice = "MAG01",
                Descrizione = "Magazzino Principale",
                Principale = true,
                Attivo = true,
                DataCreazione = DateTime.Now
            }
        );

        // Listino base
        modelBuilder.Entity<Listino>().HasData(
            new Listino
            {
                Id = 1,
                Codice = "BASE",
                Descrizione = "Listino Base",
                DataInizioValidita = DateTime.Now,
                Attivo = true,
                DataCreazione = DateTime.Now
            }
        );
    }
}
