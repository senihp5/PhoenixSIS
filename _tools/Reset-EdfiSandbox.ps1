<#
    Reset-EdfiSandbox.ps1  (PHX-3.6 — teardown + rebuild of the Phoenix sandbox)

    Returns the Ed-Fi sandbox to a known-clean state:
      1. Deletes the sandbox's EdFi_Admin rows in FK-safe order, scoped to
         the matching Vendor/OdsInstance only. Child tables cleared first
         (ClientAccessTokens, ApiClientOwnershipTokens,
         ApiClientApplicationEducationOrganizations, ApiClientOdsInstances,
         ProfileApplications, OdsInstanceContexts, OdsInstanceDerivatives)
         then ApiClients, ApplicationEducationOrganizations, Applications,
         OdsInstances, Users, VendorNamespacePrefixes, Vendors.
         (FK graph discovered from sys.foreign_keys, not assumed.)
      2. Drops EdFi_Ods_Sandbox_<Name> (single-user to kill live connections)
      3. Re-provisions by invoking New-EdfiSandbox.ps1 (fresh restore + seed +
         smoke test), unless -NoProvision is given

    Run from the workstation over the Azure VPN. SQL creds from
    C:\temp\edfi-sql.ps1. This is destructive to the sandbox ONLY; it never
    touches EdFi_Admin rows for other vendors/instances, the template DBs,
    or anything outside the sandbox. Sample data only.

    Examples:
      .\Reset-EdfiSandbox.ps1
      .\Reset-EdfiSandbox.ps1 -SandboxName Phoenix -NoProvision
#>

[CmdletBinding()]
param(
    [string]$SandboxName = 'Phoenix',
    [string]$VendorName = 'Phoenix SIS',
    [switch]$NoProvision,
    [string]$CredentialScript = 'C:\temp\edfi-sql.ps1',
    [string]$ProvisionScript = (Join-Path $PSScriptRoot 'New-EdfiSandbox.ps1')
)

$ErrorActionPreference = 'Stop'

if (-not (Test-Path $CredentialScript)) { throw "Credential file not found: $CredentialScript" }
. $CredentialScript
if (-not $Global:EdfiSrv -or -not $Global:EdfiUsr -or -not $Global:EdfiPwd) { throw "$CredentialScript did not set EdfiSrv/EdfiUsr/EdfiPwd." }

$dbName = "EdFi_Ods_Sandbox_$SandboxName"

Write-Host "=== Reset sandbox '$SandboxName' ===" -ForegroundColor Cyan

