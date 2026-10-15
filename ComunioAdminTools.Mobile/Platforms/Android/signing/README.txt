GENERACION DE LA APK DE MOBILE

Desde la raiz del repositorio:
  .\ComunioAdminTools.Mobile\Platforms\Android\CrearAndroidApk.ps1

Desde la carpeta ComunioAdminTools.Mobile:
  .\Platforms\Android\CrearAndroidApk.ps1

El script solicita la contrasena de firma sin mostrarla, limpia Android Release,
publica Mobile y firma con comunio.keystore y el alias comunioalias.
La variable temporal COMUNIO_APK_PASSWORD se restaura al finalizar.

Salida:
  ComunioAdminTools.Mobile\bin\Release\net10.0-android\publish\ComunioAdminTools-v<ApplicationVersion>.apk

La version se obtiene de ComunioAdminTools.Mobile.csproj. Incrementa
ApplicationVersion para distribuir una actualizacion y ajusta ApplicationDisplayVersion.
Se conserva el identificador Android de la aplicacion anterior y su keystore.

Requisitos:
- SDK .NET 10 y workload MAUI Android.
- Java JDK disponible como java en PATH.
- Android SDK; el script usa build-tools\36.0.0\lib\apksigner.jar
  en C:\Program Files (x86)\Android\android-sdk.
- La clave existente en Platforms\Android\signing\comunio.keystore (relativo a Mobile).

No regeneres el keystore para actualizar una instalacion existente.
No subas la clave ni signing.props al repositorio.

FIRMA SIN EL SCRIPT (OPCIONAL)

Copia signing.props.template a signing.props en esta carpeta y configura:
  $env:COMUNIO_KEYSTORE_PASSWORD = 'tu_contrasena_keystore'
  $env:COMUNIO_KEY_ALIAS_PASSWORD = 'tu_contrasena_alias'

Desde la raiz del repositorio:
  dotnet publish ComunioAdminTools.Mobile/ComunioAdminTools.Mobile.csproj -f net10.0-android -c Release

El proyecto importa signing.props si existe. El script no necesita este archivo.