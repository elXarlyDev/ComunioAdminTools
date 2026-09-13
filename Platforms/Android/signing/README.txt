GENERAACIÓN DE UN KEYSTORE para firmar el APK:

1) Ejecuta este comando (Java JDK requerido):
   keytool -genkeypair -v -keystore comunio.keystore -alias comunioalias -keyalg RSA -keysize 2048 -validity 10000

   Responde las preguntas y establece una contraseña segura.

2) Mueve el archivo generado a: Platforms/Android/signing/comunio.keystore (ya usado por el proyecto)

3) Exporta las contraseñas a variables de entorno antes de compilar (por seguridad):
   Windows (PowerShell):
	 $env:COMUNIO_KEYSTORE_PASSWORD = "tu_contrasena_keystore"
n     $env:COMUNIO_KEY_ALIAS_PASSWORD = "tu_contrasena_alias"

   Linux/macOS (bash):
	 export COMUNIO_KEYSTORE_PASSWORD=tu_contrasena_keystore
	 export COMUNIO_KEY_ALIAS_PASSWORD=tu_contrasena_alias

4) Compila en Release para Android desde Visual Studio seleccionando la configuración Release y plataforma Android.
   También puedes usar dotnet:
	 dotnet build -c Release -f net10.0-android

Notas:
- Si no colocas el keystore en la ruta indicada, el proyecto generará un APK no firmado (útil para pruebas locales) pero no podrá publicarse en Google Play.
- Nunca subas el keystore ni las contraseñas al repositorio público.
- Para integrar en CI, añade las variables de entorno a la configuración segura del pipeline y copia el keystore al agente antes de compilar.

______________________________
------------------------------


GENERACIÓN DE LA APK:

Requisitos para ejecutar el comando de publicación:

- Tener Java JDK (p. ej. 26.0.2.1) instalado y agregado al PATH:
	$env:JAVA_HOME = "C:\Program Files\Java\jdk-26.0.2.1"
	>> $env:Path = "$env:JAVA_HOME\bin;$env:Path"

1) Ejecutar este comando en el directorio raiz del proyecto:

dotnet publish -f net10.0-android -c Release `
>>     -p:AndroidPackageFormat=apk `
>>     -p:AndroidKeyStore=true `
>>     -p:AndroidSigningKeyStore="Platforms\Android\signing\comunio.keystore" `
>>     -p:AndroidSigningKeyAlias="comunioalias" `
>>     -p:AndroidSigningStorePass="(LA CONTRASEÑA)" `
>>     -p:AndroidSigningKeyPass="(LA CONTRASEÑA)"

2) El APK generado estará en la ruta: .\bin\Release\net10.0-android\publish. A partir de ese APK crear una firma correcta usando este comando

java -jar "C:\Program Files (x86)\Android\android-sdk\build-tools\36.0.0\lib\apksigner.jar" sign `
>>     --ks ".\Platforms\Android\signing\comunio.keystore" `
>>     --ks-key-alias comunioalias `
>>     --ks-pass env:KS_PASS `
>>     --key-pass env:KEY_PASS `
>>     --out ".\bin\Release\net10.0-android\publish\ComunioAdminTools-FINAL.apk" `
>>     ".\bin\Release\net10.0-android\publish\comunio_admin_tools.comunio_admin_tools.apk"