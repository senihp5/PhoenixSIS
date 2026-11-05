<#
    New-EdfiLogin.ps1

    Recreates the edfi_app SQL login that was dropped at the 2026-07-22
    teardown (PHX-3.3 Phase A rebuild, step 1).

    Generates a new random password, creates or resets the login, adds it
    to sysadmin (TEMPORARY - required by the Ed-Fi database deployment;
    drop to least privilege after PHX-3.3), verifies a round-trip
    connection as edfi_app, and writes the credentials to a local secrets
    file OUTSIDE the repo (default C:\temp\edfi-sql.ps1, same pattern as
    C:\temp\ci-sql.ps1).

    Run from the workstation over the Azure VPN. Admin credentials default
    to the ci-sql.ps1 values; pass -AdminCredential (Get-Credential) if
    that login lacks securityadmin/sysadmin rights.

    Examples:
      .\New-EdfiLogin.ps1
      .\New-EdfiLogin.ps1 -ServerInstance sql.internal.newheightsed.com
      .\New-EdfiLogin.ps1 -AdminCredential (Get-Credential)
#>

[CmdletBinding()]
param(
    [string]$ServerInstance,
    [pscredential]$AdminCredential,
    [string]$LoginName = 'edfi_app',
    [string]$CredentialScript = 'C:\temp\ci-sql.ps1',
    [string]$SecretsOut = 'C:\temp\edfi-sql.ps1'
)

$ErrorActionPreference = 'Stop'

if ((-not $ServerInstance -or -not $AdminCredential) -and (Test-Path $CredentialScript)) {
    . $CredentialScript
    if (-not $ServerInstance -and $Global:CiSrv) { $ServerInstance = $Global:CiSrv }
    if (-not $AdminCredential -and $Global:CiUsr -and $Global:CiPwd) {
        $secure = ConvertTo-SecureString $Global:CiPwd -AsPlainText -Force
        $AdminCredential = New-Object System.Management.Automation.PSCredential($Global:CiUsr, $secure)
    }
}
if (-not $ServerInstance) { $ServerInstance = Read-Host 'SQL server (host or host,port)' }
if (-not $AdminCredential) { $AdminCredential = Get-Credential -Message "SQL login with securityadmin/sysadmin on $ServerInstance" }

$charset = [char[]]'ABCDEFGHJKLMNPQRSTUVWXYZabcdefghjkmnpqrstuvwxyz23456789!@#%^*-_+'
$bytes = New-Object byte[] 28
[System.Security.Cryptography.RandomNumberGenerator]::Create().GetBytes($bytes)
$newPassword = -join ($bytes | ForEach-Object { $charset[$_ % $charset.Length] })
$pwdSql = $newPassword.Replace("'", "''")

$adminPlain = $AdminCredential.GetNetworkCredential().Password
$connStr = "Server=$ServerInstance;Initial Catalog=master;User ID=$($AdminCredential.UserName);Password=$adminPlain;TrustServerCertificate=True;Encrypt=True"

Write-Host "=== Recreate $LoginName on $ServerInstance ===" -ForegroundColor Cyan

$conn = New-Object System.Data.SqlClient.SqlConnection $connStr
$conn.Open()
try {
    $cmd = $conn.CreateCommand()
    $cmd.CommandText = @"
IF EXISTS (SELECT 1 FROM sys.server_principals WHERE name = N'$LoginName')
    ALTER LOGIN [$LoginName] WITH PASSWORD = N'$pwdSql', CHECK_POLICY = OFF;
ELSE
    CREATE LOGIN [$LoginName] WITH PASSWORD = N'$pwdSql', CHECK_POLICY = OFF;
"@
    [void]$cmd.ExecuteNonQuery()
    $cmd.CommandText = "ALTER SERVER ROLE [sysadmin] ADD MEMBER [$LoginName];"
    [void]$cmd.ExecuteNonQuery()
}
finally { $conn.Close() }

Write-Host "Login created/reset and added to sysadmin (temporary)." -ForegroundColor Green

$testStr = "Server=$ServerInstance;Initial Catalog=master;User ID=$LoginName;Password=$newPassword;TrustServerCertificate=True;Encrypt=True"
$test = New-Object System.Data.SqlClient.SqlConnection $testStr
$test.Open()
try {
    $cmd = $test.CreateCommand()
    $cmd.CommandText = 'SELECT SYSTEM_USER'
    $who = [string]$cmd.ExecuteScalar()
}
finally { $test.Close() }
if ($who -ne $LoginName) { throw "Round-trip check failed: connected as '$who'." }
Write-Host "Round-trip connection verified as $who." -ForegroundColor Green

$secrets = @(
    "`$Global:EdfiSrv = '$ServerInstance'"
    "`$Global:EdfiUsr = '$LoginName'"
    "`$Global:EdfiPwd = '$newPassword'"
) -join [Environment]::NewLine
Set-Content -Path $SecretsOut -Value $secrets -Encoding ASCII

Write-Host ""
Write-Host "Credentials written to $SecretsOut (outside the repo - do not commit)." -ForegroundColor Yellow
Write-Host "Open with: notepad $SecretsOut  (you need the password for configuration.json on the VM)." -ForegroundColor Yellow
Write-Host "Reminder: drop $LoginName from sysadmin after PHX-3.3 completes." -ForegroundColor DarkGray
