<#
.SYNOPSIS
  Створює новий застосунок із шаблону t-template.

.EXAMPLE
  ./new-app.ps1 -Name MyPet                          # спитає, які фічі вмикати
  ./new-app.ps1 -Name MyPet -Features tickets,jobs   # фічі списком
  ./new-app.ps1 -Name MyPet -Features none -Path D:\projects\my-pet -Verify

.DESCRIPTION
  Копіює шаблон (без .git, node_modules, bin/obj, dist, data), для невибраних фіч видаляє їхні теки (template.json)
  і вирізає блоки між маркерами "// #feature:<id>" … "// #end" (у Markdown — "<!-- #feature:<id> -->" … "<!-- #end -->");
  маркери вибраних фіч просто прибирає. Перейменовує TTemplate → <Name>, t-template → <name-in-kebab-case>,
  робить git init і npm install. -Verify — ще й збирає бекенд і UI.
#>
[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)][string]$Name,
    [string]$Path,
    [string[]]$Features,
    [switch]$NoInstall,
    [switch]$Verify
)

$ErrorActionPreference = 'Stop'
$template = $PSScriptRoot

if ($Name -notmatch '^[A-Z][A-Za-z0-9]*$') { throw "Name має бути в PascalCase латиницею (напр. MyPet), отримано: $Name" }
$kebab = ([regex]::Replace($Name, '(?<=[a-z0-9])([A-Z])', '-$1')).ToLowerInvariant()
if (-not $Path) { $Path = Join-Path (Split-Path $template -Parent) $kebab }
$Path = [IO.Path]::GetFullPath($Path)
if ((Test-Path $Path) -and (Get-ChildItem $Path -Force | Select-Object -First 1)) { throw "Тека $Path уже існує й не порожня" }

# ---------- фічі ----------
$manifest = Get-Content (Join-Path $template 'template.json') -Raw -Encoding UTF8 | ConvertFrom-Json
$all = @($manifest.features.PSObject.Properties.Name)
if ($null -eq $Features) {
    Write-Host "Фічі шаблону:" -ForegroundColor Cyan
    $Features = @()
    foreach ($f in $all) {
        $answer = Read-Host ("  {0} — {1}. Увімкнути? [Y/n]" -f $f, $manifest.features.$f.title)
        if ($answer -notmatch '^(n|no|н|ні)$') { $Features += $f }
    }
}
$Features = @($Features | ForEach-Object { $_ -split ',' } | ForEach-Object { $_.Trim().ToLowerInvariant() } | Where-Object { $_ })
if ($Features -contains 'all') { $Features = $all }
if ($Features -contains 'none') { $Features = @() }
$unknown = @($Features | Where-Object { $all -notcontains $_ })
if ($unknown.Count) { throw "Невідомі фічі: $($unknown -join ', '). Є: $($all -join ', ')" }
$disabled = @($all | Where-Object { $Features -notcontains $_ })

Write-Host "Створюю $Name у $Path" -ForegroundColor Cyan
Write-Host ("  увімкнено: {0}" -f ($(if ($Features.Count) { $Features -join ', ' } else { '—' })))

# ---------- копіювання ----------
New-Item -ItemType Directory -Force $Path | Out-Null
$xd = @('.git', 'node_modules', 'bin', 'obj', 'dist', 'data', 'wwwroot', '.vs', '.idea', 'publish') | ForEach-Object { Join-Path $template $_ }
$xd += @('node_modules', 'bin', 'obj', 'dist', 'data', 'wwwroot', '.vs')
robocopy $template $Path /E /NFL /NDL /NJH /NJS /NP /XD $xd /XF 'new-app.ps1' 'template.json' 'TEMPLATE.md' | Out-Null
if ($LASTEXITCODE -ge 8) { throw "robocopy завершився з кодом $LASTEXITCODE" }

# ---------- невибрані фічі: теки ----------
foreach ($f in $disabled) {
    foreach ($p in $manifest.features.$f.paths) {
        $target = Join-Path $Path $p
        if (Test-Path $target) { Remove-Item $target -Recurse -Force }
    }
}

# Порожні теки, що лишились після видалення (docs/, .claude/skills/ …).
Get-ChildItem $Path -Recurse -Directory -Force | Sort-Object { $_.FullName.Length } -Descending |
    Where-Object { -not (Get-ChildItem $_.FullName -Force | Select-Object -First 1) } | ForEach-Object { Remove-Item $_.FullName -Force }

# ---------- маркери і перейменування у файлах ----------
$utf8 = New-Object System.Text.UTF8Encoding($false)
$textExt = @('.cs', '.csproj', '.slnx', '.sln', '.json', '.ts', '.vue', '.html', '.css', '.md', '.ps1', '.txt', '.svg', '.yml', '.yaml')
$marker = '^\s*(//|<!--|#)\s*#(feature:(?<id>[\w-]+)|end)\b'

Get-ChildItem $Path -Recurse -File -Force | Where-Object { $textExt -contains $_.Extension -or $_.Name -eq '.gitignore' } | ForEach-Object {
    $file = $_.FullName
    $text = [IO.File]::ReadAllText($file, $utf8)
    $newline = if ($text -match "`r`n") { "`r`n" } else { "`n" }
    $out = New-Object System.Collections.Generic.List[string]
    $skip = $false
    foreach ($line in ($text -split "\r?\n")) {
        $m = [regex]::Match($line, $marker)
        if ($m.Success) {
            if ($m.Groups['id'].Success) { $skip = $disabled -contains $m.Groups['id'].Value } else { $skip = $false }
            continue
        }
        if (-not $skip) { $out.Add($line) }
    }
    $result = ($out -join $newline).Replace('TTemplate', $Name).Replace('t-template', $kebab)
    if ($result -ne $text) { [IO.File]::WriteAllText($file, $result, $utf8) }
}

# Теки й файли з TTemplate в імені — від найглибших.
Get-ChildItem $Path -Recurse -Force | Where-Object { $_.Name -like '*TTemplate*' } |
    Sort-Object { $_.FullName.Length } -Descending |
    ForEach-Object {
        # Свіжоскопійовані файли інколи на мить тримає антивірус/індексатор — кілька спроб.
        for ($i = 1; ; $i++) {
            try { Rename-Item $_.FullName ($_.Name.Replace('TTemplate', $Name)) -ErrorAction Stop; break }
            catch { if ($i -ge 20) { throw }; Start-Sleep -Milliseconds 500 }
        }
    }

# ---------- git, npm ----------
Push-Location $Path
try {
    git init -q -b main 2>$null
    if ($LASTEXITCODE -ne 0) { git init -q }
    $ui = Join-Path $Path "src/$Name.UI"
    if (-not $NoInstall) {
        Write-Host "npm install…" -ForegroundColor Cyan
        npm install --prefix $ui --no-audit --no-fund | Out-Host
        if ($LASTEXITCODE -ne 0) { throw 'npm install не вдався' }
    }
    if ($Verify) {
        Write-Host "Перевірка: dotnet build…" -ForegroundColor Cyan
        dotnet build "src/$Name.Api" -nologo -v q | Out-Host
        if ($LASTEXITCODE -ne 0) { throw 'dotnet build не вдався' }
        Write-Host "Перевірка: npm run build…" -ForegroundColor Cyan
        npm run build --prefix $ui | Out-Host
        if ($LASTEXITCODE -ne 0) { throw 'npm run build не вдався' }
    }
} finally {
    Pop-Location
}

Write-Host ""
Write-Host "Готово: $Path" -ForegroundColor Green
Write-Host "Далі — README.md у новому проєкті (запуск: бекенд + npm run dev)."
