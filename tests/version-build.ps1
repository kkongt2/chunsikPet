$ErrorActionPreference = 'Stop'
$path = Join-Path $PSScriptRoot '..\VERSION'
$original = [IO.File]::ReadAllBytes($path)
$exe = Join-Path $PSScriptRoot '..\ChunsikDesktopPet\bin\x64\Release\ChunsikPet.exe'
function Build-Version {
    & msbuild "$PSScriptRoot\..\ChunsikPet.sln" /t:Build /p:Configuration=Release /p:Platform=x64 /m /verbosity:quiet
    if ($LASTEXITCODE -ne 0) { throw 'Version build failed' }
}
function Check-Version([string]$expected) {
    $assembly = [Reflection.AssemblyName]::GetAssemblyName($exe).Version.ToString()
    $file = [Diagnostics.FileVersionInfo]::GetVersionInfo($exe)
    if ($assembly -ne "$expected.0" -or $file.FileVersion -ne "$expected.0" -or $file.ProductVersion -ne $expected) { throw "Version mismatch: $assembly / $($file.FileVersion) / $($file.ProductVersion)" }
}
try {
    Check-Version ([IO.File]::ReadAllText($path).Trim())
    [IO.File]::WriteAllText($path, "2.3.4`n")
    Build-Version
    Check-Version '2.3.4'
    foreach ($bad in @('', '1.2', '1.2.3.4', '1.2.x', '65535.1.1')) {
        [IO.File]::WriteAllText($path, $bad)
        & msbuild "$PSScriptRoot\..\ChunsikDesktopPet\ChunsikPet.csproj" /t:GenerateVersionInfo /p:Configuration=Release /p:Platform=x64 /verbosity:quiet
        if ($LASTEXITCODE -eq 0) { throw "Invalid VERSION accepted: $bad" }
    }
} finally {
    [IO.File]::WriteAllBytes($path, $original)
    Build-Version
}
Check-Version ([IO.File]::ReadAllText($path).Trim())
Write-Output 'PASS: VERSION drives assembly/file/product versions; incremental rebuild and invalid input checks'
exit 0
