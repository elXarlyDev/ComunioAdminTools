# ComunioAdminTools

La solución contiene cuatro proyectos activos:

- `ComunioAdminTools.Core`: modelos, autenticación, sesión y servicios de Comunio.
- `ComunioAdminTools.UI`: interfaz Razor compartida, resultados y fuentes.
- `ComunioAdminTools.Web`: cliente Blazor WebAssembly con estructura de PWA.
- `ComunioAdminTools.Mobile`: aplicación MAUI Blazor.

## Revisión de la migración

Las cinco operaciones del proyecto original están conectadas a la interfaz común:
abonar dinero por puntos, revertirlo, abonar 5 millones por el parón, consultar
puntos y consultar plantillas de más de 25 jugadores. Se mantienen importes,
límites de 100 puntos por petición, motivos y ordenación de resultados.

Core sustituye los helpers HTTP y los modelos de login originales por la
autenticación compartida. Conserva una sesión por instancia de cliente, renovación
de tokens y caché de perfil. Los pagos se realizan sin reintentos automáticos y
los fallos parciales muestran los importes confirmados.

Mobile conserva la clave y el alias de firma, identificador Android, versiones,
icono, splash, colores y fuentes originales. La categoría MacCatalyst también se
conserva. El botón Atrás cierra el diálogo de resultados; abandonar la pantalla
también lo cierra. Los estilos XAML y controles del original se sustituyen por
componentes y estilos Razor. Android mantiene el mínimo API 24 de Mobile.

La comprobación local de administrador/líder permanece desactivada en Core,
como estaba en los proyectos activos al revisar la migración; la API valida los
permisos. Web conserva los tokens en memoria y requiere login al recargar. Mobile
los guarda mediante SecureStorage. No hay backend de sesiones multiusuario.

El proyecto original y sus carpetas de código y compilación se han retirado de
la raíz. La solución ya no lo referencia.

## Ejecutar Web

Desde la raíz:

```powershell
dotnet run --project ComunioAdminTools.Web --launch-profile http
```

Abre http://localhost:5198. Las operaciones de dinero realizan movimientos reales;
para una prueba funcional inicial utiliza las consultas.

## Publicar Web en GitHub Pages

El flujo `.github/workflows/deploy-web.yml`, ubicado en la raíz del repositorio,
publica únicamente `ComunioAdminTools.Web` y sus referencias a Core y UI. No
compila la solución completa ni requiere instalar las cargas de trabajo MAUI.

1. Sube al repositorio la raíz de esta solución, incluidas las cuatro carpetas de
   proyectos y `.github/workflows/deploy-web.yml`.
2. En GitHub abre **Settings → Pages → Build and deployment → Source** y
   selecciona **GitHub Actions**.
3. Haz push a la rama predeterminada del repositorio. También puedes ejecutar
   **Actions → Publish Web to GitHub Pages → Run workflow** sobre esa rama.
4. Consulta la URL publicada en el entorno `github-pages` o en **Settings → Pages**.

El flujo detecta la rama predeterminada, sin asumir `main`, `master` o `develop`.
GitHub proporciona la ruta base: normalmente la URL será
`https://<usuario>.github.io/<repositorio>/`. También admite sitios en la raíz y
dominios personalizados configurados en Pages. No requiere un token personal:
usa los permisos de Pages del `GITHUB_TOKEN` del workflow.

La preparación ajusta el `<base>` del HTML publicado, genera `404.html` para
las rutas de Blazor y `.nojekyll`, y recalcula la integridad y versión de la caché
PWA. El service worker obtiene su ruta de su ámbito de registro y aísla las
cachés por aplicación. Los archivos fuente siguen funcionando en localhost.
Pages sirve `404.html` con estado HTTP 404 en accesos directos a rutas que no
existen como archivo, aunque Blazor puede mostrar la página correspondiente.

Para verificar el mismo artefacto localmente desde la raíz:

```powershell
dotnet publish ComunioAdminTools.Web/ComunioAdminTools.Web.csproj -c Release -o artifacts/web
.\ComunioAdminTools.Web\Deployment\Prepare-GitHubPages.ps1 -PublishDirectory artifacts/web/wwwroot -BasePath /ComunioAdminTools/
```

El contenido que se publica es `artifacts/web/wwwroot`, no el código de la
solución. Comprueba en la URL final el inicio de sesión, la carga de fuentes y
la instalación de la PWA. Las llamadas a Comunio se ejecutan desde el navegador
y dependen de que su API permita CORS para el origen publicado; Pages no
proporciona un proxy backend. La sesión continúa aislada por cliente y los
tokens permanecen en memoria.

Al añadir la PWA a la pantalla de inicio, su nombre es `Comunio Admin Tools`.
Los iconos Web de 192 y 512 píxeles y `apple-touch-icon.png` de 180 píxeles
proceden del mismo `ComunioAdminTools.Mobile/Resources/AppIcon/appicon.png`
que usa la APK Android. Si cambia ese recurso, actualiza también los tres PNG
de `ComunioAdminTools.Web/wwwroot`. En iPhone, abre la web desplegada en Safari
y utiliza **Compartir → Añadir a pantalla de inicio**. Si tenías un acceso con
el icono o nombre anterior, elimínalo y vuelve a añadirlo tras el despliegue.

Referencias: [workflows oficiales de Pages](https://docs.github.com/en/pages/getting-started-with-github-pages/using-custom-workflows-with-github-pages)
y [despliegue de Blazor WebAssembly](https://learn.microsoft.com/en-us/aspnet/core/blazor/host-and-deploy/webassembly?view=aspnetcore-10.0).

## Idiomas

La interfaz incluye un selector de español y valenciano. Los textos de Web,
Mobile y los errores de Core comparten los recursos de
`ComunioAdminTools.Core/Localization`. Consulta su README para añadir traducciones
y ejecutar las comprobaciones de idiomas.

## Generar la APK

```powershell
.\ComunioAdminTools.Mobile\Platforms\Android\CrearAndroidApk.ps1
```

El script pide la contraseña, publica Mobile y firma con el keystore existente.
La salida está en `ComunioAdminTools.Mobile/bin/Release/net10.0-android/publish`.
Incrementa `ApplicationVersion` antes de distribuir una actualización.
Consulta `ComunioAdminTools.Mobile/Platforms/Android/signing/README.txt`.

## Verificaciones

```powershell
dotnet build ComunioAdminTools.Web/ComunioAdminTools.Web.csproj
dotnet build ComunioAdminTools.Mobile/ComunioAdminTools.Mobile.csproj -f net10.0-android -c Release
dotnet build ComunioAdminTools.Mobile/ComunioAdminTools.Mobile.csproj -f net10.0-windows10.0.19041.0
dotnet run --project Tests/MigrationChecks/MigrationChecks.csproj
dotnet run --project Tests/LocalizationChecks/LocalizationChecks.csproj
```

Las comprobaciones de migración usan una API simulada. Cubren las cinco operaciones,
fallos parciales sin reintentos, caché del perfil, logout, renovación concurrente,
aislamiento de clientes y cierre de resultados. La instalación y firma final de la
APK, el uso real de Comunio, iOS/MacCatalyst y el despliegue PWA requieren su
verificación en los entornos correspondientes.
