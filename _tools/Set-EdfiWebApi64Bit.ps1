#Requires -Version 7
<#
    Set-EdfiWebApi64Bit.ps1  (Hardening #2 - run the Ed-Fi WebApi on the 64-bit .NET host)

    Root cause (found 2026-11-12 via Kudu /api/processes): the app pool (w3wp) is 64-bit,
    but the deployed web.config has
        <aspNetCore processPath="dotnet" ... hostingModel="OutOfProcess" />
    and on App Service a bare "dotnet" resolves to
        C:\Program Files (x86)\dotnet\dotnet.exe   (the 32-bit host).
    Under 32-bit, xxHash's Bmi2.X64 path throws PlatformNotSupportedException on
    /metadata/, which is why DOTNET_EnableHWIntrinsic=0 was added as a workaround.

    Fix: point the ASP.NET Core Module at %ProgramFiles%\dotnet\dotnet.exe (64-bit;
    x64 .NET 10.0.14/10.0.15 runtimes verified present 2026-11-20; app targets net10.0),
    verify, then remove the workaround.

    Modes (default = report only, changes nothing):
      -Apply                        back up web.config, set the 64-bit processPath, verify
                                    the running host is 64-bit and /metadata/ returns 200
      -RemoveHwIntrinsicWorkaround  (after -Apply passes) delete DOTNET_EnableHWIntrinsic,
                                    verify /metadata/; re-adds the setting on failure
      -Rollback                     restore the backed-up web.config, re-add the setting

    A future zip deploy of the WebApi overwrites web.config - re-run -Apply after any
    redeploy. Uses the caller's az login (Kudu via the management token). PowerShell 7.

    Examples:
      .\Set-EdfiWebApi64Bit.ps1
      .\Set-EdfiWebApi64Bit.ps1 -Apply
      .\Set-EdfiWebApi64Bit.ps1 -RemoveHwIntrinsicWorkaround
      .\Set-EdfiWebApi64Bit.ps1 -Rollback
#>

[CmdletBinding()]
param(
    [string]$ResourceGroup = 'IDCardOCR',
    [string]$AppName = 'nh-edfi-cus',
    [string]$BackupPath = 'C:\temp\nh-edfi-cus.web.config.bak',
    [switch]$Apply,
    [switch]$RemoveHwIntrinsicWorkaround,
    [switch]$Rollback
)

$ErrorActionPreference = 'Stop'
$scm = "https://$AppName.scm.azurewebsites.net"
$site = "https://$AppName.azurewebsites.net"
$webConfigPath = '/api/vfs/site/wwwroot/web.config'
$x64Path = '%ProgramFiles%\dotnet\dotnet.exe'

function Invoke-Kudu([string]$Method, [string]$Path, [string]$BodyFile, [string]$OutFile) {
    $azArgs = @('rest', '--method', $Method, '--url', "$scm$Path", '--resource', 'https://management.azure.com/')
    if ($BodyFile) { $azArgs += @('--headers', 'Content-Type=application/octet-stream', 'If-Match=*', '--body', "@$BodyFile") }
    if ($OutFile) { $azArgs += @('--output-file', $OutFile) }
    $out = & az @azArgs 2>&1
    if ($LASTEXITCODE -ne 0) { throw "Kudu $Method $Path failed: $out" }
    ($out | ForEach-Object { "$_" } | Where-Object { $_ -notmatch '^WARNING:' }) -join "`n"
}

function Get-WebConfig([string]$Destination) {
    [void](Invoke-Kudu 'get' $webConfigPath -OutFile $Destination)
    Get-Content -Path $Destination -Raw
}

function Get-ProcessPath([string]$Xml) {
    $m = [regex]::Match($Xml, '<aspNetCore[^>]*\sprocessPath="([^"]*)"')
    if ($m.Success) { $m.Groups[1].Value } else { $null }
}

function Get-AppHostFiles {
    $list = Invoke-Kudu 'get' '/api/processes' | ConvertFrom-Json
    foreach ($p in @($list)) {
        if ($p.name -ne 'dotnet') { continue }
        $d = Invoke-Kudu 'get' "/api/processes/$($p.id)" | ConvertFrom-Json
        $d.file_name
    }
}

function Wait-Api([int]$Seconds = 240) {
    $deadline = (Get-Date).AddSeconds($Seconds)
    do {
        try {
            $r = Invoke-WebRequest "$site/" -TimeoutSec 60 -SkipHttpErrorCheck
            if ($r.StatusCode -eq 200) { return $true }
        }
        catch { }
        Start-Sleep -Seconds 10
    } while ((Get-Date) -lt $deadline)
    $false
}

function Test-Metadata {
    try {
        (Invoke-WebRequest "$site/metadata/data/v3/resources/swagger.json" -TimeoutSec 180 -SkipHttpErrorCheck).StatusCode
    }
    catch { 0 }
}

function Get-HwSetting {
    az webapp config appsettings list -g $ResourceGroup -n $AppName --query "[?name=='DOTNET_EnableHWIntrinsic'].value" -o tsv
}

function Set-HwSetting {
    az webapp config appsettings set -g $ResourceGroup -n $AppName --settings DOTNET_EnableHWIntrinsic=0 -o none
    if ($LASTEXITCODE -ne 0) { throw 'Failed to set DOTNET_EnableHWIntrinsic=0.' }
}

function Assert-64BitHost {
    for ($i = 1; $i -le 4; $i++) {
        $hosts = @(Get-AppHostFiles)
        if ($hosts.Count -gt 0 -and -not ($hosts | Where-Object { $_ -match '\(x86\)' })) {
            Write-Host "Running host: $($hosts -join '; ')" -ForegroundColor Green
            return
        }
        Start-Sleep -Seconds 20
        [void](Wait-Api)
    }
    throw "Not on the 64-bit host (running: $($hosts -join '; ')). Roll back with: .\Set-EdfiWebApi64Bit.ps1 -Rollback"
}

if ($Rollback) {
    if (-not (Test-Path $BackupPath)) { throw "No backup at $BackupPath." }
    [void](Invoke-Kudu 'put' $webConfigPath -BodyFile $BackupPath)
    Set-HwSetting
    Write-Host 'ROLLBACK: web.config restored and DOTNET_EnableHWIntrinsic=0 re-added.' -ForegroundColor Yellow
    Start-Sleep -Seconds 30
    if (Wait-Api) { Write-Host "API up; /metadata/ -> $(Test-Metadata)" }
    else { Write-Host 'API did not answer within 4 minutes.' -ForegroundColor Red }
    return
}

$work = New-TemporaryFile
try {
    [void](Wait-Api)
    $xml = Get-WebConfig $work.FullName
    $current = Get-ProcessPath $xml
    if (-not $current) { throw 'No <aspNetCore processPath="..."> found in web.config.' }

    Write-Host "=== $AppName 64-bit host ===" -ForegroundColor Cyan
    Write-Host "web.config processPath   : $current"
    Write-Host "running dotnet host(s)   : $(@(Get-AppHostFiles) -join '; ')"
    Write-Host "DOTNET_EnableHWIntrinsic : $(Get-HwSetting)"

    if (-not $Apply -and -not $RemoveHwIntrinsicWorkaround) {
        Write-Host ''
        Write-Host 'DRY RUN - nothing changed.' -ForegroundColor Yellow
        Write-Host "  -Apply                       : processPath -> $x64Path, verify 64-bit + /metadata/ 200"
        Write-Host '  -RemoveHwIntrinsicWorkaround : then delete DOTNET_EnableHWIntrinsic and re-verify'
        return
    }

    if ($Apply) {
        if ($current -eq $x64Path) {
            Write-Host 'processPath already points at the 64-bit host.'
        }
        elseif ($current -ne 'dotnet') {
            throw "Unexpected processPath '$current' - not changing it automatically."
        }
        else {
            Copy-Item -Path $work.FullName -Destination $BackupPath -Force
            Write-Host "Backed up web.config to $BackupPath" -ForegroundColor Green
            $patched = [regex]::Replace($xml, '(<aspNetCore[^>]*\sprocessPath=")dotnet(")', { param($m) $m.Groups[1].Value + $x64Path + $m.Groups[2].Value })
            if ((Get-ProcessPath $patched) -ne $x64Path) { throw 'Patch did not produce the expected processPath; nothing uploaded.' }
            $patchFile = New-TemporaryFile
            try {
                [System.IO.File]::WriteAllText($patchFile.FullName, $patched, (New-Object System.Text.UTF8Encoding($false)))
                [void](Invoke-Kudu 'put' $webConfigPath -BodyFile $patchFile.FullName)
            }
            finally { Remove-Item $patchFile -ErrorAction SilentlyContinue }
            Write-Host "processPath set to $x64Path; waiting for the app to recycle..." -ForegroundColor Green
            Start-Sleep -Seconds 30
        }

        if (-not (Wait-Api)) { throw 'API did not come back within 4 minutes. Roll back with -Rollback.' }
        Assert-64BitHost
        $m = Test-Metadata
        if ($m -ne 200) { throw "/metadata/ returned $m on the 64-bit host. Roll back with -Rollback." }
        Write-Host 'PASS: 64-bit host, /metadata/ 200 (workaround still in place).' -ForegroundColor Green
    }

    if ($RemoveHwIntrinsicWorkaround) {
        if (-not (Wait-Api)) { throw 'API is not answering; not changing settings.' }
        Assert-64BitHost
        if (-not (Get-HwSetting)) {
            Write-Host 'DOTNET_EnableHWIntrinsic already absent.'
        }
        else {
            az webapp config appsettings delete -g $ResourceGroup -n $AppName --setting-names DOTNET_EnableHWIntrinsic -o none
            if ($LASTEXITCODE -ne 0) { throw 'Failed to delete DOTNET_EnableHWIntrinsic.' }
            Write-Host 'DOTNET_EnableHWIntrinsic removed; waiting for the app to restart...' -ForegroundColor Green
            Start-Sleep -Seconds 45
        }
        if (-not (Wait-Api)) {
            Set-HwSetting
            throw 'API did not come back without the workaround - re-added DOTNET_EnableHWIntrinsic=0.'
        }
        Assert-64BitHost
        $m = Test-Metadata
        if ($m -ne 200) {
            Set-HwSetting
            throw "/metadata/ returned $m without the workaround - re-added DOTNET_EnableHWIntrinsic=0."
        }
        Write-Host 'PASS: 64-bit host, hardware intrinsics on, /metadata/ 200. Workaround retired.' -ForegroundColor Green
    }
}
finally { Remove-Item $work -ErrorAction SilentlyContinue }
