#!/bin/bash
# ============================================
# DIDO-GEST - Build Script per Linux/Mac
# ============================================

echo ""
echo "========================================"
echo "  DIDO-GEST - Build e Compilazione"
echo "========================================"
echo ""

# Verifica presenza .NET SDK
echo "[1/6] Verifica .NET SDK..."
if ! command -v dotnet &> /dev/null; then
    echo ""
    echo "[ERRORE] .NET 8.0 SDK non trovato!"
    echo ""
    echo "Scarica e installa .NET 8.0 SDK da:"
    echo "https://dotnet.microsoft.com/download/dotnet/8.0"
    echo ""
    exit 1
fi
echo "   OK - .NET SDK trovato"
echo ""

# Vai alla cartella del progetto
cd "$(dirname "$0")"

# Pulizia build precedenti
echo "[2/6] Pulizia build precedenti..."
rm -rf ./Publish
rm -rf ./DidoGest.UI/bin
rm -rf ./DidoGest.UI/obj
rm -rf ./DidoGest.Core/bin
rm -rf ./DidoGest.Core/obj
rm -rf ./DidoGest.Data/bin
rm -rf ./DidoGest.Data/obj
echo "   OK - Pulizia completata"
echo ""

# Restore pacchetti NuGet
echo "[3/6] Download dipendenze NuGet..."
dotnet restore
if [ $? -ne 0 ]; then
    echo "   ERRORE durante il restore!"
    exit 1
fi
echo "   OK - Dipendenze scaricate"
echo ""

# Build del progetto
echo "[4/6] Compilazione del progetto..."
dotnet build --configuration Release --no-restore
if [ $? -ne 0 ]; then
    echo "   ERRORE durante la compilazione!"
    exit 1
fi
echo "   OK - Compilazione completata"
echo ""

# Smoke test (affidabilità): schema + movimenti + incassi + numerazioni
echo "[5/6] Smoke test automatico..."
dotnet run --project ./Tools/DbSmokeTest/DbSmokeTest.csproj -c Release
if [ $? -ne 0 ]; then
    echo "   ERRORE durante lo smoke test!"
    exit 1
fi
echo "   OK - Smoke test completato"
echo ""

# Publish - Crea eseguibile standalone
echo "[6/6] Creazione eseguibile standalone..."
echo "   Questo processo può richiedere alcuni minuti..."
dotnet publish DidoGest.UI/DidoGest.UI.csproj -c Release -r win-x64 --self-contained true -o ./Publish/DidoGest
if [ $? -ne 0 ]; then
    echo "   ERRORE durante la pubblicazione!"
    exit 1
fi
echo "   OK - Eseguibile creato"
echo ""

# Copia file di configurazione
echo "Copia file di configurazione..."
cp -f ./README.md ./Publish/DidoGest/ 2>/dev/null
cp -f ./INSTALL.md ./Publish/DidoGest/ 2>/dev/null
cp -f ./LICENSE.txt ./Publish/DidoGest/ 2>/dev/null
cp -f ./CHANGELOG.md ./Publish/DidoGest/ 2>/dev/null

# Crea cartelle necessarie
mkdir -p ./Publish/DidoGest/FattureElettroniche
mkdir -p ./Publish/DidoGest/Certificati
mkdir -p ./Publish/DidoGest/Archivio
mkdir -p ./Publish/DidoGest/Modelli
mkdir -p ./Publish/DidoGest/Stampe
mkdir -p ./Publish/DidoGest/Logs
mkdir -p ./Publish/DidoGest/Backup
echo "   OK - Struttura cartelle creata"
echo ""

# Crea file README nella cartella di pubblicazione
cat > ./Publish/DidoGest/LEGGIMI.txt << 'EOF'
========================================
  DIDO-GEST v1.0
  Gestionale Professionale
========================================

Per avviare il software:
1. Esegui DidoGest.exe

Al primo avvio:
- Il database SQLite verrà creato automaticamente
- Consulta il file README.md per la configurazione

Requisiti:
- Windows 10 o superiore (64-bit)
- Tutti i runtime sono già inclusi

Cartelle importanti:
- FattureElettroniche/ = XML fatture generate
- Archivio/ = Documenti archiviati
- Backup/ = Backup database
- Logs/ = File di log applicazione

Per supporto: support@didogest.com

Copyright 2025 DIDO Software
========================================
EOF

echo ""
echo "========================================"
echo "  BUILD COMPLETATA CON SUCCESSO!"
echo "========================================"
echo ""
echo "L'applicazione è pronta in:"
echo "$(pwd)/Publish/DidoGest"
echo ""
echo "File eseguibile: DidoGest.exe"
echo ""
echo "Puoi copiare l'intera cartella 'DidoGest' su qualsiasi PC"
echo "con Windows 10/11 e il software funzionerà senza installazione!"
echo ""
