<#
.SYNOPSIS
    Crée un nouveau jeu à partir du squelette HeroesOfIdlestan.

.DESCRIPTION
    Copie le dépôt (sans bin/, obj/, .vs/, .git/, ni le cache .godot/) dans un nouveau dossier en remplaçant
    « HeroesOfIdlestan » par le nom du jeu dans les noms de fichiers, de dossiers et dans le contenu
    texte, et « heroesofidlestan » par sa version minuscule (identifiant Android, préfixe localStorage).
    Les fixtures de sauvegarde sont régénérées (la clé de brouillage change avec le nom), puis la
    solution est compilée et testée.

    Voir design/01-demarrer-un-jeu.md pour la suite.

.EXAMPLE
    .\tools\New-Game.ps1 -Name MyGame -Destination C:\DEV
    # => C:\DEV\MyGame\MyGame.slnx
#>
param(
    [Parameter(Mandatory)] [ValidatePattern('^[A-Z][A-Za-z0-9]*$')] [string] $Name,
    [Parameter(Mandatory)] [string] $Destination,
    [switch] $SkipTests
)

$ErrorActionPreference = 'Stop'

$source = Split-Path -Parent $PSScriptRoot
$target = Join-Path $Destination $Name
if (Test-Path $target) { throw "Le dossier $target existe déjà." }

$lower = $Name.ToLowerInvariant()
$excluded = @('bin', 'obj', '.vs', '.git', 'TestResults', '.godot')
$textExtensions = @('.cs', '.csproj', '.slnx', '.props', '.json', '.md', '.yml', '.yaml', '.js', '.html',
                    '.xml', '.py', '.ps1', '.bat', '.txt', '.gitignore', '.godot')

function Rename-Text([string] $text) {
    return $text.Replace('HeroesOfIdlestan', $Name).Replace('heroesofidlestan', $lower)
}

Write-Host "Copie de $source vers $target"
Get-ChildItem -Path $source -Recurse -Force -File | Where-Object {
    $relative = $_.FullName.Substring($source.Length + 1)
    -not ($relative.Split([IO.Path]::DirectorySeparatorChar) | Where-Object { $excluded -contains $_ })
} | ForEach-Object {
    $relative = $_.FullName.Substring($source.Length + 1)
    $destPath = Join-Path $target (Rename-Text $relative)
    New-Item -ItemType Directory -Force -Path (Split-Path -Parent $destPath) | Out-Null

    $isText = $textExtensions -contains $_.Extension -or $_.Name -eq '.gitignore'
    if ($isText) {
        # UTF-8 sans BOM comme les sources, sauf les .ps1 : Windows PowerShell 5.1 lirait un script
        # sans BOM en ANSI et en abîmerait les accents.
        $content = [IO.File]::ReadAllText($_.FullName, [Text.Encoding]::UTF8)
        $bom = $_.Extension -eq '.ps1'
        [IO.File]::WriteAllText($destPath, (Rename-Text $content), (New-Object Text.UTF8Encoding $bom))
    } else {
        Copy-Item -LiteralPath $_.FullName -Destination $destPath
    }
}

# Les fixtures sont brouillées avec une clé qui contient le nom du jeu : on les reconstruit.
Remove-Item -Recurse -Force (Join-Path $target "$Name.Test.Tests\Saves\current") -ErrorAction SilentlyContinue

Push-Location $target
try {
    dotnet build "$Name.slnx"
    if ($LASTEXITCODE -ne 0) { throw 'Le build a échoué.' }

    $env:GAME_MANUAL_TESTS = '1'
    dotnet test "$Name.Test.Tests" --no-build --filter "FullyQualifiedName~Rebuild_Current_Saves"
    Remove-Item Env:GAME_MANUAL_TESTS
    if ($LASTEXITCODE -ne 0) { throw 'La régénération des fixtures a échoué.' }

    if (-not $SkipTests) {
        dotnet test "$Name.Test.Tests" --no-build
        if ($LASTEXITCODE -ne 0) { throw 'Les tests ont échoué.' }
    }
}
finally {
    Pop-Location
}

Write-Host ""
Write-Host "[OK] $target\$Name.slnx est prêt. Suite : design/01-demarrer-un-jeu.md"
