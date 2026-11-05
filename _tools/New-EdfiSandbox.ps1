<#
    New-EdfiSandbox.ps1  (PHX-3.4 — direct-in-EdFi_Admin provisioning, no SandboxAdmin)

    Creates a working Ed-Fi sandbox end to end:
      1. Restores EdFi_Ods_Sandbox_<Name> from the baseline .bak of the chosen
         template (logical file names read via RESTORE FILELISTONLY), SIMPLE recovery
      2. Inserts Vendor + namespace prefixes, User, Application (claim set),
         ApplicationEducationOrganizations, ApiClient (plaintext secret,
         SecretIsHashed=0 - the API hashes it on first successful auth),
         ApiClientApplicationEducationOrganizations, OdsInstances (full
         connection string for the API, which runs in Azure -> 10.10.2.4),
         ApiClientOdsInstances - all in one transaction
      3. Saves key/secret to C:\temp\edfi-sandbox.ps1 (outside the repo)
      4. Smoke test: OAuth token + GET /data/v3/ed-fi/students

    Run from the workstation over the Azure VPN. SQL creds come from
    C:\temp\edfi-sql.ps1 (written by New-EdfiLogin.ps1).

    Examples:
      .\New-EdfiSandbox.ps1
      .\New-EdfiSandbox.ps1 -SandboxName Demo -Template Minimal -SandboxType 1
#>

[CmdletBinding()]
param(
    [string]$SandboxName = 'Phoenix',
    [ValidateSet('Populated', 'Minimal')]
    [string]$Template = 'Populated',
    [int]$SandboxType = 2,
    [string]$ClaimSet = 'Ed-Fi Sandbox',
    [long[]]$EdOrgIds = @(255901),
    [string]$VendorName = 'Phoenix SIS',
    [string]$UserEmail = 'patrick@phoenixsis.onmicrosoft.com',
    [string]$UserFullName = 'Patrick Hines',
    [string]$ApiBase = 'https://nh-edfi-cus.azurewebsites.net',
    [string]$OdsServerForApi = '10.10.2.4',
    [string]$BackupDir,
    [string]$CredentialScript = 'C:\temp\edfi-sql.ps1',
    [string]$SecretsOut = 'C:\temp\edfi-sandbox.ps1'
)

$ErrorActionPreference = 'Stop'
[Net.ServicePointManager]::SecurityProtocol += [Net.SecurityProtocolType]::Tls12

if (-not (Test-Path $CredentialScript)) { throw "Credential file not found: $CredentialScript" }
. $CredentialScript
if (-not $Global:EdfiSrv -or -not $Global:EdfiUsr -or -not $Global:EdfiPwd) { throw "$CredentialScript did not set EdfiSrv/EdfiUsr/EdfiPwd." }

$dbName = "EdFi_Ods_Sandbox_$SandboxName"
$charset = [char[]]'ABCDEFGHJKLMNPQRSTUVWXYZabcdefghjkmnpqrstuvwxyz23456789'
$rng = [System.Security.Cryptography.RandomNumberGenerator]::Create()
function New-RandomString([int]$len) {
    $b = New-Object byte[] $len; $rng.GetBytes($b)
    -join ($b | ForEach-Object { $charset[$_ % $charset.Length] })
}
$key = New-RandomString 16
$secret = New-RandomString 32

