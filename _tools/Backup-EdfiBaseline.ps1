<#
    Backup-EdfiBaseline.ps1

    Post-deployment step for PHX-3.3 Phase A (MANDATORY this round - it
    was deferred in July and the teardown mooted it): sets SIMPLE
    recovery on the four Ed-Fi databases and takes compressed baseline
    .bak backups to the SQL server's default backup path (override with
    -BackupDir, which is a path on the VM, not the workstation).

    Run from the workstation over the Azure VPN. Credentials are
    dot-sourced from C:\temp\edfi-sql.ps1 (written by New-EdfiLogin.ps1).

    Examples:
      .\Backup-EdfiBaseline.ps1
      .\Backup-EdfiBaseline.ps1 -BackupDir 'D:\SQLBackups\EdFi'
#>

[CmdletBinding()]
param(
    [string]$CredentialScript = 'C:\temp\edfi-sql.ps1',
    [string]$BackupDir,
    [string[]]$Databases = @('EdFi_Admin', 'EdFi_Security', 'EdFi_Ods_Minimal_Template', 'EdFi_Ods_Populated_Template')
)

$ErrorActionPreference = 'Stop'

if (-not (Test-Path $CredentialScript)) { throw "Credential file not found: $CredentialScript. Run New-EdfiLogin.ps1 first." }
. $CredentialScript
if (-not $Global:EdfiSrv -or -not $Global:EdfiUsr -or -not $Global:EdfiPwd) { throw "Credential file $CredentialScript did not set EdfiSrv/EdfiUsr/EdfiPwd." }

$connStr = "Server=$($Global:EdfiSrv);Initial Catalog=master;User ID=$($Global:EdfiUsr);Password=$($Global:EdfiPwd);TrustServerCertificate=True;Encrypt=True"
$conn = New-Object System.Data.SqlClient.SqlConnection $connStr
$conn.Open()
try {
    $cmd = $conn.CreateCommand()
    $cmd.CommandTimeout = 3600

    if (-not $BackupDir) {
        $cmd.CommandText = "SELECT CAST(SERVERPROPERTY('InstanceDefaultBackupPath') AS nvarchar(4000))"
        $BackupDir = [string]$cmd.ExecuteScalar()
        if (-not $BackupDir) { throw 'Could not resolve the default backup path; pass -BackupDir.' }
    }
    $BackupDir = $BackupDir.TrimEnd('\')
    Write-Host "Backing up to $BackupDir on $($Global:EdfiSrv)" -ForegroundColor Cyan

    $cmd.CommandText = "SELECT name FROM sys.databases WHERE name IN ('" + ($Databases -join "','") + "')"
    $found = @()
    $rdr = $cmd.ExecuteReader()
    while ($rdr.Read()) { $found += $rdr.GetString(0) }
    $rdr.Close()
    $missing = $Databases | Where-Object { $found -notcontains $_ }
    foreach ($m in $missing) { Write-Host "  SKIPPING (not found): $m" -ForegroundColor Yellow }
    if ($found.Count -eq 0) { throw 'None of the Ed-Fi databases exist yet. Run the deployment first.' }

    foreach ($db in $found) {
        $file = "$BackupDir\${db}_baseline.bak"
        Write-Host ("  {0} -> SIMPLE recovery, then {1}" -f $db, $file)
        $cmd.CommandText = "ALTER DATABASE [$db] SET RECOVERY SIMPLE"
        [void]$cmd.ExecuteNonQuery()
        $cmd.CommandText = "BACKUP DATABASE [$db] TO DISK = N'$file' WITH INIT, COMPRESSION"
        [void]$cmd.ExecuteNonQuery()
    }

    $cmd.CommandText = @"
SELECT d.name, CONVERT(decimal(10,1), SUM(mf.size) * 8.0 / 1024) AS SizeMB, d.recovery_model_desc
FROM sys.databases d JOIN sys.master_files mf ON mf.database_id = d.database_id
WHERE d.name LIKE 'EdFi%'
GROUP BY d.name, d.recovery_model_desc
ORDER BY d.name
"@
    $rdr = $cmd.ExecuteReader()
    Write-Host ''
    Write-Host 'Ed-Fi databases:' -ForegroundColor Green
    while ($rdr.Read()) { Write-Host ("  {0,-30} {1,8} MB  {2}" -f $rdr.GetString(0), $rdr.GetDecimal(1), $rdr.GetString(2)) }
    $rdr.Close()
}
finally { $conn.Close() }

Write-Host ''
Write-Host 'Baselines complete. Now delete the extracted package folder under C:\Temp on the VM (configuration.json holds the password).' -ForegroundColor Yellow
