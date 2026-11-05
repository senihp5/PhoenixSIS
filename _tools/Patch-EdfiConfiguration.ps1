<#
    Patch-EdfiConfiguration.ps1

    Rewrites the ConnectionStrings block of an extracted
    EdFi.Suite3.RestApi.Databases package configuration.json with the
    July-proven settings: SQL auth as edfi_app, Encrypt=false, all FOUR
    strings including the EdFi_Ods template string (the EdFi_{0}
    placeholder is kept literal), InstallType=Sandbox, Engine=SQLServer.
    Backs up the original, validates the JSON after writing, and removes
    configuration.postgreSQL.json.

    COPY THIS SCRIPT TO THE SQL VM and run it from inside the extracted
    package folder. The deployment itself must run ON the VM - the
    minimal/populated template .bak restores read from the VM's local
    disk, so it cannot run over the VPN from the workstation.

    Examples:
      .\Patch-EdfiConfiguration.ps1
      .\Patch-EdfiConfiguration.ps1 -PackageDir C:\Temp\EdFi.Suite3.RestApi.Databases.Standard.6.1.0
#>

[CmdletBinding()]
param(
    [string]$PackageDir = (Get-Location).Path,
    [string]$SqlServer = '(local)',
    [string]$LoginName = 'edfi_app',
    [string]$Password
)

$ErrorActionPreference = 'Stop'

$configPath = Join-Path $PackageDir 'configuration.json'
if (-not (Test-Path $configPath)) { throw "configuration.json not found in $PackageDir" }

if (-not $Password) {
    $secure = Read-Host -Prompt "Password for $LoginName (from C:\temp\edfi-sql.ps1 on the workstation)" -AsSecureString
    $Password = (New-Object System.Management.Automation.PSCredential('x', $secure)).GetNetworkCredential().Password
}
if (-not $Password) { throw 'A password is required.' }

Copy-Item $configPath "$configPath.bak" -Force
Write-Host "Backup written: $configPath.bak" -ForegroundColor DarkGray

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

$json = ConvertTo-Json -InputObject $cfg -Depth 32
Set-Content -Path $configPath -Value $json -Encoding UTF8

$check = Get-Content $configPath -Raw | ConvertFrom-Json
foreach ($name in 'EdFi_Admin', 'EdFi_Security', 'EdFi_Master', 'EdFi_Ods') {
    if (-not $check.ConnectionStrings.PSObject.Properties[$name]) { throw "Validation failed: $name missing after write." }
}
if ($check.ConnectionStrings.EdFi_Ods -notlike '*EdFi_{0}*') { throw 'Validation failed: EdFi_Ods template placeholder {0} was lost.' }
if ($check.InstallType -ne 'Sandbox') { throw 'Validation failed: InstallType is not Sandbox.' }
if ($check.Engine -ne 'SQLServer') { throw 'Validation failed: Engine is not SQLServer.' }

$pg = Join-Path $PackageDir 'configuration.postgreSQL.json'
if (Test-Path $pg) {
    Remove-Item $pg -Force
    Write-Host 'Removed configuration.postgreSQL.json.' -ForegroundColor DarkGray
}

Write-Host ''
Write-Host 'configuration.json patched and validated:' -ForegroundColor Green
foreach ($p in $check.ConnectionStrings.PSObject.Properties) {
    Write-Host ("  {0,-13} {1}" -f $p.Name, ($p.Value -replace 'password=[^;]*', 'password=********'))
}
Write-Host ("  InstallType   {0}" -f $check.InstallType)
Write-Host ("  Engine        {0}" -f $check.Engine)
Write-Host ''
Write-Host 'Next (admin PowerShell in this folder):' -ForegroundColor Cyan
Write-Host '  [Net.ServicePointManager]::SecurityProtocol += [Net.SecurityProtocolType]::Tls12'
Write-Host '  Import-Module .\Deployment.psm1'
Write-Host '  Initialize-DeploymentEnvironment'