$admin = New-Object System.Data.SqlClient.SqlConnection "Server=$($Global:EdfiSrv);Initial Catalog=EdFi_Admin;User ID=$($Global:EdfiUsr);Password=$($Global:EdfiPwd);TrustServerCertificate=True;Encrypt=True"
$admin.Open()
$tx = $admin.BeginTransaction('ResetSandbox')
try {
    $cmd = $admin.CreateCommand(); $cmd.Transaction = $tx; $cmd.CommandTimeout = 300
    $cmd.CommandText = @"
DECLARE @vendorId int = (SELECT VendorId FROM dbo.Vendors WHERE VendorName = @vn);
DECLARE @odsId int = (SELECT OdsInstanceId FROM dbo.OdsInstances WHERE [Name] = @db);
DECLARE @clients TABLE (Id int);
INSERT INTO @clients (Id)
    SELECT c.ApiClientId FROM dbo.ApiClients c
    JOIN dbo.Applications a ON a.ApplicationId = c.Application_ApplicationId
    WHERE a.Vendor_VendorId = @vendorId;

DELETE FROM dbo.ClientAccessTokens WHERE ApiClient_ApiClientId IN (SELECT Id FROM @clients);
DELETE FROM dbo.ApiClientOwnershipTokens WHERE ApiClient_ApiClientId IN (SELECT Id FROM @clients);
DELETE FROM dbo.ApiClientApplicationEducationOrganizations WHERE ApiClient_ApiClientId IN (SELECT Id FROM @clients);
DELETE FROM dbo.ApiClientOdsInstances WHERE ApiClient_ApiClientId IN (SELECT Id FROM @clients) OR OdsInstance_OdsInstanceId = @odsId;
DELETE FROM dbo.ApiClients WHERE ApiClientId IN (SELECT Id FROM @clients);

DELETE aeo FROM dbo.ApplicationEducationOrganizations aeo
    JOIN dbo.Applications a ON a.ApplicationId = aeo.Application_ApplicationId WHERE a.Vendor_VendorId = @vendorId;
DELETE pa FROM dbo.ProfileApplications pa
    JOIN dbo.Applications a ON a.ApplicationId = pa.Application_ApplicationId WHERE a.Vendor_VendorId = @vendorId;
DELETE FROM dbo.Applications WHERE Vendor_VendorId = @vendorId;

DELETE FROM dbo.OdsInstanceContexts WHERE OdsInstance_OdsInstanceId = @odsId;
DELETE FROM dbo.OdsInstanceDerivatives WHERE OdsInstance_OdsInstanceId = @odsId;
DELETE FROM dbo.OdsInstances WHERE OdsInstanceId = @odsId;

DELETE FROM dbo.Users WHERE Vendor_VendorId = @vendorId;
DELETE FROM dbo.VendorNamespacePrefixes WHERE Vendor_VendorId = @vendorId;
DELETE FROM dbo.Vendors WHERE VendorId = @vendorId;

SELECT ISNULL(@vendorId, -1), ISNULL(@odsId, -1);
"@
    [void]$cmd.Parameters.AddWithValue('@vn', $VendorName)
    [void]$cmd.Parameters.AddWithValue('@db', $dbName)
    $rdr = $cmd.ExecuteReader(); [void]$rdr.Read()
    $removedVendor = $rdr.GetInt32(0); $removedOds = $rdr.GetInt32(1); $rdr.Close()
    $tx.Commit()
    Write-Host ("Admin rows cleared (vendorId={0}, odsInstanceId={1})." -f $removedVendor, $removedOds) -ForegroundColor Green
}
catch { $tx.Rollback(); throw }
finally { $admin.Close() }

$master = New-Object System.Data.SqlClient.SqlConnection "Server=$($Global:EdfiSrv);Initial Catalog=master;User ID=$($Global:EdfiUsr);Password=$($Global:EdfiPwd);TrustServerCertificate=True;Encrypt=True"
$master.Open()
try {
    $cmd = $master.CreateCommand(); $cmd.CommandTimeout = 300
    $cmd.CommandText = "SELECT COUNT(*) FROM sys.databases WHERE name = @db"
    [void]$cmd.Parameters.AddWithValue('@db', $dbName)
    if ([int]$cmd.ExecuteScalar() -gt 0) {
        $cmd.CommandText = "ALTER DATABASE [$dbName] SET SINGLE_USER WITH ROLLBACK IMMEDIATE; DROP DATABASE [$dbName];"
        [void]$cmd.ExecuteNonQuery()
        Write-Host "$dbName dropped." -ForegroundColor Green
    }
    else { Write-Host "$dbName did not exist." -ForegroundColor DarkGray }
}
finally { $master.Close() }

if ($NoProvision) {
    Write-Host "Teardown complete. -NoProvision set; skipping rebuild." -ForegroundColor Yellow
    return
}

if (-not (Test-Path $ProvisionScript)) { throw "Provisioner not found: $ProvisionScript" }
Write-Host ''
Write-Host "Re-provisioning via $ProvisionScript ..." -ForegroundColor Cyan
& $ProvisionScript -SandboxName $SandboxName -VendorName $VendorName

Write-Host ''
Write-Host "Reset complete. Sandbox '$SandboxName' rebuilt from baseline." -ForegroundColor Green
