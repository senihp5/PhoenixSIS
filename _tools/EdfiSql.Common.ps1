<#
    EdfiSql.Common.ps1 - shared helpers for the Ed-Fi ops scripts.

    Dot-source it:  . (Join-Path $PSScriptRoot 'EdfiSql.Common.ps1')
#>

function Get-EdfiDbUserGrantSql {
    <#
        Returns T-SQL that:
          1. makes $Owner the owner of $Database - this re-syncs the database's
             internal dbo, which is stale in any database restored from a .bak
             (dbo keeps the ORIGINAL owner's SID, so the restoring login is not
             actually mapped inside the database);
          2. maps $LoginName as a real database user in db_owner.
        Idempotent: creates the user, re-links an orphaned user of the same name,
        or reuses an existing user that already carries the login's SID.
        Ends the batch back in master so the caller's connection context is
        unchanged. Must run as a sysadmin.
    #>
    param(
        [Parameter(Mandatory)][string]$Database,
        [Parameter(Mandatory)][string]$LoginName,
        [string]$Owner = 'sa'
    )

    $db = $Database.Replace(']', ']]')
    $loginLit = $LoginName.Replace("'", "''")
    $loginBr = $LoginName.Replace(']', ']]')
    $ownerBr = $Owner.Replace(']', ']]')

    @"
ALTER AUTHORIZATION ON DATABASE::[$db] TO [$ownerBr];
USE [$db];
DECLARE @sid varbinary(85) = SUSER_SID(N'$loginLit');
DECLARE @user sysname = (SELECT name FROM sys.database_principals WHERE sid = @sid);
IF @user IS NULL AND EXISTS (SELECT 1 FROM sys.database_principals WHERE name = N'$loginLit')
    ALTER USER [$loginBr] WITH LOGIN = [$loginBr];
ELSE IF @user IS NULL
    CREATE USER [$loginBr] FOR LOGIN [$loginBr];
SET @user = (SELECT name FROM sys.database_principals WHERE sid = @sid);
IF NOT EXISTS (
    SELECT 1 FROM sys.database_role_members rm
    JOIN sys.database_principals r ON r.principal_id = rm.role_principal_id
    JOIN sys.database_principals m ON m.principal_id = rm.member_principal_id
    WHERE r.name = N'db_owner' AND m.sid = @sid)
BEGIN
    DECLARE @grant nvarchar(400) = N'ALTER ROLE [db_owner] ADD MEMBER ' + QUOTENAME(@user) + N';';
    EXEC (@grant);
END;
USE [master];
"@
}
