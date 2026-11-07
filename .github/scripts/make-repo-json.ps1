param(
    [Parameter(Mandatory)] [string] $ManifestPath,
    [Parameter(Mandatory)] [string] $Repository, # owner/name
    [Parameter(Mandatory)] [string] $Tag,
    [Parameter(Mandatory)] [string] $OutPath,
    [string] $Branch = 'master'
)

# Builds the custom Dalamud plugin repository file (repo.json) from the manifest DalamudPackager generated.
# AssemblyVersion is kept from the manifest so it matches the one inside latest.zip.
$manifest = Get-Content -Raw $ManifestPath | ConvertFrom-Json

$download = "https://github.com/$Repository/releases/download/$Tag/latest.zip"
$extra = [ordered]@{
    IsHide              = $false
    IsTestingExclusive  = $false
    DownloadLinkInstall = $download
    DownloadLinkUpdate  = $download
    DownloadLinkTesting = $download
    IconUrl             = "https://raw.githubusercontent.com/$Repository/$Branch/PearHonorific/images/icon.png"
    LastUpdate          = [DateTimeOffset]::UtcNow.ToUnixTimeSeconds()
}
foreach ($key in $extra.Keys)
{
    $manifest | Add-Member -NotePropertyName $key -NotePropertyValue $extra[$key] -Force
}

# Dalamud expects a JSON array of plugins.
ConvertTo-Json -InputObject @($manifest) -Depth 5 | Set-Content -Encoding utf8 $OutPath
