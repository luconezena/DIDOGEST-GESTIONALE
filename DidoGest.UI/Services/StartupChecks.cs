using System;
using System.IO;
using System.Windows;
using DidoGest.Data;

namespace DidoGest.UI.Services;

public static class StartupChecks
{
    public static bool EnsureSqliteDatabaseDirectoryWritableOrShowError()
    {
        var result = SqliteHealthChecks.CheckCurrentDatabase(includeLockCheck: false);
        if (result.Ok)
            return true;

        if (result.Issue == SqliteStartupIssue.DatabaseReadOnly)
        {
            MessageBox.Show(
                "Impossibile avviare l'applicazione: il file del database è in sola lettura.\n\n" +
                $"Database: {result.DatabasePath}\n\n" +
                "Soluzioni:\n" +
                "- Rimuovi l'attributo 'Sola lettura' dal file .db (tasto destro → Proprietà).\n" +
                "- (Versione portatile) Copia la cartella dell'app + database in Documenti/Desktop e riprova.\n" +
                "- Oppure copia il database in una cartella scrivibile (es. Documenti) e aggiorna PercorsoDatabase in DidoGest.settings.json.\n\n" +
                "Nota: se il database è su una chiavetta USB, assicurati che non sia protetta da scrittura.",
                "DIDO-GEST - Errore database",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
            return false;
        }

        if (result.Issue == SqliteStartupIssue.DirectoryNotWritable)
        {
            MessageBox.Show(
                "Impossibile avviare l'applicazione: la cartella del database non è scrivibile.\n\n" +
                $"Database: {result.DatabasePath}\n" +
                $"Cartella: {result.DatabaseDirectory}\n\n" +
                "Soluzioni:\n" +
                "- (Versione portatile) Sposta la cartella dell'app in un percorso scrivibile (es. Desktop/Documenti) e riprova.\n" +
                "- Oppure configura un PercorsoDatabase scrivibile in DidoGest.settings.json (o da Utility → Impostazioni → Database).\n" +
                "- (Versione installabile, futura) I dati andranno in una cartella utente dedicata: evita Program Files per il database.\n\n" +
                $"Dettagli: {result.Details}",
                "DIDO-GEST - Errore database",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
            return false;
        }

        MessageBox.Show(
            "Impossibile verificare l'accesso al database SQLite.\n\n" +
            $"Database: {result.DatabasePath}\n" +
            $"Cartella: {result.DatabaseDirectory}\n\n" +
            $"Dettagli: {result.Details}",
            "DIDO-GEST - Errore database",
            MessageBoxButton.OK,
            MessageBoxImage.Error);

        return false;
    }

    public static bool EnsureSqliteDatabaseNotLockedOrShowError()
    {
        var result = SqliteHealthChecks.CheckCurrentDatabase(includeLockCheck: true);
        if (result.Ok)
            return true;

        if (result.Issue == SqliteStartupIssue.DatabaseLocked)
        {
            MessageBox.Show(
                "Impossibile avviare l'applicazione: il database risulta bloccato (in uso).\n\n" +
                $"Database: {result.DatabasePath}\n\n" +
                "Cause comuni:\n" +
                "- È già aperta un'altra istanza di DIDO-GEST.\n" +
                "- Il file è su cartella sincronizzata (es. OneDrive) o su rete e viene temporaneamente bloccato.\n" +
                "- Antivirus/backup sta scansionando il file.\n\n" +
                "Soluzioni:\n" +
                "- Chiudi eventuali altre istanze e riprova.\n" +
                "- (Versione portatile) Metti app+database in una cartella locale non sincronizzata (es. Documenti) e riprova.\n" +
                "- Oppure sposta il database in una cartella locale non sincronizzata (es. Documenti) e aggiorna PercorsoDatabase.\n\n" +
                $"Dettagli: {result.Details}",
                "DIDO-GEST - Database bloccato",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
            return false;
        }

        // Se fallisce per altri motivi, lasciamo che sia l'altro check (directory/accesso) a gestire il messaggio.
        return true;
    }
}
