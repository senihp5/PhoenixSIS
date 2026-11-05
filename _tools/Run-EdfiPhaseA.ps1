<#
    Run-EdfiPhaseA.ps1

    One-shot PHX-3.3 Phase A for the SQL VM. Self-contained: copy ONLY
    this file to the VM and run it in an ADMIN PowerShell window:

        .\Run-EdfiPhaseA.ps1

    It will:
      1. Verify the .NET 10 SDK is present
      2. Resolve and download the LATEST 7.3.x
         EdFi.Suite3.RestApi.Databases.Standard.6.1.0 package from the
         Ed-Fi Azure Artifacts feed into C:\Temp\EdFiRebuild
      3. Patch configuration.json (prompts once for the edfi_app
         password; SQL auth, Encrypt=false, EdFi_{0} kept literal,
         InstallType=Sandbox, Engine=SQLServer) with JSON validation,
         and remove configuration.postgreSQL.json
      4. Run Import-Module Deployment.psm1 + Initialize-DeploymentEnvironment

    Afterwards: run Backup-EdfiBaseline.ps1 from the workstation, then
    DELETE C:\Temp\EdFiRebuild (configuration.json holds the password).
#>

[CmdletBinding()]
param(
    [string]$WorkDir = 'C:\Temp\EdFiRebuild',
    [string]$SqlServer = '(local)',
    [string]$LoginName = 'edfi_app',
    [string]$Password
)

$ErrorActionPreference = 'Stop'
[Net.ServicePointManager]::SecurityProtocol += [Net.SecurityProtocolType]::Tls12

$sdks = @(dotnet --list-sdks 2>$null)
if (-not ($sdks | Where-Object { $_ -like '10.*' })) {
    throw '.NET 10 SDK not found. Install it first (July: dotnet-install.ps1 -Channel 10.0 -InstallDir "C:\Program Files\dotnet"), open a NEW shell, rerun.'
}
Write-Host ".NET 10 SDK present." -ForegroundColor Green

if (-not $Password) {
    $secure = Read-Host -Prompt "Password for $LoginName (from C:\temp\edfi-sql.ps1 on the workstation)" -AsSecureString
    $Password = (New-Object System.Management.Automation.PSCredential('x', $secure)).GetNetworkCredential().Password
}
if (-not $Password) { throw 'A password is required.' }

$feed = 'https://pkgs.dev.azure.com/ed-fi-alliance/Ed-Fi-Alliance-OSS/_packaging/EdFi/nuget/v3/flat2/edfi.suite3.restapi.databases.standard.6.1.0'
Write-Host 'Resolving latest 7.3.x package version...' -ForegroundColor Cyan
$idx = Invoke-RestMethod "$feed/index.json"
$v = $idx.versions | Where-Object { $_ -like '7.3*' } | Sort-Object { [version]$_ } | Select-Object -Last 1
if (-not $v) { throw 'No 7.3.x version found on the feed.' }
Write-Host "Using EdFi.Suite3.RestApi.Databases.Standard.6.1.0 $v" -ForegroundColor Green

New-Item -ItemType Directory -Path $WorkDir -Force | Out-Null
$zip = Join-Path $WorkDir "pkg-$v.zip"
$pkgDir = Join-Path $WorkDir "pkg-$v"
if (-not (Test-Path (Join-Path $pkgDir 'Deployment.psm1'))) {
    Write-Host 'Downloading package...' -ForegroundColor Cyan
    Invoke-WebRequest "$feed/$v/edfi.suite3.restapi.databases.standard.6.1.0.$v.nupkg" -OutFile $zip -UseBasicParsing
    Expand-Archive $zip -DestinationPath $pkgDir -Force
}
if (-not (Test-Path (Join-Path $pkgDir 'Deployment.psm1'))) { throw "Deployment.psm1 not found in $pkgDir after extract." }
Write-Host "Package ready at $pkgDir" -ForegroundColor Green

$configPath = Join-Path $pkgDir 'configuration.json'
if (-not (Test-Path $configPath)) { throw "configuration.json not found in $pkgDir" }
Copy-Item $configPath "$configPath.bak" -Force

$cfg = Get-Content $configPath -Raw | ConvertFrom-Json
$cs = [pscustomobject]@{
    EdFi_Admin    = "server=$SqlServer;user id=$LoginName;password=$Password;database=EdFi_Admin;Encrypt=false;Application Name=EdFi.Ods.WebApi"
    EdFi_Security = "server=$SqlServer;user id=$LoginName;password=$Password;database=EdFi_Security;persist security info=True;Encrypt=false;Application Name=EdFi.Ods.WebApi"
    EdFi_Master   = "server=$SqlServer;user id=$LoginName;password=$Password;database=master;Encrypt=false;Application Name=EdFi.Ods.WebApi"
    EdFi_Ods      = "server=$SqlServer;user id=$LoginName;password=$Password;database=EdFi_{0};Encrypt=false;Application Name=EdFi.RestApi.Databases"
}
if ($cfg.PSObject.Properties['ConnectionStrings']) { $cfg.ConnectionStrings = $cs }
else { $cfg | Add-Member -NotePropertyName ConnectionStrings -NotePropertyValue $cs }
foreach ($pair in @(, @('InstallType', 'Sandbox')) + @(, @('Engine', 'SQLServer'))) {
    if ($cfg.PSObject.Properties[$pair[0]]) { $cfg.($pair[0]) = $pair[1] }
    else { $cfg | Add-Member -NotePropertyName $pair[0] -NotePropertyValue $pair[1] }
}
Set-Content -Path $configPath -Value (ConvertTo-Json -InputObject $cfg -Depth 32) -Encoding UTF8

$check = Get-Content $configPath -Raw | ConvertFrom-Json
foreach ($name in 'EdFi_Admin', 'EdFi_Security', 'EdFi_Master', 'EdFi_Ods') {
    if (-not $check.ConnectionStrings.PSObject.Properties[$name]) { throw "Validation failed: $name missing after write." }
}
if ($check.ConnectionStrings.EdFi_Ods -notlike '*EdFi_{0}*') { throw 'Validation failed: EdFi_{0} placeholder was lost.' }
$pg = Join-Path $pkgDir 'configuration.postgreSQL.json'
if (Test-Path $pg) { Remove-Item $pg -Force }
Write-Host 'configuration.json patched and validated (4 connection strings, Sandbox, SQLServer).' -ForegroundColor Green

Write-Host ''
Write-Host 'Starting database deployment...' -ForegroundColor Cyan
Push-Location $pkgDir
try {
    Import-Module .\Deployment.psm1 -Force
    Initialize-DeploymentEnvironment
}
finally { Pop-Location }

Write-Host ''
Write-Host 'Phase A deployment finished.' -ForegroundColor Green
Write-Host 'Next: from the WORKSTATION run F:\Github\PhoenixSIS\_tools\Backup-EdfiBaseline.ps1' -ForegroundColor Yellow
Write-Host "Then DELETE $WorkDir on this VM (configuration.json holds the password)." -ForegroundColor Yellow
