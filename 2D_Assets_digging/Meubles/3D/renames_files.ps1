Get-ChildItem -File -Recurse | Where-Object { $_.Name -like "*_00001_*" } | ForEach-Object {
    $newName = $_.Name -replace "_00001_", ""
    Rename-Item $_.FullName $newName
}