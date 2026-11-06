<#
    Seed-EdfiReference.ps1  (PHX-6.2 — Texas-shaped Ed-Fi reference data)

    Seeds a New-Heights-shaped education-organization structure into the Ed-Fi
    ODS so synced students (Sprint 7) have valid targets:
      - LocalEducationAgency 220822 "New Heights" (charter, Fort Worth / Tarrant)
      - School 220822001 "New Heights Adult High School" (Adult School / Adult Education)

    Uses the sandbox key/secret from C:\temp\edfi-sandbox.ps1. Idempotent: Ed-Fi
    POST is upsert-by-natural-key, so re-running updates in place (no duplicates).
    Retries transient cold-start failures. Sample data only.

    NOTE on Texas descriptors: this seeder references the national Ed-Fi
    descriptor set already in the ODS (LEA category "Charter", school category
    "Adult School", grade level "Adult Education"). It deliberately does NOT
    create uri://tea.texas.gov-namespaced descriptors — real TEDS descriptor
    namespaces/extensions are a separate, TEA-platform concern (see
    docs\integrations\teds-gap-analysis.md).

    Examples:
      .\Seed-EdfiReference.ps1
#>

[CmdletBinding()]
param(
    [string]$CredentialScript = 'C:\temp\edfi-sandbox.ps1',
    [int]$LeaId = 220822,
    [int]$SchoolId = 220822001
)

$ErrorActionPreference = 'Stop'
[Net.ServicePointManager]::SecurityProtocol += [Net.SecurityProtocolType]::Tls12

if (-not (Test-Path $CredentialScript)) { throw "Credential file not found: $CredentialScript" }
. $CredentialScript
if (-not $Global:EdfiSandboxKey -or -not $Global:EdfiApiBase) { throw "$CredentialScript did not set the sandbox creds." }

$ns = 'uri://ed-fi.org'
$dataBase = "$($Global:EdfiApiBase)/data/v3/ed-fi"

function Get-Token {
    $auth = 'Basic ' + [Convert]::ToBase64String(
        [Text.Encoding]::ASCII.GetBytes("$($Global:EdfiSandboxKey):$($Global:EdfiSandboxSecret)"))
    (Invoke-RestMethod "$($Global:EdfiApiBase)/oauth/token" -Method Post `
        -Body @{ grant_type = 'client_credentials' } -Headers @{ Authorization = $auth } -TimeoutSec 60).access_token
}

# Retry wrapper for the App Service cold-start flakiness (transient 404/502/503).
function Invoke-Edfi([string]$method, [string]$url, $body, [hashtable]$headers) {
    for ($i = 0; $i -lt 5; $i++) {
        try {
            $json = if ($body) { $body | ConvertTo-Json -Depth 10 } else { $null }
            return Invoke-WebRequest -Method $method -Uri $url -Headers $headers `
                -ContentType 'application/json' -Body $json -TimeoutSec 60 -UseBasicParsing
        }
        catch {
            $code = $_.Exception.Response.StatusCode.value__
            if ($code -in 404, 408, 429, 500, 502, 503, 504 -and $i -lt 4) { Start-Sleep 4; continue }
            throw
        }
    }
}

$token = Get-Token
$h = @{ Authorization = "Bearer $token" }

Write-Host "=== Seeding New-Heights-shaped reference data ===" -ForegroundColor Cyan

$lea = @{
    localEducationAgencyId              = $LeaId
    nameOfInstitution                   = 'New Heights'
    shortNameOfInstitution              = 'New Heights'
    localEducationAgencyCategoryDescriptor = "$ns/LocalEducationAgencyCategoryDescriptor#Charter"
    categories                          = @(
        @{ educationOrganizationCategoryDescriptor = "$ns/EducationOrganizationCategoryDescriptor#Local Education Agency" }
    )
    addresses                           = @(
        @{
            addressTypeDescriptor      = "$ns/AddressTypeDescriptor#Physical"
            city                       = 'Fort Worth'
            postalCode                 = '76104'
            stateAbbreviationDescriptor = "$ns/StateAbbreviationDescriptor#TX"
            streetNumberName           = '201 Sample Street'
            nameOfCounty               = 'Tarrant'
        }
    )
}

$school = @{
    schoolId                     = $SchoolId
    nameOfInstitution            = 'New Heights Adult High School'
    localEducationAgencyReference = @{ localEducationAgencyId = $LeaId }
    educationOrganizationCategories = @(
        @{ educationOrganizationCategoryDescriptor = "$ns/EducationOrganizationCategoryDescriptor#School" }
    )
    schoolCategories             = @(
        @{ schoolCategoryDescriptor = "$ns/SchoolCategoryDescriptor#Adult School" }
    )
    gradeLevels                  = @(
        @{ gradeLevelDescriptor = "$ns/GradeLevelDescriptor#Adult Education" }
    )
}

$r1 = Invoke-Edfi 'Post' "$dataBase/localEducationAgencies" $lea $h
Write-Host ("  LEA {0} 'New Heights'  -> HTTP {1}" -f $LeaId, $r1.StatusCode) -ForegroundColor Green

$r2 = Invoke-Edfi 'Post' "$dataBase/schools" $school $h
Write-Host ("  School {0} 'New Heights Adult High School' -> HTTP {1}" -f $SchoolId, $r2.StatusCode) -ForegroundColor Green

# Verify by natural key
$leaCheck    = Invoke-RestMethod "$dataBase/localEducationAgencies?localEducationAgencyId=$LeaId" -Headers $h -TimeoutSec 60
$schoolCheck = Invoke-RestMethod "$dataBase/schools?schoolId=$SchoolId" -Headers $h -TimeoutSec 60

Write-Host ''
if (@($leaCheck).Count -ge 1 -and @($schoolCheck).Count -ge 1) {
    Write-Host 'Verified:' -ForegroundColor Green
    Write-Host ("  LEA    {0}: {1} ({2})" -f $LeaId, @($leaCheck)[0].nameOfInstitution, @($leaCheck)[0].localEducationAgencyCategoryDescriptor.Split('#')[-1])
    Write-Host ("  School {0}: {1} ({2}, {3})" -f $SchoolId, @($schoolCheck)[0].nameOfInstitution, @($schoolCheck)[0].schoolCategories[0].schoolCategoryDescriptor.Split('#')[-1], @($schoolCheck)[0].gradeLevels[0].gradeLevelDescriptor.Split('#')[-1])
    Write-Host ''
    Write-Host 'Reference seed complete. Re-running this script is a no-op (upsert by natural key).' -ForegroundColor Cyan
}
else {
    throw 'Verification failed: LEA or School not found after seeding.'
}
