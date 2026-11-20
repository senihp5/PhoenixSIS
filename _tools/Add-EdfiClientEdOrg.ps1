<#
    Add-EdfiClientEdOrg.ps1  (PHX-7.0 prerequisite)

    Grants the Phoenix sandbox API client an additional education organization
    (default: LEA 220822 "New Heights") so it can write enrollment-side
    resources (calendars, sessions, studentSchoolAssociations, ...) for that
    LEA's schools. The Ed-Fi Sandbox claim set uses relationship-based
    authorization: without this the client only covers the Ed-Fi sample LEA
    255901 and every write for school 220822001 returns 403.

    Report mode (default) changes nothing: it shows the client's current
    EdOrgs from EdFi_Admin and from /oauth/token_info.
    -Apply inserts the ApplicationEducationOrganizations row (if missing) and
    the ApiClientApplicationEducationOrganizations link (if missing) in one
    transaction, then verifies a fresh token lists the EdOrg.
    -Rollback removes exactly those rows for the given EdOrgIds.

    Scope guard: keyed to the single ApiClient whose Key is in
    C:\temp\edfi-sandbox.ps1. Runs as edfi_app (db_owner on EdFi_Admin);
    no sysadmin needed. Run from the workstation over the Azure VPN.
    Sample data only.

    Examples:
      .\Add-EdfiClientEdOrg.ps1
      .\Add-EdfiClientEdOrg.ps1 -Apply
      .\Add-EdfiClientEdOrg.ps1 -Rollback
#>

[CmdletBinding()]
param(
    [long[]]$EdOrgIds = @(220822),
    [switch]$Apply,
    [switch]$Rollback,
    [string]$CredentialScript = 'C:\temp\edfi-sql.ps1',
    [string]$SandboxScript = 'C:\temp\edfi-sandbox.ps1'
)

$ErrorActionPreference = 'Stop'
[Net.ServicePointManager]::SecurityProtocol += [Net.SecurityProtocolType]::Tls12

if ($Apply -and $Rollback) { throw 'Use -Apply or -Rollback, not both.' }
foreach ($f in $CredentialScript, $SandboxScript) {
    if (-not (Test-Path $f)) { throw "Credential file not found: $f" }
}
. $CredentialScript
. $SandboxScript
if (-not $Global:EdfiSrv -or -not $Global:EdfiUsr -or -not $Global:EdfiPwd) { throw "$CredentialScript did not set EdfiSrv/EdfiUsr/EdfiPwd." }
if (-not $Global:EdfiSandboxKey -or -not $Global:EdfiApiBase) { throw "$SandboxScript did not set the sandbox creds." }

