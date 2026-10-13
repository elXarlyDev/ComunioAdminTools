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
```

Las comprobaciones de migración usan una API simulada. Cubren las cinco operaciones,
fallos parciales sin reintentos, caché del perfil, logout, renovación concurrente,
aislamiento de clientes y cierre de resultados. La instalación y firma final de la
APK, el uso real de Comunio, iOS/MacCatalyst y el despliegue PWA requieren su
verificación en los entornos correspondientes.
