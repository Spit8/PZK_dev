# Obtenir le dossier où se trouve le script
$scriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path

# Fichier de sortie dans le même dossier
$outputFile = Join-Path $scriptDir "liste_fichiers_glb.txt"

# Rechercher tous les fichiers .glb récursivement et extraire le chemin relatif
$fichiers = Get-ChildItem -Path $scriptDir -Filter "*.glb" -Recurse -File

$cheminsRelatifs = $fichiers | ForEach-Object {
    $_.FullName.Substring($scriptDir.Length + 1)
}

# Écrire les résultats dans le fichier texte
$cheminsRelatifs | Out-File -FilePath $outputFile -Encoding UTF8

Write-Host "Terminé ! $($fichiers.Count) fichier(s) .glb trouvé(s)."
Write-Host "Résultat enregistré dans : $outputFile"