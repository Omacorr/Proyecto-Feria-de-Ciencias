# push_new_branch.ps1
# Crea una rama nueva, comitea el avance de hoy (teleport con fade y altura
# fija, puntos que se ocultan/muestran correctamente, y el loop de juego:
# GameManager, menu, victoria y derrota) y la sube a GitHub.

param(
    [string]$BranchName = "teleport-fade-y-gameloop"
)

$ErrorActionPreference = "Stop"
Set-Location $PSScriptRoot

Write-Host "== Limpiando archivos de prueba que no deben ir al repo ==" -ForegroundColor Cyan
Remove-Item "gazetest.txt" -ErrorAction SilentlyContinue

Write-Host "== Estado actual del repo ==" -ForegroundColor Cyan
git status

Write-Host ""
Write-Host "== Cambiando a la rama '$BranchName' ==" -ForegroundColor Cyan
$branchExists = git branch --list $BranchName
if ($branchExists) {
    git checkout $BranchName
} else {
    git checkout -b $BranchName
}

Write-Host "== Agregando archivos ==" -ForegroundColor Cyan
git add -A

Write-Host ""
Write-Host "== Archivos que se van a subir ==" -ForegroundColor Cyan
git status
Write-Host ""

$confirm = Read-Host "Escribi SI para confirmar el commit y push a la rama '$BranchName' (cualquier otra cosa cancela)"
if ($confirm -ne "SI") {
    Write-Host "Cancelado. No se hizo commit ni push." -ForegroundColor Red
    exit 0
}

git commit -m "Teleport con fade y altura fija, puntos que se ocultan/muestran bien, y loop de juego (menu, victoria, derrota)"

Write-Host "== Subiendo la rama a GitHub ==" -ForegroundColor Cyan
git push -u origin $BranchName

if ($LASTEXITCODE -eq 0) {
    Write-Host ""
    Write-Host "Listo! Rama '$BranchName' subida." -ForegroundColor Green
    Write-Host "https://github.com/Omacorr/Proyecto-Feria-de-Ciencias/tree/$BranchName" -ForegroundColor Green
} else {
    Write-Host ""
    Write-Host "El push fallo. Puede que te haya pedido iniciar sesion de nuevo en GitHub." -ForegroundColor Red
}
