<#
    Test-EdfiStudentCrud.ps1  (PHX-3.5 — full student CRUD cycle, also the 3.6 regression test)

    Runs the complete lifecycle against the Phoenix sandbox:
      1. OAuth client-credentials token
      2. POST   /ed-fi/students                          (create - fake test student)
      3. GET    /ed-fi/students/{id}                     (read back)
      4. POST   /ed-fi/studentSchoolAssociations         (enroll in a real school found via the API)
      5. PUT    /ed-fi/students/{id}                     (update middleName)
      6. GET    /ed-fi/students/{id}                     (verify update)
      7. DELETE /ed-fi/studentSchoolAssociations/{id}
      8. DELETE /ed-fi/students/{id}
      9. GET    /ed-fi/students/{id}                     (expect 404)

    Sandbox key/secret come from C:\temp\edfi-sandbox.ps1 (New-EdfiSandbox.ps1).
    Sample data only - never run against an ODS with real students.
#>

[CmdletBinding()]
param(
    [string]$CredentialScript = 'C:\temp\edfi-sandbox.ps1'
)

$ErrorActionPreference = 'Stop'
[Net.ServicePointManager]::SecurityProtocol += [Net.SecurityProtocolType]::Tls12

if (-not (Test-Path $CredentialScript)) { throw "Credential file not found: $CredentialScript. Run New-EdfiSandbox.ps1 first." }
. $CredentialScript
if (-not $Global:EdfiSandboxKey -or -not $Global:EdfiSandboxSecret -or -not $Global:EdfiApiBase) { throw "$CredentialScript did not set EdfiSandboxKey/Secret/ApiBase." }

$base = "$($Global:EdfiApiBase)/data/v3"
$script:passed = 0; $script:failed = 0

function Step([string]$name, [scriptblock]$action) {
    try {
        $result = & $action
        $script:passed++
        Write-Host ("  PASS  {0}{1}" -f $name, $(if ($result) { " - $result" } else { '' })) -ForegroundColor Green
        return $true
    }
    catch {
        $script:failed++
        $detail = if ($_.ErrorDetails.Message) { $_.ErrorDetails.Message } else { $_.Exception.Message }
        Write-Host ("  FAIL  {0} - {1}" -f $name, $detail) -ForegroundColor Red
        return $false
    }
}

Write-Host '=== PHX-3.5 Student CRUD cycle ===' -ForegroundColor Cyan

$auth = 'Basic ' + [Convert]::ToBase64String([Text.Encoding]::ASCII.GetBytes("$($Global:EdfiSandboxKey):$($Global:EdfiSandboxSecret)"))
$token = $null
[void](Step 'OAuth token' {
    $t = Invoke-RestMethod "$($Global:EdfiApiBase)/oauth/token" -Method Post -Body @{ grant_type = 'client_credentials' } -Headers @{ Authorization = $auth } -TimeoutSec 120
    $script:token = $t.access_token
    "expires_in $($t.expires_in)"
})
if (-not $script:token) { throw 'No token - aborting.' }
$h = @{ Authorization = "Bearer $($script:token)" }
$hj = $h + @{ 'Content-Type' = 'application/json' }

$schoolId = $null; $gradeLevel = $null
[void](Step 'Discover school (LEA 255901) + grade level' {
    $schools = Invoke-RestMethod "$base/ed-fi/schools?limit=25" -Headers $h -TimeoutSec 120
    $school = $null
    foreach ($s in $schools) {
        if ($s.localEducationAgencyReference.localEducationAgencyId -eq 255901) { $school = $s; break }
    }
    if (-not $school) { foreach ($s in $schools) { $school = $s; break } }
    $script:schoolId = $school.schoolId
    $grades = Invoke-RestMethod "$base/ed-fi/gradeLevelDescriptors?limit=100" -Headers $h -TimeoutSec 120
    $g = $null
    foreach ($d in $grades) {
        if ($d.codeValue -eq 'Twelfth grade') { $g = $d; break }
        if (-not $g) { $g = $d }
    }
    $script:gradeLevel = "$($g.namespace)#$($g.codeValue)"
    "school $($script:schoolId) ($($school.nameOfInstitution)), grade '$($g.codeValue)'"
})

