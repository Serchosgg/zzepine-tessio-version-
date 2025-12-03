# Script de Compilacion Automatica - GTAV Injector Enhanced
# Ejecuta este script para compilar el proyecto automaticamente

Write-Host "==============================================================" -ForegroundColor Cyan
Write-Host "        GTAV Injector Enhanced - Compilacion Automatica      " -ForegroundColor Cyan
Write-Host "==============================================================" -ForegroundColor Cyan
Write-Host ""

# Verificar si dotnet esta instalado
Write-Host "[1/5] Verificando .NET SDK..." -ForegroundColor Yellow
try {
    $dotnetVersion = dotnet --version
    Write-Host "OK .NET SDK encontrado: v$dotnetVersion" -ForegroundColor Green
}
catch {
    Write-Host "ERROR .NET SDK no encontrado" -ForegroundColor Red
    exit 1
}

Write-Host ""

# Limpiar compilaciones anteriores
Write-Host "[2/5] Limpiando compilaciones anteriores..." -ForegroundColor Yellow
dotnet clean "GTAV-Injector.csproj" --verbosity quiet
if ($LASTEXITCODE -eq 0) {
    Write-Host "OK Limpieza completada" -ForegroundColor Green
}
else {
    Write-Host "WARN Advertencia en limpieza (continuando...)" -ForegroundColor Yellow
}

Write-Host ""

# Restaurar dependencias
Write-Host "[3/5] Restaurando dependencias NuGet..." -ForegroundColor Yellow
dotnet restore "GTAV-Injector.csproj"
if ($LASTEXITCODE -eq 0) {
    Write-Host "OK Dependencias restauradas" -ForegroundColor Green
}
else {
    Write-Host "ERROR Error al restaurar dependencias" -ForegroundColor Red
    exit 1
}

Write-Host ""

# Compilar en modo Release
Write-Host "[4/5] Compilando proyecto (Release)..." -ForegroundColor Yellow
dotnet build "GTAV-Injector.csproj" -c Release --no-restore
if ($LASTEXITCODE -eq 0) {
    Write-Host "OK Compilacion exitosa" -ForegroundColor Green
}
else {
    Write-Host "ERROR Error en la compilacion" -ForegroundColor Red
    exit 1
}

Write-Host ""

# Publicar como ejecutable portable (opcional)
Write-Host "[5/5] Deseas crear un ejecutable portable? (S/N)" -ForegroundColor Yellow
$response = Read-Host
if ($response -eq "S" -or $response -eq "s") {
    Write-Host "Creando ejecutable portable..." -ForegroundColor Yellow
    
    $publishArgs = @("publish", "GTAV-Injector.csproj", "-c", "Release", "-r", "win-x64", "--self-contained", "true", "-p:PublishSingleFile=true", "-p:IncludeNativeLibrariesForSelfExtract=true")
    & dotnet $publishArgs
    
    if ($LASTEXITCODE -eq 0) {
        Write-Host "OK Ejecutable portable creado" -ForegroundColor Green
        $portablePath = "bin\Release\net8.0-windows\win-x64\publish\GTA GG'S Launcher.exe"
        Write-Host "Ubicacion: $portablePath" -ForegroundColor Cyan
    }
}

Write-Host ""
Write-Host "==============================================================" -ForegroundColor Green
Write-Host "              OK COMPILACION COMPLETADA                       " -ForegroundColor Green
Write-Host "==============================================================" -ForegroundColor Green
Write-Host ""

$exePath = "bin\Release\net8.0-windows\GTA GGS Launcher.exe"
Write-Host "Ejecutable generado en:" -ForegroundColor Cyan
Write-Host "   $exePath" -ForegroundColor White
Write-Host ""

Write-Host "Deseas ejecutar la aplicacion ahora? (S/N)" -ForegroundColor Yellow
$runNow = Read-Host
if ($runNow -eq "S" -or $runNow -eq "s") {
    Write-Host "Iniciando GTAV Injector Enhanced..." -ForegroundColor Green
    Start-Process $exePath
}

Write-Host "Presiona cualquier tecla para salir..."
$null = $Host.UI.RawUI.ReadKey("NoEcho,IncludeKeyDown")
