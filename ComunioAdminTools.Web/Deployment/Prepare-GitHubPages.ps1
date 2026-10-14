param(
    [Parameter(Mandatory = $true)]
    [string] $PublishDirectory,
    [AllowEmptyString()]
    [string] $BasePath = ''
)

$ErrorActionPreference = 'Stop'
$siteDirectory = (Resolve-Path -LiteralPath $PublishDirectory).Path
$normalizedBasePath = '/' + $BasePath.Trim('/')
if ($normalizedBasePath -ne '/') { $normalizedBasePath += '/' }
if ($normalizedBasePath -match '[<>"?#\s\\]') {
    throw 'BasePath must be a URL path such as /ComunioAdminTools/.'
}

$utf8 = New-Object System.Text.UTF8Encoding($false)
$indexPath = Join-Path $siteDirectory 'index.html'
$index = [System.IO.File]::ReadAllText($indexPath)
if ($index -notmatch '<base href="[^"]*"\s*/>') {
    throw 'The published index.html does not contain the expected base element.'
}
$index = [regex]::Replace($index, '<base href="[^"]*"\s*/>', "<base href=`"$normalizedBasePath`" />")
[System.IO.File]::WriteAllText($indexPath, $index, $utf8)

# Pages serves this document for direct requests to client-side routes.
[System.IO.File]::WriteAllText((Join-Path $siteDirectory '404.html'), $index, $utf8)
[System.IO.File]::WriteAllText((Join-Path $siteDirectory '.nojekyll'), '', $utf8)

# Changing index.html after publish requires updating its PWA integrity hash.
$manifestPath = Join-Path $siteDirectory 'service-worker-assets.js'
$manifestText = [System.IO.File]::ReadAllText($manifestPath)
$match = [regex]::Match($manifestText, '(?s)self\.assetsManifest\s*=\s*(\{.*\})\s*;?\s*$')
if (-not $match.Success) { throw 'Unexpected service-worker-assets.js format.' }
$manifest = $match.Groups[1].Value | ConvertFrom-Json
$indexAssets = @($manifest.assets | Where-Object { $_.url -eq 'index.html' })
if ($indexAssets.Count -ne 1) { throw 'Expected one index.html entry in the PWA manifest.' }
$sha256 = [System.Security.Cryptography.SHA256]::Create()
try {
    $indexAssets[0].hash = 'sha256-' + [Convert]::ToBase64String($sha256.ComputeHash([System.IO.File]::ReadAllBytes($indexPath)))
    $assetsJson = ConvertTo-Json -InputObject @($manifest.assets) -Depth 10 -Compress
    $manifest.version = [Convert]::ToBase64String($sha256.ComputeHash($utf8.GetBytes($assetsJson)))
} finally {
    $sha256.Dispose()
}
$json = ConvertTo-Json -InputObject $manifest -Depth 10
[System.IO.File]::WriteAllText($manifestPath, "self.assetsManifest = $json;`n", $utf8)
Write-Host "Prepared GitHub Pages site at $siteDirectory with base path $normalizedBasePath"