function Get-TokenEdOrgIds {
    $auth = 'Basic ' + [Convert]::ToBase64String(
        [Text.Encoding]::ASCII.GetBytes("$($Global:EdfiSandboxKey):$($Global:EdfiSandboxSecret)"))
    for ($i = 0; $i -lt 5; $i++) {
        try {
            $tok = (Invoke-RestMethod "$($Global:EdfiApiBase)/oauth/token" -Method Post `
                -Body @{ grant_type = 'client_credentials' } -Headers @{ Authorization = $auth } -TimeoutSec 60).access_token
            $ti = Invoke-RestMethod "$($Global:EdfiApiBase)/oauth/token_info" -Method Post `
                -Body @{ token = $tok } -Headers @{ Authorization = "Bearer $tok" } -TimeoutSec 60
            return @($ti.education_organizations | ForEach-Object { [long]$_.education_organization_id })
        }
        catch {
            $code = $_.Exception.Response.StatusCode.value__
            if ($code -in 404, 408, 429, 500, 502, 503, 504 -and $i -lt 4) { Start-Sleep 4; continue }
            throw
        }
    }
}

$cn = New-Object System.Data.SqlClient.SqlConnection "Server=$($Global:EdfiSrv);Initial Catalog=EdFi_Admin;User ID=$($Global:EdfiUsr);Password=$($Global:EdfiPwd);TrustServerCertificate=True;Encrypt=True"
$cn.Open()
try {
    $c = $cn.CreateCommand()
    $c.CommandText = 'SELECT ApiClientId, Application_ApplicationId, [Name] FROM dbo.ApiClients WHERE [Key] = @k'
    [void]$c.Parameters.AddWithValue('@k', $Global:EdfiSandboxKey)
    $r = $c.ExecuteReader()
    if (-not $r.Read()) { $r.Close(); throw "No ApiClient in EdFi_Admin matches the key in $SandboxScript." }
    $apiClientId = $r.GetInt32(0); $appId = $r.GetInt32(1); $clientName = $r.GetString(2)
    $r.Close()

    function Get-AdminEdOrgs {
        $q = $cn.CreateCommand()
        $q.CommandText = @"
SELECT aeo.EducationOrganizationId,
       CASE WHEN link.ApiClient_ApiClientId IS NULL THEN 0 ELSE 1 END AS Linked
FROM dbo.ApplicationEducationOrganizations aeo
LEFT JOIN dbo.ApiClientApplicationEducationOrganizations link
    ON link.ApplicationEducationOrganization_ApplicationEducationOrganizationId = aeo.ApplicationEducationOrganizationId
   AND link.ApiClient_ApiClientId = @c
WHERE aeo.Application_ApplicationId = @a
ORDER BY aeo.EducationOrganizationId;
"@
        [void]$q.Parameters.AddWithValue('@c', $apiClientId)
        [void]$q.Parameters.AddWithValue('@a', $appId)
        $rr = $q.ExecuteReader(); $rows = @()
        while ($rr.Read()) { $rows += [pscustomobject]@{ EdOrgId = [Convert]::ToInt64($rr.GetValue(0)); Linked = ([Convert]::ToInt32($rr.GetValue(1)) -eq 1) } }
        $rr.Close()
        $rows
    }

    Write-Host "=== Sandbox API client '$clientName' (ApiClientId=$apiClientId, ApplicationId=$appId) ===" -ForegroundColor Cyan
    foreach ($row in Get-AdminEdOrgs) {
        Write-Host ("  EdFi_Admin EdOrg {0,-10} linked to client: {1}" -f $row.EdOrgId, $row.Linked)
    }
    $tokenIds = Get-TokenEdOrgIds
    foreach ($id in $EdOrgIds) {
        Write-Host ("  Token covers {0}: {1}" -f $id, ($tokenIds -contains $id))
    }

    if (-not $Apply -and -not $Rollback) {
        Write-Host ''
        Write-Host 'REPORT ONLY - nothing changed.' -ForegroundColor Yellow
        Write-Host "  -Apply    : grant EdOrg(s) $($EdOrgIds -join ',') to this client, then verify via /oauth/token_info"
        Write-Host "  -Rollback : remove EdOrg(s) $($EdOrgIds -join ',') from this client"
        return
    }

    $tx = $cn.BeginTransaction('EdfiClientEdOrg')
    try {
        foreach ($id in $EdOrgIds) {
            $w = $cn.CreateCommand(); $w.Transaction = $tx
            if ($Apply) {
                $w.CommandText = @"
DECLARE @aeo int = (SELECT ApplicationEducationOrganizationId FROM dbo.ApplicationEducationOrganizations
                    WHERE Application_ApplicationId = @a AND EducationOrganizationId = @e);
IF @aeo IS NULL
BEGIN
    INSERT INTO dbo.ApplicationEducationOrganizations (EducationOrganizationId, Application_ApplicationId) VALUES (@e, @a);
    SET @aeo = CAST(SCOPE_IDENTITY() AS int);
END;
IF NOT EXISTS (SELECT 1 FROM dbo.ApiClientApplicationEducationOrganizations
               WHERE ApiClient_ApiClientId = @c AND ApplicationEducationOrganization_ApplicationEducationOrganizationId = @aeo)
    INSERT INTO dbo.ApiClientApplicationEducationOrganizations (ApiClient_ApiClientId, ApplicationEducationOrganization_ApplicationEducationOrganizationId)
    VALUES (@c, @aeo);
"@
            }
            else {
                $w.CommandText = @"
DECLARE @aeo int = (SELECT ApplicationEducationOrganizationId FROM dbo.ApplicationEducationOrganizations
                    WHERE Application_ApplicationId = @a AND EducationOrganizationId = @e);
IF @aeo IS NOT NULL
BEGIN
    DELETE FROM dbo.ApiClientApplicationEducationOrganizations
        WHERE ApiClient_ApiClientId = @c AND ApplicationEducationOrganization_ApplicationEducationOrganizationId = @aeo;
    IF NOT EXISTS (SELECT 1 FROM dbo.ApiClientApplicationEducationOrganizations
                   WHERE ApplicationEducationOrganization_ApplicationEducationOrganizationId = @aeo)
        DELETE FROM dbo.ApplicationEducationOrganizations WHERE ApplicationEducationOrganizationId = @aeo;
END;
"@
            }
            [void]$w.Parameters.AddWithValue('@a', $appId)
            [void]$w.Parameters.AddWithValue('@c', $apiClientId)
            [void]$w.Parameters.AddWithValue('@e', $id)
            [void]$w.ExecuteNonQuery()
        }
        $tx.Commit()
    }
    catch { $tx.Rollback(); throw }

    Write-Host ''
    Write-Host ($(if ($Apply) { 'Applied.' } else { 'Rolled back.' }) + ' EdFi_Admin now:') -ForegroundColor Green
    foreach ($row in Get-AdminEdOrgs) {
        Write-Host ("  EdOrg {0,-10} linked to client: {1}" -f $row.EdOrgId, $row.Linked)
    }
}
finally { $cn.Close() }

$tokenIds = Get-TokenEdOrgIds
$ok = $true
foreach ($id in $EdOrgIds) {
    $has = $tokenIds -contains $id
    Write-Host ("  Fresh token covers {0}: {1}" -f $id, $has)
    if ($Apply -and -not $has) { $ok = $false }
    if ($Rollback -and $has) { $ok = $false }
}
Write-Host ''
if ($ok) { Write-Host 'PASS' -ForegroundColor Green }
else {
    Write-Host 'FAIL - EdFi_Admin changed but the token does not reflect it yet. The API caches client details for a few minutes; wait 5 minutes and re-run in report mode.' -ForegroundColor Red
    exit 1
}
