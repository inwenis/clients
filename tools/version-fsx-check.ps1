<#
.SYNOPSIS
  Ensures .fsx #r "nuget: ..." pins match versions locked by Paket.

.DESCRIPTION
  - Reads paket.lock and builds a package->version map for the NUGET sections.
  - Scans all .fsx files for lines like: #r "nuget: FSharp.Data, 6.3.0"
  - Emits warnings on mismatches and suggestions when version is missing.
  - ensures the below issues are avoided
    - https://youtrack.jetbrains.com/issue/RIDER-127413
    - https://github.com/dotnet/fsharp/issues/11135
#>

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

function Get-PaketLockedVersions {
    param([string]$LockPath = "paket.lock")

    if (-not (Test-Path $LockPath)) {
        throw "paket.lock not found at: $LockPath (run 'paket install' or ensure you are in the repo root)."
    }

    $versions = @{}
    $inNuget = $false

    # paket.lock is indentation-based; packages are typically under "NUGET"
    # Example line: "    FSharp.Data (6.3.0)"
    $pkgLine = '^\s{2,}([A-Za-z0-9\.\-_]+)\s+\(([^)]+)\)\s*$'

    Get-Content -LiteralPath $LockPath | ForEach-Object {
        $line = $_

        if ($line -match '^\s*NUGET\s*$') { $inNuget = $true; return }
        if ($line -match '^\S' -and -not ($line -match '^\s*NUGET\s*$')) { $inNuget = $false }

        if ($inNuget -and ($line -match $pkgLine)) {
            $name = $Matches[1]
            $ver  = $Matches[2]

            # paket.lock can occasionally pin multiples across groups; keep first occurrence per package name
            if (-not $versions.ContainsKey($name)) {
                $versions[$name] = $ver
            }
        }
    }

    return $versions
}

function Get-FsxNugetRefs {
    param([string]$Root = ".")
    $rx = '#r\s*"nuget:\s*([^," ]+)\s*(?:,\s*([^"]+))?"'

    $results = @()
    Get-ChildItem -Path $Root -Recurse -Filter *.fsx -File | ForEach-Object {
        $file = $_.FullName
        $i = 0
        Get-Content -LiteralPath $file | ForEach-Object {
            $i++
            if ($_ -match $rx) {
                $results += [pscustomobject]@{
                    File    = $file
                    Line    = $i
                    Package = $Matches[1]
                    Version = $Matches[2]  # can be $null if not specified
                    Text    = $_.Trim()
                }
            }
        }
    }
    return $results
}

function Compare-FsxAgainstPaket {
    param([hashtable]$PaketVersions, [object[]]$FsxRefs)

    $warnings = 0

    foreach ($ref in $FsxRefs) {
        $pkg = $ref.Package
        $fsxVer = $ref.Version
        $paketVer = $PaketVersions[$pkg]

        if (-not $paketVer) {
            Write-Warning ("{0}:{1}: Package '{2}' is referenced in .fsx but not found in paket.lock." -f $ref.File, $ref.Line, $pkg)
            $warnings++
            continue
        }

        if ([string]::IsNullOrWhiteSpace($fsxVer)) {
            Write-Warning ("{0}:{1}: Package '{2}' has NO version pinned in .fsx. Consider pinning '{2}, {3}' to match paket.lock." -f $ref.File, $ref.Line, $pkg, $paketVer)
            $warnings++
            continue
        }

        if ($fsxVer -ne $paketVer) {
            Write-Warning ("{0}:{1}: Version mismatch for '{2}'. .fsx pins '{3}' but paket.lock pins '{4}'." -f $ref.File, $ref.Line, $pkg, $fsxVer, $paketVer)
            $warnings++
        }
    }

    if ($warnings -eq 0) {
        Write-Host "✅ All .fsx NuGet pins match paket.lock." -ForegroundColor Green
        return 0
    } else {
        Write-Host "⚠️  Found $warnings issue(s). See warnings above." -ForegroundColor Yellow
        return 2
    }
}

# ---- main ----
try {
    $paketVersions = Get-PaketLockedVersions -LockPath "paket.lock"
    if ($paketVersions.Count -eq 0) {
        Write-Host "No packages parsed from paket.lock NUGET section(s). Nothing to compare." -ForegroundColor Yellow
        exit 3
    }

    $fsxRefs = Get-FsxNugetRefs -Root "."
    if ($fsxRefs.Count -eq 0) {
        Write-Host "No '#r ""nuget: ...""' references found in any .fsx file." -ForegroundColor Yellow
        exit 3
    }

    $code = Compare-FsxAgainstPaket -PaketVersions $paketVersions -FsxRefs $fsxRefs
    exit $code
}
catch {
    Write-Error $_
    exit 1
}
