<#
.SYNOPSIS
    Mostra a árvore de arquivos .cs com a contagem de linhas úteis de cada um.

.DESCRIPTION
    "Linha útil" exclui linhas em branco, comentários de linha (// e ///) e blocos /* */.
    Diretórios bin/ e obj/ são ignorados. Por padrão analisa a raiz do repositório.

.EXAMPLE
    pwsh ./tools/Get-CsLineCount.ps1
    pwsh ./tools/Get-CsLineCount.ps1 -Root C:\outro\projeto
#>
param([string]$Root = (Split-Path $PSScriptRoot -Parent))

[Console]::OutputEncoding = [Text.Encoding]::UTF8
$Root = (Resolve-Path -LiteralPath $Root).Path.TrimEnd('\')

function Get-UsefulLines {
    param([string]$Path)
    $inBlock = $false; $n = 0
    foreach ($line in [IO.File]::ReadAllLines($Path)) {
        $t = $line.Trim()
        if ($inBlock) { if ($t.Contains('*/')) { $inBlock = $false }; continue }
        if ($t -eq '' -or $t.StartsWith('//')) { continue }
        if ($t.StartsWith('/*')) { if (-not $t.Contains('*/')) { $inBlock = $true }; continue }
        $n++
    }
    $n
}

$rows = Get-ChildItem -LiteralPath $Root -Recurse -Filter *.cs -File |
    Where-Object { $_.FullName -notmatch '\\(bin|obj)\\' } |
    ForEach-Object {
        $rel = $_.FullName.Substring($Root.Length).TrimStart('\')
        [pscustomobject]@{
            Dir   = if (Split-Path $rel -Parent) { Split-Path $rel -Parent } else { '.' }
            Name  = $_.Name
            Uteis = Get-UsefulLines $_.FullName
            Total = [IO.File]::ReadAllLines($_.FullName).Count
        }
    } | Sort-Object Dir, Name

if (-not $rows) { Write-Warning "Nenhum arquivo .cs encontrado em $Root"; return }

foreach ($g in ($rows | Group-Object Dir | Sort-Object Name)) {
    Write-Host ''
    Write-Host ($g.Name + '\') -ForegroundColor Cyan
    $i = 0
    foreach ($r in $g.Group) {
        $i++
        $branch = if ($i -eq $g.Group.Count) { '└── ' } else { '├── ' }
        '{0}{1,-38} {2,5} úteis  {3,5} brutas' -f $branch, $r.Name, $r.Uteis, $r.Total
    }
}

$top = $rows | Sort-Object Uteis -Descending | Select-Object -First 1
Write-Host ''
Write-Host ('TOTAL: {0} arquivos | {1} linhas úteis | {2} linhas brutas' -f `
    $rows.Count, ($rows | Measure-Object Uteis -Sum).Sum, ($rows | Measure-Object Total -Sum).Sum) -ForegroundColor Green
Write-Host ('Maior: {0} ({1} úteis)' -f $top.Name, $top.Uteis) -ForegroundColor Yellow
