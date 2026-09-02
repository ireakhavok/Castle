# Unity / FBX pack inventory
# Windows PowerShell 5.1 compatible. ASCII only.
#
#   powershell -NoProfile -ExecutionPolicy Bypass -File .\UnityPackInventory.ps1
#   powershell -NoProfile -ExecutionPolicy Bypass -File .\UnityPackInventory.ps1 -Path "C:\repos\Castle\Castle\Assets\FantasySlavicHut"

param(
    [string]$Path = ".",
    [string]$Out = ""
)

$ErrorActionPreference = "Stop"

function RelPath($root, $full) {
    $prefix = $root.TrimEnd("\", "/") + "\"
    if ($full.Length -ge $prefix.Length -and $full.Substring(0, $prefix.Length).ToLower() -eq $prefix.ToLower()) {
        return $full.Substring($prefix.Length)
    }
    return $full
}

function ReadAll($file) {
    try { return [System.IO.File]::ReadAllText($file) } catch { return "" }
}

function GetMetaGuid($metaPath) {
    $text = ReadAll $metaPath
    $m = [regex]::Match($text, '(?m)^guid:\s*([0-9a-fA-F]{32})\s*$')
    if ($m.Success) { return $m.Groups[1].Value.ToLower() }
    return $null
}

function GetMatName($text, $fallback) {
    $m = [regex]::Match($text, '(?m)^\s*m_Name:\s*(.+)\s*$')
    if ($m.Success) {
        $n = $m.Groups[1].Value.Trim().Trim("'").Trim('"')
        if ($n -ne "" -and $n -ne "---") { return $n }
    }
    return $fallback
}

function SlotHint($slot) {
    switch -Regex ($slot) {
        '_MainTex|_BaseMap|_BaseColorMap|_Diffuse|_Albedo' { return "albedo" }
        '_BumpMap|_NormalMap|_Bump' { return "normal" }
        '_MetallicGlossMap|_Metallic|_MaskMap' { return "metallic" }
        '_EmissionMap|_Emission' { return "emission" }
        '_OcclusionMap|_Occlusion|_AO' { return "ao" }
        default { return "other" }
    }
}

function FbxHints($fbxPath) {
    $bytes = [System.IO.File]::ReadAllBytes($fbxPath)
    $cur = New-Object System.Text.StringBuilder
    $hits = New-Object System.Collections.Generic.List[string]
    $seen = @{}
    foreach ($b in $bytes) {
        if ($b -ge 32 -and $b -le 126) {
            [void]$cur.Append([char]$b)
        } else {
            if ($cur.Length -ge 5) {
                $s = $cur.ToString()
                $keep = $false
                if ($s -match '\.(png|jpe?g|tga|tiff?|psd|bmp|exr)$') { $keep = $true }
                elseif ($s -match '(?i)(texture|diffuse|albedo|normal|bump|emissive|metallic|roughness|atlas|material)') { $keep = $true }
                if ($keep -and $s.Length -lt 80 -and -not $seen.ContainsKey($s)) {
                    $seen[$s] = $true
                    $hits.Add($s)
                }
            }
            [void]$cur.Clear()
        }
        if ($hits.Count -ge 40) { break }
    }
    return $hits
}

if (-not (Test-Path -LiteralPath $Path)) {
    throw "Path not found: $Path"
}

$root = (Resolve-Path -LiteralPath $Path).Path
if (-not (Test-Path -LiteralPath $root -PathType Container)) {
    $root = Split-Path -Parent $root
}

$assets = Join-Path $root "Assets"
if (Test-Path -LiteralPath $assets -PathType Container) {
    Write-Host "Detected Unity project. Scanning Assets only."
    $root = (Resolve-Path -LiteralPath $assets).Path
}

Write-Host ("Scanning: " + $root)

$skip = @("Library", "Temp", "Obj", "Logs", "Build", "Builds", ".git", ".vs", "node_modules", "PackageCache")
$texExt = @(".png", ".jpg", ".jpeg", ".tga", ".tif", ".tiff", ".psd", ".exr", ".bmp", ".gif", ".webp")

$all = Get-ChildItem -LiteralPath $root -Recurse -File -ErrorAction SilentlyContinue | Where-Object {
    $ok = $true
    $rel = RelPath $root $_.FullName
    foreach ($part in $rel.Split("\")) {
        if ($skip -contains $part) { $ok = $false }
    }
    $ok
}

$fbxFiles = @($all | Where-Object { $_.Extension -eq ".fbx" })
$matFiles = @($all | Where-Object { $_.Extension -eq ".mat" })
$texFiles = @($all | Where-Object { $texExt -contains $_.Extension.ToLower() })
$metaFiles = @($all | Where-Object { $_.Extension -eq ".meta" })
$prefabFiles = @($all | Where-Object { $_.Extension -eq ".prefab" })

$guidMap = @{}
foreach ($meta in $metaFiles) {
    $guid = GetMetaGuid $meta.FullName
    if (-not $guid) { continue }
    $asset = $meta.FullName
    if ($asset.ToLower().EndsWith(".meta")) {
        $asset = $asset.Substring(0, $asset.Length - 5)
    }
    if (Test-Path -LiteralPath $asset) {
        $guidMap[$guid] = $asset
    }
}

$lines = New-Object System.Collections.Generic.List[string]
function OutLine($t) {
    $lines.Add($t)
    Write-Host $t
}

OutLine "Unity / FBX pack inventory"
OutLine ("Generated: " + (Get-Date -Format "yyyy-MM-dd HH:mm"))
OutLine ("Root: " + $root)
OutLine ""
OutLine "Counts"
OutLine ("  FBX:        " + $fbxFiles.Count)
OutLine ("  Materials:  " + $matFiles.Count)
OutLine ("  Textures:   " + $texFiles.Count)
OutLine ("  Prefabs:    " + $prefabFiles.Count)
OutLine ("  .meta:      " + $metaFiles.Count)
OutLine ("  GUID index: " + $guidMap.Count)
OutLine ""

OutLine "Folders (depth 4)"
$dirs = Get-ChildItem -LiteralPath $root -Recurse -Directory -ErrorAction SilentlyContinue | Where-Object {
    $rel = RelPath $root $_.FullName
    $depth = ($rel.Split("\")).Count
    $bad = $false
    foreach ($part in $rel.Split("\")) {
        if ($skip -contains $part) { $bad = $true }
    }
    (-not $bad) -and ($depth -le 4)
}
$n = 0
foreach ($d in ($dirs | Sort-Object FullName)) {
    if ($n -ge 80) { break }
    OutLine ("  [dir] " + (RelPath $root $d.FullName))
    $n++
}
OutLine ""

OutLine "Textures"
if ($texFiles.Count -eq 0) {
    OutLine "  (none found. Copy the pack Textures folder next to the FBXs.)"
} else {
    $n = 0
    foreach ($t in ($texFiles | Sort-Object FullName)) {
        if ($n -ge 80) { break }
        $kb = [int][math]::Round($t.Length / 1KB)
        OutLine ("  " + $kb + "KB  " + (RelPath $root $t.FullName))
        $n++
    }
    if ($texFiles.Count -gt 80) {
        OutLine ("  ... " + ($texFiles.Count - 80) + " more")
    }
}
OutLine ""

OutLine "Unity materials (.mat) resolved via .meta GUIDs"
if ($matFiles.Count -eq 0) {
    OutLine "  (no .mat files. If you only copied FBXs, also copy Materials and Textures.)"
} else {
    $n = 0
    foreach ($mat in ($matFiles | Sort-Object FullName)) {
        if ($n -ge 60) { break }
        $text = ReadAll $mat.FullName
        if ($text -eq "") { continue }
        $name = GetMatName $text $mat.BaseName
        OutLine ("  MATERIAL  " + $name)
        OutLine ("            file: " + (RelPath $root $mat.FullName))
        $foundSlot = $false
        foreach ($m in [regex]::Matches($text, '(?ms)^\s{0,4}-\s+(_[A-Za-z0-9]+):\s.*?m_Texture:\s*\{([^}]*)\}')) {
            $slot = $m.Groups[1].Value
            $body = $m.Groups[2].Value
            $gm = [regex]::Match($body, 'guid:\s*([0-9a-fA-F]{32})')
            $fm = [regex]::Match($body, 'fileID:\s*(-?\d+)')
            $guid = $null
            if ($gm.Success) { $guid = $gm.Groups[1].Value.ToLower() }
            $fileId = ""
            if ($fm.Success) { $fileId = $fm.Groups[1].Value }
            if ($fileId -eq "0" -and -not $guid) { continue }
            $foundSlot = $true
            $hint = SlotHint $slot
            if ($guid -and $guidMap.ContainsKey($guid)) {
                OutLine ("            " + $slot + " -> " + $hint + "  " + (RelPath $root $guidMap[$guid]))
            } elseif ($guid) {
                OutLine ("            " + $slot + " -> " + $hint + "  GUID " + $guid + " (file not in this folder)")
            } else {
                OutLine ("            " + $slot + " -> (empty)")
            }
        }
        if (-not $foundSlot) {
            OutLine "            textures: (none referenced)"
        }
        OutLine ""
        $n++
    }
    if ($matFiles.Count -gt 60) {
        OutLine ("  ... " + ($matFiles.Count - 60) + " more materials")
    }
}

OutLine "FBX files + name hints"
if ($fbxFiles.Count -eq 0) {
    OutLine "  (no .fbx files found)"
} else {
    $n = 0
    foreach ($fbx in ($fbxFiles | Sort-Object FullName)) {
        if ($n -ge 40) { break }
        $kb = [int][math]::Round($fbx.Length / 1KB)
        OutLine ("  FBX  " + (RelPath $root $fbx.FullName) + "  (" + $kb + " KB)")
        if ($fbx.Length -gt 80MB) {
            OutLine "       (skipped string scan, FBX larger than 80MB)"
        } else {
            try {
                $hints = @(FbxHints $fbx.FullName)
                if ($hints.Count -eq 0) {
                    OutLine "       (no material/texture strings inside FBX. Textures are external.)"
                } else {
                    foreach ($h in $hints) {
                        OutLine ("       hint: " + $h)
                    }
                }
            } catch {
                OutLine ("       (could not scan binary: " + $_.Exception.Message + ")")
            }
        }
        OutLine ""
        $n++
    }
    if ($fbxFiles.Count -gt 40) {
        OutLine ("  ... " + ($fbxFiles.Count - 40) + " more FBX files")
    }
}

OutLine "How to read this"
OutLine "  - If Textures exist and materials resolve to PNGs, assign those PNGs in Blender."
OutLine "  - One atlas PNG used by every slot is common for store packs."
OutLine "  - If GUIDs do not resolve, the Textures folder is missing from this copy."
OutLine "  - Collision hulls: delete meshes named UCX_/UBX_/Col/Collider/Hull."
OutLine "  - Paste pack-inventory.txt back for a slot-by-slot assignment list."

$report = [string]::Join([Environment]::NewLine, $lines)
if ($Out -eq "") {
    $Out = Join-Path $root "pack-inventory.txt"
}
try {
    [System.IO.File]::WriteAllText($Out, $report)
    Write-Host ""
    Write-Host ("Wrote " + $Out)
} catch {
    $fallback = Join-Path $env:TEMP "pack-inventory.txt"
    [System.IO.File]::WriteAllText($fallback, $report)
    Write-Host ""
    Write-Host ("Wrote " + $fallback)
}