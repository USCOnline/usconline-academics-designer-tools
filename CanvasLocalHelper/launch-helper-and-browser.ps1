$projectDirectory = $PSScriptRoot
$repositoryDirectory = Split-Path -Parent $projectDirectory
$indexPath = Join-Path $repositoryDirectory 'index.html'

if (-not (Test-Path -LiteralPath $indexPath)) {
    throw "Could not find index.html at $indexPath"
}

Start-Process -FilePath 'dotnet' `
    -ArgumentList @('run', '--project', (Join-Path $projectDirectory 'CanvasLocalHelper.csproj')) `
    -WorkingDirectory $repositoryDirectory

Start-Process -FilePath $indexPath
Write-Host "Started CanvasLocalHelper in an external window."
Write-Host "Opened $indexPath in the default browser."