$master = New-Object System.Data.SqlClient.SqlConnection "Server=$($Global:EdfiSrv);Initial Catalog=master;User ID=$($Global:EdfiUsr);Password=$($Global:EdfiPwd);TrustServerCertificate=True;Encrypt=True"
$master.Open()
try {
    $cmd = $master.CreateCommand(); $cmd.CommandTimeout = 3600

    $cmd.CommandText = "SELECT COUNT(*) FROM sys.databases WHERE name = N'$dbName'"
    if ([int]$cmd.ExecuteScalar() -gt 0) { throw "$dbName already exists. Drop it or pick another -SandboxName." }

    if (-not $BackupDir) {
        $cmd.CommandText = "SELECT CAST(SERVERPROPERTY('InstanceDefaultBackupPath') AS nvarchar(4000))"
        $BackupDir = ([string]$cmd.ExecuteScalar()).TrimEnd('\')
    }
    $bak = "$BackupDir\EdFi_Ods_${Template}_Template_baseline.bak"
    $cmd.CommandText = "SELECT CAST(SERVERPROPERTY('InstanceDefaultDataPath') AS nvarchar(400)), CAST(SERVERPROPERTY('InstanceDefaultLogPath') AS nvarchar(400))"
    $rdr = $cmd.ExecuteReader(); [void]$rdr.Read(); $dataPath = $rdr.GetString(0).TrimEnd('\'); $logPath = $rdr.GetString(1).TrimEnd('\'); $rdr.Close()

    Write-Host "Restoring $dbName from $bak ..." -ForegroundColor Cyan
    $cmd.CommandText = "RESTORE FILELISTONLY FROM DISK = N'$bak'"
    $rdr = $cmd.ExecuteReader(); $files = @()
    while ($rdr.Read()) { $files += [pscustomobject]@{ Logical = $rdr['LogicalName']; Type = $rdr['Type'] } }
    $rdr.Close()
    $moves = ($files | ForEach-Object {
        $ext = if ($_.Type -eq 'L') { 'ldf' } else { 'mdf' }
        $dir = if ($_.Type -eq 'L') { $logPath } else { $dataPath }
        "MOVE N'$($_.Logical)' TO N'$dir\$dbName.$ext'"
    }) -join ', '
    $cmd.CommandText = "RESTORE DATABASE [$dbName] FROM DISK = N'$bak' WITH $moves, RECOVERY"
    [void]$cmd.ExecuteNonQuery()
    $cmd.CommandText = "ALTER DATABASE [$dbName] SET RECOVERY SIMPLE"
    [void]$cmd.ExecuteNonQuery()
    Write-Host "$dbName restored (SIMPLE recovery)." -ForegroundColor Green
}
finally { $master.Close() }

$odsConnForApi = "Server=$OdsServerForApi;Database=$dbName;User Id=$($Global:EdfiUsr);Password=$($Global:EdfiPwd);Encrypt=false;Application Name=EdFi.Ods.WebApi"

$admin = New-Object System.Data.SqlClient.SqlConnection "Server=$($Global:EdfiSrv);Initial Catalog=EdFi_Admin;User ID=$($Global:EdfiUsr);Password=$($Global:EdfiPwd);TrustServerCertificate=True;Encrypt=True"
$admin.Open()
$tx = $admin.BeginTransaction('EdfiSandbox')
try {
    function Exec-Scalar([string]$sql, [hashtable]$params) {
        $c = $admin.CreateCommand(); $c.Transaction = $tx; $c.CommandText = $sql
        foreach ($k in $params.Keys) { [void]$c.Parameters.AddWithValue($k, $params[$k]) }
        $c.ExecuteScalar()
    }

    $existing = Exec-Scalar "SELECT COUNT(*) FROM dbo.ApiClients WHERE [Key] = @k" @{ '@k' = $key }
    if ([int]$existing -gt 0) { throw 'Key collision (astronomically unlikely) - rerun.' }

    $vendorId = Exec-Scalar "INSERT INTO dbo.Vendors (VendorName) VALUES (@n); SELECT CAST(SCOPE_IDENTITY() AS int)" @{ '@n' = $VendorName }
    foreach ($ns in 'uri://ed-fi.org', 'uri://phoenixsis.org') {
        [void](Exec-Scalar "INSERT INTO dbo.VendorNamespacePrefixes (NamespacePrefix, Vendor_VendorId) VALUES (@p, @v)" @{ '@p' = $ns; '@v' = $vendorId })
    }
    $userId = Exec-Scalar "INSERT INTO dbo.Users (Email, FullName, Vendor_VendorId) VALUES (@e, @f, @v); SELECT CAST(SCOPE_IDENTITY() AS int)" @{ '@e' = $UserEmail; '@f' = $UserFullName; '@v' = $vendorId }
    $appId = Exec-Scalar "INSERT INTO dbo.Applications (ApplicationName, Vendor_VendorId, ClaimSetName, OperationalContextUri) VALUES (@n, @v, @c, @o); SELECT CAST(SCOPE_IDENTITY() AS int)" @{ '@n' = "$SandboxName Sandbox"; '@v' = $vendorId; '@c' = $ClaimSet; '@o' = 'uri://ed-fi-api-host.org' }

    $appEdOrgIds = @()
    foreach ($edOrg in $EdOrgIds) {
        $appEdOrgIds += Exec-Scalar "INSERT INTO dbo.ApplicationEducationOrganizations (EducationOrganizationId, Application_ApplicationId) VALUES (@e, @a); SELECT CAST(SCOPE_IDENTITY() AS int)" @{ '@e' = $edOrg; '@a' = $appId }
    }

    $apiClientId = Exec-Scalar @"
INSERT INTO dbo.ApiClients ([Key], [Secret], [Name], IsApproved, UseSandbox, SandboxType, Application_ApplicationId, User_UserId, SecretIsHashed, ActivationRetried)
VALUES (@k, @s, @n, 1, 1, @t, @a, @u, 0, 0); SELECT CAST(SCOPE_IDENTITY() AS int)
"@ @{ '@k' = $key; '@s' = $secret; '@n' = $SandboxName; '@t' = $SandboxType; '@a' = $appId; '@u' = $userId }

    foreach ($aeo in $appEdOrgIds) {
        [void](Exec-Scalar "INSERT INTO dbo.ApiClientApplicationEducationOrganizations (ApiClient_ApiClientId, ApplicationEducationOrganization_ApplicationEducationOrganizationId) VALUES (@c, @e)" @{ '@c' = $apiClientId; '@e' = $aeo })
    }

    $odsInstanceId = Exec-Scalar "INSERT INTO dbo.OdsInstances ([Name], InstanceType, ConnectionString) VALUES (@n, N'Sandbox', @cs); SELECT CAST(SCOPE_IDENTITY() AS int)" @{ '@n' = $dbName; '@cs' = $odsConnForApi }
    [void](Exec-Scalar "INSERT INTO dbo.ApiClientOdsInstances (ApiClient_ApiClientId, OdsInstance_OdsInstanceId) VALUES (@c, @o)" @{ '@c' = $apiClientId; '@o' = $odsInstanceId })

    $tx.Commit()
    Write-Host "Admin rows committed: Vendor=$vendorId User=$userId App=$appId ApiClient=$apiClientId OdsInstance=$odsInstanceId EdOrgs=$($EdOrgIds -join ',')" -ForegroundColor Green
}
catch { $tx.Rollback(); throw }
finally { $admin.Close() }

$secrets = @(
    "`$Global:EdfiSandboxKey = '$key'"
    "`$Global:EdfiSandboxSecret = '$secret'"
    "`$Global:EdfiSandboxDb = '$dbName'"
    "`$Global:EdfiApiBase = '$ApiBase'"
) -join [Environment]::NewLine
Set-Content -Path $SecretsOut -Value $secrets -Encoding ASCII
Write-Host "Key/secret written to $SecretsOut (outside the repo - do not commit)." -ForegroundColor Yellow

Write-Host ''
Write-Host 'Smoke test: OAuth token...' -ForegroundColor Cyan
$auth = 'Basic ' + [Convert]::ToBase64String([Text.Encoding]::ASCII.GetBytes("${key}:${secret}"))
$tokenResp = Invoke-RestMethod "$ApiBase/oauth/token" -Method Post -Body @{ grant_type = 'client_credentials' } -Headers @{ Authorization = $auth } -TimeoutSec 120
Write-Host "  token_type=$($tokenResp.token_type) expires_in=$($tokenResp.expires_in)" -ForegroundColor Green

Write-Host 'Smoke test: GET /data/v3/ed-fi/students?limit=3 ...' -ForegroundColor Cyan
$students = Invoke-RestMethod "$ApiBase/data/v3/ed-fi/students?limit=3" -Headers @{ Authorization = "Bearer $($tokenResp.access_token)" } -TimeoutSec 120
Write-Host "  returned $(@($students).Count) student(s); first: $(@($students)[0].firstName) $(@($students)[0].lastSurname)" -ForegroundColor Green

Write-Host ''
Write-Host "PHX-3.4 sandbox '$SandboxName' is LIVE. Postman: key/secret in $SecretsOut, token URL $ApiBase/oauth/token" -ForegroundColor Green
