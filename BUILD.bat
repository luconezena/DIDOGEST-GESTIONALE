@echo off
REM ============================================
REM DIDO-GEST - Script di Build Automatico
REM ============================================

echo.
echo ========================================
echo   DIDO-GEST - Build e Compilazione
echo ========================================
echo.

REM Verifica presenza .NET SDK
echo [1/6] Verifica .NET SDK...
dotnet --version >nul 2>&1
if errorlevel 1 (
    echo.
    echo [ERRORE] .NET 8.0 SDK non trovato!
    echo.
    echo Scarica e installa .NET 8.0 SDK da:
    echo https://dotnet.microsoft.com/download/dotnet/8.0
    echo.
    echo Premi CTRL+C per annullare, o un tasto per aprire il browser...
    pause >nul
    start https://dotnet.microsoft.com/download/dotnet/8.0
    echo.
    echo Dopo l'installazione, riavvia questo script.
    pause
    exit /b 1
)
echo    OK - .NET SDK trovato
echo.

REM Vai alla cartella del progetto
cd /d "%~dp0"

REM Pulizia build precedenti
echo [2/6] Pulizia build precedenti...
if exist ".\Publish" rmdir /s /q ".\Publish"
if exist ".\DidoGest.UI\bin" rmdir /s /q ".\DidoGest.UI\bin"
if exist ".\DidoGest.UI\obj" rmdir /s /q ".\DidoGest.UI\obj"
if exist ".\DidoGest.Core\bin" rmdir /s /q ".\DidoGest.Core\bin"
if exist ".\DidoGest.Core\obj" rmdir /s /q ".\DidoGest.Core\obj"
if exist ".\DidoGest.Data\bin" rmdir /s /q ".\DidoGest.Data\bin"
if exist ".\DidoGest.Data\obj" rmdir /s /q ".\DidoGest.Data\obj"
echo    OK - Pulizia completata
echo.

REM Restore pacchetti NuGet
echo [3/6] Download dipendenze NuGet...
dotnet restore
if errorlevel 1 (
    echo    ERRORE durante il restore!
    pause
    exit /b 1
)
echo    OK - Dipendenze scaricate
echo.

REM Build del progetto
echo [4/6] Compilazione del progetto...
dotnet build --configuration Release --no-restore
if errorlevel 1 (
    echo    ERRORE durante la compilazione!
    pause
    exit /b 1
)
echo    OK - Compilazione completata
echo.

REM Smoke test (affidabilità): schema + movimenti + incassi + numerazioni
echo [5/6] Smoke test automatico...
dotnet run --project .\Tools\DbSmokeTest\DbSmokeTest.csproj -c Release
if errorlevel 1 (
    echo    ERRORE durante lo smoke test!
    pause
    exit /b 1
)
echo    OK - Smoke test completato
echo.

REM Publish - Crea eseguibile standalone
echo [6/6] Creazione eseguibile standalone...
echo    Questo processo puo' richiedere alcuni minuti...
dotnet publish DidoGest.UI\DidoGest.UI.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=false -o .\Publish\DidoGest
if errorlevel 1 (
    echo    ERRORE durante la pubblicazione!
    pause
    exit /b 1
)
echo    OK - Eseguibile creato
echo.

REM Copia file di configurazione
echo Copia file di configurazione...
copy /Y ".\README.md" ".\Publish\DidoGest\" >nul 2>&1
copy /Y ".\INSTALL.md" ".\Publish\DidoGest\" >nul 2>&1
copy /Y ".\LICENSE.txt" ".\Publish\DidoGest\" >nul 2>&1
copy /Y ".\CHANGELOG.md" ".\Publish\DidoGest\" >nul 2>&1

REM Crea cartelle necessarie
mkdir ".\Publish\DidoGest\FattureElettroniche" >nul 2>&1
mkdir ".\Publish\DidoGest\Certificati" >nul 2>&1
mkdir ".\Publish\DidoGest\Archivio" >nul 2>&1
mkdir ".\Publish\DidoGest\Modelli" >nul 2>&1
mkdir ".\Publish\DidoGest\Stampe" >nul 2>&1
mkdir ".\Publish\DidoGest\Logs" >nul 2>&1
mkdir ".\Publish\DidoGest\Backup" >nul 2>&1
echo    OK - Struttura cartelle creata
echo.

REM Crea file README nella cartella di pubblicazione
echo Creazione file INFO...
(
echo ========================================
echo   DIDO-GEST v1.0
echo   Gestionale Professionale
echo ========================================
echo.
echo Per avviare il software:
echo 1. Esegui DidoGest.exe
echo.
echo Al primo avvio:
echo - Il database SQLite verra' creato automaticamente
echo - Consulta il file README.md per la configurazione
echo.
echo Requisiti:
echo - Windows 10 o superiore ^(64-bit^)
echo - Tutti i runtime sono gia' inclusi
echo.
echo Cartelle importanti:
echo - FattureElettroniche\ = XML fatture generate
echo - Archivio\ = Documenti archiviati
echo - Backup\ = Backup database
echo - Logs\ = File di log applicazione
echo.
echo Per supporto: support@didogest.com
echo.
echo Copyright 2025 DIDO Software
echo ========================================
) > ".\Publish\DidoGest\LEGGIMI.txt"

echo.
echo ========================================
echo   BUILD COMPLETATA CON SUCCESSO!
echo ========================================
echo.
echo L'applicazione e' pronta in:
echo %CD%\Publish\DidoGest
echo.
echo File eseguibile: DidoGest.exe
echo.
echo Puoi copiare l'intera cartella "DidoGest" su qualsiasi PC
echo con Windows 10/11 e il software funzionera' senza installazione!
echo.
echo Per creare un installer professionale, usa Inno Setup:
echo https://jrsoftware.org/isinfo.php
echo.

REM Apri la cartella di output
echo Vuoi aprire la cartella con l'eseguibile? (S/N)
choice /C SN /N /M "Premi S per Si, N per No: "
if errorlevel 2 goto :fine
if errorlevel 1 start explorer "%CD%\Publish\DidoGest"

:fine
echo.
echo Premi un tasto per chiudere...
pause >nul
