<#
    Set-EdfiLeastPrivilege.ps1  (Hardening #3 - edfi_app: sysadmin -> db_owner)

    Moves the Ed-Fi runtime login off sysadmin WITHOUT breaking the API:
      1. Preflight: the admin credential must be a sysadmin.
      2. For every EdFi_* database: owner -> sa (re-syncs the stale internal
         dbo in databases restored from .bak files), then map edfi_app as a
         real database user in db_owner (Get-EdfiDbUserGrantSql).
      3. Drop edfi_app from sysadmin.
      4. Verify AS edfi_app: not sysadmin, and db_owner in every EdFi_* DB.

    Default is a DRY RUN (report only). -Apply makes the changes.
    -Rollback re-adds edfi_app to sysadmin (the database users stay; harmless).

    Why step 2 matters: the ODS databases were restored from backups, so their
    internal dbo still points at the original owner and edfi_app is NOT mapped
    in them. Today it only gets in because sysadmin maps to dbo everywhere.
    Dropping sysadmin without step 2 takes the Ed-Fi API down.

    Scope: databases named EdFi_* and the edfi_app login only - nothing else on
    the shared SQL VM is touched. Run from the workstation over the Azure VPN.
    Admin = a real sysadmin (e.g. decisioned-admin) via Get-Credential. Never the
    CMS/Flightdeck cms_app login (not sysadmin; outside the ADR-003 boundary).

    Examples:
      .\Set-EdfiLeastPrivilege.ps1
      .\Set-EdfiLeastPrivilege.ps1 -Apply
      .\Set-EdfiLeastPrivilege.ps1 -Rollback
#>

[CmdletBinding()]
param(
    [string]$ServerInstance,
    [pscredential]$AdminCredential,
    [string]$LoginName = 'edfi_app',
    [string]$DatabaseOwner = 'sa',
    [string]$CredentialScript = 'C:\temp\edfi-sql.ps1',
    [switch]$Apply,
    [switch]$Rollback
)

$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot 'EdfiSql.Common.ps1')

if (-not (Test-Path $CredentialScript)) { throw "Credential file not found: $CredentialScript" }
. $CredentialScript
if (-not $Global:EdfiUsr -or -not $Global:EdfiPwd) { throw "$CredentialScript did not set EdfiUsr/EdfiPwd." }
if ($Global:EdfiUsr -ne $LoginName) { throw "$CredentialScript is for '$($Global:EdfiUsr)', not '$LoginName'." }
if (-not $ServerInstance) { $ServerInstance = $Global:EdfiSrv }
if (-not $ServerInstance) { throw "No server: pass -ServerInstance or set EdfiSrv in $CredentialScript." }
if (-not $AdminCredential) {
    $AdminCredential = Get-Credential -Message "SQL sysadmin on $ServerInstance (e.g. decisioned-admin - NOT cms_app)"
}

function Open-Sql([string]$user, [string]$password, [string]$database) {
    $c = New-Object System.Data.SqlClient.SqlConnection "Server=$ServerInstance;Initial Catalog=$database;User ID=$user;Password=$password;TrustServerCertificate=True;Encrypt=True;Connect Timeout=15"
    $c.Open()
    $c
}

function Invoke-Scalar($conn, [string]$sql) {
    $cmd = $conn.CreateCommand(); $cmd.CommandText = $sql; $cmd.CommandTimeout = 120
    $cmd.ExecuteScalar()
}

function Invoke-NonQuery($conn, [string]$sql) {
    $cmd = $conn.CreateCommand(); $cmd.CommandText = $sql; $cmd.CommandTimeout = 120
    [void]$cmd.ExecuteNonQuery()
}

$adminPassword = $AdminCredential.GetNetworkCredential().Password
$admin = Open-Sql $AdminCredential.UserName $adminPassword 'master'
$dbs = @()
try {
    if ([int](Invoke-Scalar $admin "SELECT IS_SRVROLEMEMBER('sysadmin')") -ne 1) {
        throw "$($AdminCredential.UserName) is not a sysadmin on $ServerInstance."
    }

    if ($Rollback) {
        Invoke-NonQuery $admin "ALTER SERVER ROLE [sysadmin] ADD MEMBER [$LoginName];"
        Write-Host "ROLLBACK: $LoginName added back to sysadmin." -ForegroundColor Yellow
        return
    }

    $cmd = $admin.CreateCommand()
    $cmd.CommandText = "SELECT name, SUSER_SNAME(owner_sid) FROM sys.databases WHERE name LIKE N'EdFi[_]%' AND state_desc = N'ONLINE' ORDER BY name"
    $rdr = $cmd.ExecuteReader()
    while ($rdr.Read()) { $dbs += [pscustomobject]@{ Database = $rdr.GetString(0); Owner = $rdr.GetValue(1) } }
    $rdr.Close()
    if ($dbs.Count -eq 0) { throw "No ONLINE EdFi_* databases found on $ServerInstance." }

    Write-Host "=== $LoginName least privilege on $ServerInstance ===" -ForegroundColor Cyan
    Write-Host ("{0} is sysadmin now: {1}" -f $LoginName, (Invoke-Scalar $admin "SELECT IS_SRVROLEMEMBER('sysadmin', N'$LoginName')"))
    $dbs | Format-Table -AutoSize | Out-String | Write-Host

    if (-not $Apply) {
        Write-Host "DRY RUN - nothing changed. Re-run with -Apply to make these changes:" -ForegroundColor Yellow
        Write-Host "  each database above: owner -> $DatabaseOwner, $LoginName mapped as db_owner"
        Write-Host "  server: $LoginName removed from sysadmin"
        return
    }

    foreach ($d in $dbs) {
        Invoke-NonQuery $admin (Get-EdfiDbUserGrantSql -Database $d.Database -LoginName $LoginName -Owner $DatabaseOwner)
        Write-Host "$($d.Database): owner=$DatabaseOwner, $LoginName mapped as db_owner." -ForegroundColor Green
    }

    Invoke-NonQuery $admin "IF IS_SRVROLEMEMBER('sysadmin', N'$LoginName') = 1 ALTER SERVER ROLE [sysadmin] DROP MEMBER [$LoginName];"
    Write-Host "$LoginName removed from sysadmin." -ForegroundColor Green
}
finally { $admin.Close() }

Write-Host ''
Write-Host "Verifying as $LoginName ..." -ForegroundColor Cyan
$app = Open-Sql $Global:EdfiUsr $Global:EdfiPwd 'master'
try { $isSysadmin = [int](Invoke-Scalar $app "SELECT IS_SRVROLEMEMBER('sysadmin')") } finally { $app.Close() }
if ($isSysadmin -ne 0) { throw "$LoginName is still sysadmin." }

$failed = @()
foreach ($d in $dbs) {
    $owner = 0
    try {
        $app = Open-Sql $Global:EdfiUsr $Global:EdfiPwd $d.Database
        try { $owner = [int](Invoke-Scalar $app "SELECT IS_MEMBER('db_owner')") } finally { $app.Close() }
    }
    catch { $owner = 0 }
    Write-Host ("  {0,-34} db_owner={1}" -f $d.Database, $owner)
    if ($owner -ne 1) { $failed += $d.Database }
}
if ($failed.Count -gt 0) {
    throw "$LoginName is NOT db_owner in: $($failed -join ', '). Roll back with: .\Set-EdfiLeastPrivilege.ps1 -Rollback"
}

Write-Host "PASS: $LoginName is db_owner on every EdFi_* database and is not sysadmin." -ForegroundColor Green
Write-Host "Next: restart nh-edfi-cus, then smoke-test the API." -ForegroundColor DarkGray
