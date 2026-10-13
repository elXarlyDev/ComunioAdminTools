# Desde la raiz del repositorio: .\ComunioAdminTools.Mobile\Platforms\Android\CrearAndroidApk.ps1
$ErrorActionPreference = 'Stop'
$previousPassword = [Environment]::GetEnvironmentVariable('COMUNIO_APK_PASSWORD', 'Process')
$securePassword = $null
$locationChanged = $false
$exitCode = 0

try {
    $securePassword = Read-Host 'Introduce la contrasena de firma' -AsSecureString
    if ($securePassword.Length -eq 0) {
        throw 'La contrasena no puede estar vacia.'
    }

    # Ambos programas leen la misma contrasena del entorno del proceso.
    $env:COMUNIO_APK_PASSWORD = [System.Net.NetworkCredential]::new('', $securePassword).Password
    $projectRoot = [System.IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..\..'))
    Push-Location -LiteralPath $projectRoot
    $locationChanged = $true

    [xml]$projectXml = Get-Content -LiteralPath (Join-Path $projectRoot 'ComunioAdminTools.Mobile.csproj') -Raw
    $versionNode = $projectXml.SelectSingleNode('/Project/PropertyGroup/ApplicationVersion')
    if ($null -eq $versionNode -or $versionNode.InnerText.Trim() -notmatch '^[0-9]+$') {
        throw 'ApplicationVersion no existe o no contiene una version numerica valida en ComunioAdminTools.Mobile.csproj.'
    }
    $applicationVersion = $versionNode.InnerText.Trim()

    $apkSigner = 'C:\Program Files (x86)\Android\android-sdk\build-tools\36.0.0\lib\apksigner.jar'
    $inputApk = '.\bin\Release\net10.0-android\publish\comunio_admin_tools.comunio_admin_tools.apk'
    $outputApk = ".\bin\Release\net10.0-android\publish\ComunioAdminTools-v$applicationVersion.apk"

    $null = Get-Command dotnet -ErrorAction Stop
    $null = Get-Command java -ErrorAction Stop
    foreach ($requiredFile in @($apkSigner, '.\Platforms\Android\signing\comunio.keystore')) {
        if (-not (Test-Path -LiteralPath $requiredFile -PathType Leaf)) {
            throw "No se encuentra el archivo necesario: $requiredFile"
        }
    }

    # Regenerar los recursos evita referencias obsoletas al cambiar el splash.
    Write-Host 'Limpiando recursos de Android Release...'
    dotnet clean ComunioAdminTools.Mobile.csproj -f net10.0-android -c Release
    if ($LASTEXITCODE -ne 0) {
        throw "La limpieza de Android Release ha fallado (codigo $LASTEXITCODE)."
    }

    Write-Host 'Publicando APK...'
    dotnet publish ComunioAdminTools.Mobile.csproj -f net10.0-android -c Release `
        -p:AndroidPackageFormat=apk `
        -p:AndroidKeyStore=true `
        -p:AndroidSigningKeyStore="Platforms\Android\signing\comunio.keystore" `
        -p:AndroidSigningKeyAlias="comunioalias" `
        -p:AndroidSigningStorePass=env:COMUNIO_APK_PASSWORD `
        -p:AndroidSigningKeyPass=env:COMUNIO_APK_PASSWORD

    if ($LASTEXITCODE -ne 0) {
        throw "dotnet publish ha fallado (codigo $LASTEXITCODE)."
    }
    if (-not (Test-Path -LiteralPath $inputApk -PathType Leaf)) {
        throw "No se encuentra la APK publicada: $inputApk"
    }

    Write-Host 'Firmando APK...'
    java -jar $apkSigner sign `
        --ks '.\Platforms\Android\signing\comunio.keystore' `
        --ks-key-alias comunioalias `
        --ks-pass env:COMUNIO_APK_PASSWORD `
        --key-pass env:COMUNIO_APK_PASSWORD `
        --out $outputApk `
        $inputApk

    if ($LASTEXITCODE -ne 0) {
        throw "La firma de la APK ha fallado (codigo $LASTEXITCODE)."
    }
    if (-not (Test-Path -LiteralPath $outputApk -PathType Leaf)) {
        throw "No se encuentra la APK final: $outputApk"
    }
    Write-Host "APK creada correctamente: $(Join-Path $projectRoot $outputApk)" -ForegroundColor Green
}
catch {
    $exitCode = 1
    Write-Host "Error: $($_.Exception.Message)" -ForegroundColor Red
    Read-Host 'Pulsa Enter para cerrar' | Out-Null
}
finally {
    [Environment]::SetEnvironmentVariable('COMUNIO_APK_PASSWORD', $previousPassword, 'Process')
    if ($null -ne $securePassword) {
        $securePassword.Dispose()
    }
    if ($locationChanged) {
        Pop-Location
    }
}

exit $exitCode
