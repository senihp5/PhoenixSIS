<#
    New-PhoenixDb.ps1  (PHX-5.1 — Phoenix application database on the NH VM)

    Creates the PhoenixSIS database and a LEAST-PRIVILEGE SQL login
    (phoenix_app: db_owner on PhoenixSIS only — never sysadmin) on the
    DecisionED SQL VM, then writes the connection string to a local
    secrets file OUTSIDE the repo (C:\temp\phoenix-sql.ps1).

    This is Phoenix's OWN clean-room store — unrelated to the Ed-Fi ODS
    databases. Run from the workstation over the Azure VPN. The admin
    credential is pulled from Key Vault (kv-decisioned-prod/sql-admin-password,
    login decisioned-admin) at runtime; pass -AdminCredential to override.

    Examples:
      .\New-PhoenixDb.ps1
      .\New-PhoenixDb.ps1 -AdminCredential (Get-Credential)
#>

[CmdletBinding()]
param(
    [string]$ServerInstance,
    [pscredential]$AdminCredential,
    [string]$DbName = 'PhoenixSIS',
    [string]$LoginName = 'phoenix_app',
    [string]$KeyVault = 'kv-decisioned-prod',
    [string]$AdminSecretName = 'sql-admin-password',
    [string]$AdminLogin = 'decisioned-admin',
    [string]$CredentialScript = 'C:\temp\ci-sql.ps1',
    [string]$SecretsOut = 'C:\temp\phoenix-sql.ps1'
)

$ErrorActionPreference = 'Stop'

if (-not $ServerInstance -and (Test-Path $CredentialScript)) {
    . $CredentialScript
    if ($Global:CiSrv) { $ServerInstance = $Global:CiSrv }
}
if (-not $ServerInstance) { $ServerInstance = '10.10.2.4' }

if (-not $AdminCredential) {
    $adminPwd = az keyvault secret show --vault-name $KeyVault -n $AdminSecretName --query value -o tsv
    if (-not $adminPwd) { throw "Could not read $AdminSecretName from $KeyVault. Pass -AdminCredential instead." }
    $AdminCredential = New-Object System.Management.Automation.PSCredential(
        $AdminLogin, (ConvertTo-SecureString $adminPwd -AsPlainText -Force))
    Remove-Variable adminPwd
}

$charset = [char[]]'ABCDEFGHJKLMNPQRSTUVWXYZabcdefghjkmnpqrstuvwxyz23456789!@#%^*-_+'
$bytes = New-Object byte[] 28
[System.Security.Cryptography.RandomNumberGenerator]::Create().GetBytes($bytes)
$newPassword = -join ($bytes | ForEach-Object { $charset[$_ % $charset.Length] })
$pwdSql = $newPassword.Replace("'", "''")

$adminPlain = $AdminCredential.GetNetworkCredential().Password
$connStr = "Server=$ServerInstance;Initial Catalog=master;User ID=$($AdminCredential.UserName);Password=$adminPlain;TrustServerCertificate=True;Encrypt=True"

Write-Host "=== Create $DbName + $LoginName on $ServerInstance ===" -ForegroundColor Cyan

$conn = New-Object System.Data.SqlClient.SqlConnection $connStr
$conn.Open()
try {
    $cmd = $conn.CreateCommand()
    $cmd.CommandTimeout = 120

    $cmd.CommandText = "IF DB_ID(N'$DbName') IS NULL CREATE DATABASE [$DbName];"
    [void]$cmd.ExecuteNonQuery()

    $cmd.CommandText = @"
IF NOT EXISTS (SELECT 1 FROM sys.server_principals WHERE name = N'$LoginName')
    CREATE LOGIN [$LoginName] WITH PASSWORD = N'$pwdSql', CHECK_POLICY = OFF;
ELSE
    ALTER LOGIN [$LoginName] WITH PASSWORD = N'$pwdSql', CHECK_POLICY = OFF;
"@
    [void]$cmd.ExecuteNonQuery()

    $cmd.CommandText = @"
USE [$DbName];
IF NOT EXISTS (SELECT 1 FROM sys.database_principals WHERE name = N'$LoginName')
    CREATE USER [$LoginName] FOR LOGIN [$LoginName];
ALTER ROLE [db_owner] ADD MEMBER [$LoginName];
"@
    [void]$cmd.ExecuteNonQuery()
}
finally { $conn.Close() }

Write-Host "$DbName created; $LoginName is db_owner on $DbName only (not sysadmin)." -ForegroundColor Green

$testStr = "Server=$ServerInstance;Initial Catalog=$DbName;User ID=$LoginName;Password=$newPassword;TrustServerCertificate=True;Encrypt=True"
$test = New-Object System.Data.SqlClient.SqlConnection $testStr
$test.Open()
try {
    $cmd = $test.CreateCommand()
    $cmd.CommandText = 'SELECT DB_NAME(), SYSTEM_USER'
    $rdr = $cmd.ExecuteReader(); [void]$rdr.Read()
    $db = $rdr.GetString(0); $who = $rdr.GetString(1); $rdr.Close()
}
finally { $test.Close() }
if ($db -ne $DbName -or $who -ne $LoginName) { throw "Round-trip check failed: db=$db user=$who." }
Write-Host "Round-trip verified: connected to $db as $who." -ForegroundColor Green

$efConnString = "Server=$ServerInstance;Database=$DbName;User Id=$LoginName;Password=$newPassword;TrustServerCertificate=True;Encrypt=True;Application Name=PhoenixSIS"
$secrets = @(
    "`$Global:PhoenixSqlSrv = '$ServerInstance'"
    "`$Global:PhoenixSqlDb = '$DbName'"
    "`$Global:PhoenixSqlUsr = '$LoginName'"
    "`$Global:PhoenixSqlPwd = '$newPassword'"
    "`$Global:PhoenixConnString = '$efConnString'"
) -join [Environment]::NewLine
Set-Content -Path $SecretsOut -Value $secrets -Encoding ASCII

Write-Host ""
Write-Host "Connection string written to $SecretsOut (outside the repo — do not commit)." -ForegroundColor Yellow
Write-Host "Set it into Phoenix.Web user-secrets with:" -ForegroundColor Yellow
Write-Host "  dotnet user-secrets set `"ConnectionStrings:PhoenixDb`" `$Global:PhoenixConnString --project src\Phoenix.Web" -ForegroundColor Yellow
