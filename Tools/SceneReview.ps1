param(
    [ValidateSet('Snapshot','Compare')][string]$Mode = 'Compare',
    [string]$Baseline = 'Docs/SceneReview/baseline.json',
    [string]$Output
)
$ErrorActionPreference = 'Stop'
$repo = Split-Path $PSScriptRoot -Parent
function RelativePath([string]$Path) {
    return [IO.Path]::GetRelativePath($repo, $Path).Replace('\','/')
}
function ResolveReportPath([string]$Path) {
    if ([IO.Path]::IsPathRooted($Path)) { return [IO.Path]::GetFullPath($Path) }
    return [IO.Path]::GetFullPath((Join-Path $repo $Path))
}
function Category([string]$Path) {
    if ($Path -match '/Editor/|/Tests_Optional/|Smoke\.cs$|Check(s)?\.cs$') { return 'Editor and verification' }
    if ($Path -match '/Passives/') { return 'Passive state and data' }
    if ($Path -match '/Stigmas/') { return 'Stigma state and combat' }
    if ($Path -match '/Room/') { return 'Generated expedition and map' }
    if ($Path -match '/Enemy/') { return 'Enemies and boss encounters' }
    if ($Path -match '/Cinematics/') { return 'Timeline and cinematics' }
    if ($Path -match 'DungeonBossTrial|DungeonTrainingStations|DungeonEntrance') { return 'Town entrances and debug rooms' }
    if ($Path -match '/Combat/|/(Player|Raven|CombatRules|CombatArt|SkillEffects)\.cs$') { return 'Player and combat' }
    if ($Path -match 'UI|HUD|Dialogue') { return 'UI and dialogue' }
    if ($Path -match '/Authoring/') { return 'Serialized scene and prefab bindings' }
    if ($Path -match '/(Dungeon|Rules|RunBuild|ExpeditionProgress|ExplorationStage|RoomDoor)\.cs$') { return 'Run lifecycle and routing' }
    return 'World presentation and animation'
}
$guidPaths = @{}
$duplicates = @()
Get-ChildItem (Join-Path $repo 'Assets') -Filter '*.meta' -Recurse -File | ForEach-Object {
    $match = [regex]::Match([IO.File]::ReadAllText($_.FullName), '(?m)^guid: ([a-f0-9]{32})')
    if ($match.Success) {
        $guid = $match.Groups[1].Value
        $asset = RelativePath $_.FullName.Substring(0, $_.FullName.Length - 5)
        if ($guidPaths.ContainsKey($guid)) { $duplicates += "$guid | $asset | $($guidPaths[$guid])" }
        $guidPaths[$guid] = $asset
    }
}
$files = @()
foreach ($folder in 'Assets','Packages','ProjectSettings') {
    $files += Get-ChildItem (Join-Path $repo $folder) -File -Recurse | Where-Object {
        $_.Extension -in '.cs','.unity','.prefab','.asset','.shader','.playable','.json','.meta','.txt'
    }
}
$manifest = [ordered]@{}
foreach ($file in ($files | Sort-Object FullName)) {
    $manifest[(RelativePath $file.FullName)] = (Get-FileHash -LiteralPath $file.FullName -Algorithm SHA256).Hash
}
$scripts = @(Get-ChildItem (Join-Path $repo 'Assets') -Filter '*.cs' -File -Recurse | Sort-Object FullName | ForEach-Object {
    $path = RelativePath $_.FullName
    [ordered]@{ path=$path; category=(Category $path) }
})
$scenes = @(Get-ChildItem (Join-Path $repo 'Assets') -Filter '*.unity' -Recurse -File | Sort-Object FullName | ForEach-Object {
    $sceneText = [IO.File]::ReadAllText($_.FullName)
    $blocks = [regex]::Matches($sceneText,'(?ms)^--- !u!(\d+) &(-?\d+)[^\r\n]*\r?\n(.*?)(?=^--- !u!|\z)')
    $names = @{}
    foreach ($block in $blocks) {
        if ($block.Groups[1].Value -eq '1') {
            $names[$block.Groups[2].Value] = [regex]::Match($block.Groups[3].Value,'(?m)^  m_Name: (.*)').Groups[1].Value.Trim()
        }
    }
    $roots = @()
    $components = @()
    foreach ($block in $blocks) {
        $body = $block.Groups[3].Value
        $go = [regex]::Match($body,'m_GameObject: \{fileID: (-?\d+)\}').Groups[1].Value
        if ($block.Groups[1].Value -in '4','224' -and $body -match 'm_Father: \{fileID: 0\}') {
            $roots += [ordered]@{ object=$names[$go]; transformId=$block.Groups[2].Value }
        }
        if ($block.Groups[1].Value -eq '114') {
            $guid = [regex]::Match($body,'m_Script: \{[^}]*guid: ([a-f0-9]{32})').Groups[1].Value
            $components += [ordered]@{ object=$names[$go]; fileId=$block.Groups[2].Value; script=$guidPaths[$guid]; scriptGuid=$guid }
        }
    }
    $references = @([regex]::Matches($sceneText,'guid: ([a-f0-9]{32})') | ForEach-Object { $_.Groups[1].Value } | Sort-Object -Unique | ForEach-Object {
        [ordered]@{ guid=$_; asset=$guidPaths[$_]; resolution= $(if ($guidPaths.ContainsKey($_)) {'Assets'} else {'Package, built-in, or unresolved; verify in Unity'}) }
    })
    [ordered]@{ path=(RelativePath $_.FullName); roots=$roots; components=$components; references=$references }
})
$buildText = [IO.File]::ReadAllText((Join-Path $repo 'ProjectSettings/EditorBuildSettings.asset'))
$buildScenes = @([regex]::Matches($buildText,'(?m)  - enabled: (\d)\r?\n    path: (.+)') | ForEach-Object {
    [ordered]@{ enabled=($_.Groups[1].Value -eq '1'); path=$_.Groups[2].Value.Trim() }
})
$head = & git -C $repo rev-parse HEAD
if ($LASTEXITCODE -ne 0) { throw 'Cannot read Git HEAD.' }
$status = @(& git -C $repo status --porcelain)
$snapshot = [ordered]@{
    schemaVersion=1; head="$head"; gitStatus=$status
    scope='Static inventory of Assets, Packages and ProjectSettings. No Unity compile or play validation. Hashes exclude image/audio bytes and generated caches.'
    scripts=$scripts; scenes=$scenes; buildScenes=$buildScenes
    duplicateAssetGuids=$duplicates; manifest=$manifest
}
$baselinePath = ResolveReportPath $Baseline
if ($Mode -eq 'Snapshot') {
    if (Test-Path -LiteralPath $baselinePath) { throw 'Baseline already exists. Use a new -Baseline path to preserve previous evidence.' }
    [IO.Directory]::CreateDirectory((Split-Path $baselinePath -Parent)) | Out-Null
    [IO.File]::WriteAllText($baselinePath,($snapshot | ConvertTo-Json -Depth 12),[Text.UTF8Encoding]::new($false))
    Write-Output "Snapshot written. Scripts=$($scripts.Count), scenes=$($scenes.Count), hashes=$($manifest.Count)"
} else {
    $before = Get-Content -LiteralPath $baselinePath -Raw | ConvertFrom-Json -AsHashtable
    $added = @($manifest.Keys | Where-Object { -not $before.manifest.ContainsKey($_) })
    $removed = @($before.manifest.Keys | Where-Object { -not $manifest.Contains($_) })
    $changed = @($manifest.Keys | Where-Object { $before.manifest.ContainsKey($_) -and $manifest[$_] -ne $before.manifest[$_] })
    $report = [ordered]@{
        baselineHead=$before.head; currentHead=$head; added=$added; removed=$removed; changed=$changed
        buildScenes=$buildScenes; duplicateAssetGuids=$duplicates
        runtimeValidation='NOT RUN'; interpretation='Changes require review. Exit 0 only means static structural checks passed.'
    }
    if ($Output) {
        $outputPath = ResolveReportPath $Output
        if ($outputPath -eq $baselinePath) { throw 'Output must not overwrite baseline.' }
        [IO.Directory]::CreateDirectory((Split-Path $outputPath -Parent)) | Out-Null
        [IO.File]::WriteAllText($outputPath,($report | ConvertTo-Json -Depth 10),[Text.UTF8Encoding]::new($false))
    }
    $report | ConvertTo-Json -Depth 10
}
if ($duplicates.Count) { throw 'Duplicate asset GUIDs found. See snapshot/report.' }
foreach ($scene in $buildScenes) {
    if ($scene.enabled -and -not (Test-Path -LiteralPath (Join-Path $repo $scene.path))) { throw "Enabled build scene missing: $($scene.path)" }
}
