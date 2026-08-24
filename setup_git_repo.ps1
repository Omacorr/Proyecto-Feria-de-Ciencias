# setup_git_repo.ps1
# Sube el proyecto actual a https://github.com/Omacorr/Proyecto-Feria-de-Ciencias.git
# reemplazando todo lo que habia antes, conservando README.md y .gitignore.

$ErrorActionPreference = "Stop"
$repoUrl = "https://github.com/Omacorr/Proyecto-Feria-de-Ciencias.git"

Set-Location $PSScriptRoot

Write-Host "== Verificando git ==" -ForegroundColor Cyan
git --version
if ($LASTEXITCODE -ne 0) {
    Write-Host "No se encontro git. Instalalo desde https://git-scm.com/download/win y volve a correr este script." -ForegroundColor Red
    exit 1
}

Write-Host "== Descargando README.md y .gitignore actuales del repo ==" -ForegroundColor Cyan
Invoke-WebRequest -Uri "https://raw.githubusercontent.com/Omacorr/Proyecto-Feria-de-Ciencias/main/README.md" -OutFile "README.md"
Invoke-WebRequest -Uri "https://raw.githubusercontent.com/Omacorr/Proyecto-Feria-de-Ciencias/main/.gitignore" -OutFile ".gitignore"

Write-Host "== Agregando exclusiones extra al .gitignore (evita subir archivos de build pesados) ==" -ForegroundColor Cyan
$extra = "`n# Build artifacts pesados generados por Unity`n*_BackUpThisFolder_ButDontShipItWithYourGame/`n*_BurstDebugInformation_DoNotShip/`n*.symbols.zip"
Add-Content ".gitignore" $extra

if (-not (Test-Path ".git")) {
    Write-Host "== Inicializando repositorio git ==" -ForegroundColor Cyan
    git init
} else {
    Write-Host "== Ya existe un repositorio git en esta carpeta, se reutiliza ==" -ForegroundColor Yellow
}

$existingRemote = git remote 2>$null
if ($existingRemote -contains "origin") {
    git remote set-url origin $repoUrl
} else {
    git remote add origin $repoUrl
}

Write-Host "== Agregando archivos al staging ==" -ForegroundColor Cyan
git add -A

Write-Host ""
Write-Host "== Archivos que se van a subir ==" -ForegroundColor Cyan
git status
Write-Host ""
Write-Host "Revisa la lista de arriba: NO deberia aparecer ningun .zip, .apk, ni carpetas *_DoNotShip o *_BackUpThisFolder." -ForegroundColor Yellow
Write-Host ""

$confirm = Read-Host "Escribi SI para continuar con el commit y el push (cualquier otra cosa cancela)"
if ($confirm -ne "SI") {
    Write-Host "Cancelado. No se hizo commit ni push. Podes corregir el .gitignore y volver a correr el script." -ForegroundColor Red
    exit 0
}

Write-Host "== Creando commit ==" -ForegroundColor Cyan
git commit -m "Sube version funcional del proyecto Cardboard VR"

Write-Host "== Subiendo a GitHub (reemplaza todo el contenido anterior del repo) ==" -ForegroundColor Cyan
git branch -M main
git push origin main --force

if ($LASTEXITCODE -eq 0) {
    Write-Host ""
    Write-Host "Listo! Revisa https://github.com/Omacorr/Proyecto-Feria-de-Ciencias" -ForegroundColor Green
} else {
    Write-Host ""
    Write-Host "El push fallo. Es probable que te haya pedido iniciar sesion en GitHub (se abre una ventana o el navegador) - si eso paso, volve a correr el script despues de loguearte." -ForegroundColor Red
}