$uniqueId = "PHXTEST$(Get-Random -Minimum 100000 -Maximum 999999)"
$studentId = $null
[void](Step "POST student ($uniqueId)" {
    $body = @{ studentUniqueId = $uniqueId; firstName = 'Testy'; lastSurname = 'McTestface'; birthDate = '1990-01-15' } | ConvertTo-Json
    $r = Invoke-WebRequest "$base/ed-fi/students" -Method Post -Body $body -Headers $hj -UseBasicParsing -TimeoutSec 120
    $script:studentId = ([string]$r.Headers['Location']).TrimEnd('/').Split('/')[-1]
    "HTTP $($r.StatusCode), id $($script:studentId)"
})
if (-not $script:studentId) { throw 'No student id - aborting.' }

$ssaId = $null
[void](Step 'POST studentSchoolAssociation (enroll - REQUIRED before read: relationship-based auth)' {
    $body = @{
        studentReference         = @{ studentUniqueId = $uniqueId }
        schoolReference          = @{ schoolId = $script:schoolId }
        entryDate                = '2026-08-17'
        entryGradeLevelDescriptor = $script:gradeLevel
    } | ConvertTo-Json
    $r = Invoke-WebRequest "$base/ed-fi/studentSchoolAssociations" -Method Post -Body $body -Headers $hj -UseBasicParsing -TimeoutSec 120
    $script:ssaId = ([string]$r.Headers['Location']).TrimEnd('/').Split('/')[-1]
    "HTTP $($r.StatusCode), id $($script:ssaId)"
})

[void](Step 'GET student by id (authorized via the new association)' {
    $s = Invoke-RestMethod "$base/ed-fi/students/$($script:studentId)" -Headers $h -TimeoutSec 120
    "$($s.firstName) $($s.lastSurname), born $($s.birthDate)"
})

[void](Step 'PUT student (add middleName)' {
    $body = @{ id = $script:studentId; studentUniqueId = $uniqueId; firstName = 'Testy'; middleName = 'Updated'; lastSurname = 'McTestface'; birthDate = '1990-01-15' } | ConvertTo-Json
    $r = Invoke-WebRequest "$base/ed-fi/students/$($script:studentId)" -Method Put -Body $body -Headers $hj -UseBasicParsing -TimeoutSec 120
    "HTTP $($r.StatusCode)"
})

[void](Step 'GET student (verify update)' {
    $s = Invoke-RestMethod "$base/ed-fi/students/$($script:studentId)" -Headers $h -TimeoutSec 120
    if ($s.middleName -ne 'Updated') { throw "middleName is '$($s.middleName)', expected 'Updated'" }
    "middleName = '$($s.middleName)'"
})

if ($script:ssaId) {
    [void](Step 'DELETE studentSchoolAssociation' {
        $r = Invoke-WebRequest "$base/ed-fi/studentSchoolAssociations/$($script:ssaId)" -Method Delete -Headers $h -UseBasicParsing -TimeoutSec 120
        "HTTP $($r.StatusCode)"
    })
}

[void](Step 'DELETE student' {
    $r = Invoke-WebRequest "$base/ed-fi/students/$($script:studentId)" -Method Delete -Headers $h -UseBasicParsing -TimeoutSec 120
    "HTTP $($r.StatusCode)"
})

[void](Step 'GET deleted student (expect 404)' {
    try {
        Invoke-RestMethod "$base/ed-fi/students/$($script:studentId)" -Headers $h -TimeoutSec 120 | Out-Null
        throw 'Student still exists after DELETE'
    }
    catch {
        if ($_.Exception.Response -and [int]$_.Exception.Response.StatusCode -eq 404) { return 'HTTP 404 as expected' }
        throw
    }
})

Write-Host ''
$color = if ($script:failed -eq 0) { 'Green' } else { 'Red' }
Write-Host ("=== Result: {0} passed, {1} failed ===" -f $script:passed, $script:failed) -ForegroundColor $color
if ($script:failed -gt 0) { exit 1 }
