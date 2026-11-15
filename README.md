# ComunioAdminTools

Las prácticas compartidas de desarrollo se mantienen en [AGENTS.md](AGENTS.md),
la gobernanza para todo el repositorio. Consulta ese archivo antes de realizar
cambios y registra allí las nuevas convenciones acordadas.

La solución contiene cuatro proyectos activos:

- `ComunioAdminTools.Core`: modelos, autenticación, sesión y servicios de Comunio.
- `ComunioAdminTools.UI`: interfaz Razor compartida, resultados y fuentes.
- `ComunioAdminTools.Web`: cliente Blazor WebAssembly con estructura de PWA.
- `ComunioAdminTools.Mobile`: aplicación MAUI Blazor.

## Revisión de la migración

Las cinco operaciones del proyecto original están conectadas a la interfaz común:
abonar dinero por puntos, revertirlo, abonar 5 millones por el parón, consultar
puntos y consultar el tamaño de todas las plantillas, resaltando las que superan
25 jugadores. Se mantienen importes,
límites de 100 puntos por petición, motivos y ordenación de resultados.

Core sustituye los helpers HTTP y los modelos de login originales por la
autenticación compartida. Conserva una sesión por instancia de cliente, renovación
de tokens y caché de perfil. Los pagos se realizan sin reintentos automáticos y
los fallos parciales muestran los importes confirmados.

Mobile conserva la clave y el alias de firma, identificador Android, versiones,
icono, splash, colores y fuentes originales. La categoría MacCatalyst también se
conserva. Los resultados se muestran dentro de cada pestaña y se retiran con
Ocultar; no hay diálogos de resultados que intercepten el botón Atrás.
Los estilos XAML y controles del original se sustituyen por
componentes y estilos Razor. Android mantiene el mínimo API 24 de Mobile.

La comprobación local de administrador/líder permanece desactivada en Core,
como estaba en los proyectos activos al revisar la migración; la API valida los
permisos. Web y Mobile conservan tokens y perfil únicamente en memoria mediante
SessionManager. Recargar Web o reiniciar el proceso de Mobile requiere login.
Mobile elimina al arrancar el access token que guardaban versiones anteriores
en SecureStorage; ya no persiste credenciales. No hay backend de sesiones multiusuario.

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

## Iconos de Font Awesome

UI incluye Font Awesome Free 7.3.1 en `wwwroot/lib/fontawesome`, con su licencia.
Web y Mobile cargan los mismos estilos y fuentes locales, sin depender de un CDN.
Se incluyen los estilos gratuitos Solid, Regular y Brands. En cualquier componente
Razor de UI puedes utilizar las clases directamente:

```razor
<button type="button">
    <i class="fa-solid fa-floppy-disk" aria-hidden="true"></i>
    Guardar
</button>
<i class="fa-regular fa-circle-check" aria-hidden="true"></i>
<i class="fa-brands fa-github" aria-hidden="true"></i>
```

Los iconos decorativos llevan `aria-hidden="true"`. Si un botón solo contiene
un icono, añade un `aria-label` traducido que describa su acción.
Para actualizar la biblioteca, sustituye `css/all.min.css`, las fuentes de
`webfonts` y `LICENSE.txt` por los de la misma versión del
[paquete oficial](https://github.com/FortAwesome/Font-Awesome/releases/tag/7.3.1).

## Idiomas

La interfaz incluye un selector de español y valenciano. Los textos de Web,
Mobile y los errores de Core comparten los recursos de
`ComunioAdminTools.Core/Localization`. Consulta su README para añadir traducciones
y ejecutar las comprobaciones de idiomas.

La pantalla de login reúne usuario, contraseña, inicio de sesión y selector de
idioma. El nombre `Comunio Admin Tools` permanece igual en ambos idiomas.
Al abrir la app se muestra LoginPage (ruta /). Tras un login correcto se navega
a MainPage (ruta /main), que contiene las cinco operaciones. Cerrar sesión desde
la esquina superior derecha vuelve a LoginPage. Acceder a /main sin sesión
también redirige al login. Las rutas se resuelven desde la biblioteca UI compartida.
Un acceso rechazado conserva la pantalla de login. Las pantallas se comparten
entre Web y Mobile y el idioma elegido se conserva al cambiar de pantalla.
MainPage incluye la pestaña de dinero únicamente si el usuario de la sesión es
administrador o líder (`IsAdmin` o `IsLeader`). Las consultas están disponibles
para todos los usuarios con sesión.
Los resultados y errores de Dinero se muestran dentro de su pestaña. Los botones
Abonar jornada y Abonar parón comparten una fila; Revertir abono aparece debajo
del resultado de una jornada abonada correctamente con importes positivos, y
desaparece al revertir o ejecutar otro abono. Los fallos parciales muestran los
importes confirmados, sin ofrecer una reversión completa de la jornada.
Consultas usa los botones Últimos puntos y Clasificaciones, en una fila compacta.
Sus resultados, estados vacíos y errores aparecen en la misma pestaña, con Ocultar
alineado a la derecha. Cada pestaña conserva sus resultados de forma independiente.
La pestaña Plantilla está disponible para todos los usuarios con sesión y contiene
Exceso plantilla y Alineación. Cada botón sustituye los resultados o errores del
otro; nunca se muestran ambas vistas a la vez. Exceso plantilla conserva su consulta,
la jornada, el resaltado por número de jugadores y Ocultar dentro de esta pestaña.
Alineación es un botón verde Comunio y sin icono. Consulta la plantilla
del usuario con sesión y obtiene el detalle de cada jugador para mostrar su
nombre, club, posición (`type`), estado (`status`) e información (`statusInfo`) en
tarjetas como las del historial de fichajes, sin columnas. Las posiciones se
deserializan al enumerado `PlayerPosition` y se muestran en blanco: PT sobre ocre,
DF sobre verde, MC sobre azul y DL sobre rojo. Los estados usan el enumerado
`PlayerStatus`: ACTIVE se muestra como un check verde; INJURED como una cruz
sanitaria roja; REHABILITATION con la misma cruz naranja clara; WEAKENED con una
interrogación naranja clara; los BANNED y SUSPENDED con una tarjeta roja; AWAY y
GAME_BREAK con una persona saliendo. Los demás conservan su texto y los iconos
incluyen una etiqueta accesible con el estado de la API.
Titulares aparece a la izquierda con once huecos fijos,
ordenados por la propiedad numérica `position` de mayor a menor, consultados en
`communities/{comunidad}/users/{usuario}/lineup`. Sus estados proceden de esa
respuesta. El desplegable Formación permite 4-4-2, 3-4-3, 3-5-2, 4-3-3 y 4-5-1,
seleccionando inicialmente `tactic`. El hueco 11 es siempre para portero; los
siguientes corresponden a defensas, medios y delanteros según la formación.
Solo se permiten movimientos e intercambios si cada jugador encaja en su destino.
Al cambiar de formación se vacían los huecos que contienen un jugador incompatible,
que vuelve a suplentes; se mantienen los demás jugadores en sus huecos.
Los huecos vacíos muestran un «+» y un fondo tenue del color de su posición,
con el nombre de la posición en su etiqueta accesible y al pasar el cursor.
Junto al título Alineación, un icono azul de información sin fondo abre un modal
con la explicación de cómo formar la alineación y la leyenda de estados.
Se cierra con Cerrar o Escape y devuelve el foco al botón de información.
Suplentes aparece a la derecha y excluye por identificador a los
titulares; se ordena por posición (PT, DF, MC, DL) y después por nombre
alfabético, con los jugadores sin posición al final. Arrastrar un suplente a un
hueco lo añade a titulares; soltarlo sobre un titular los intercambia. Arrastrar
un titular al fondo del listado de suplentes lo quita; sobre un suplente los
intercambia. También se pueden intercambiar dos huecos de titulares. En pantallas
táctiles se arrastra después de mantener pulsado; con teclado se selecciona y
se confirma el destino con Intro o espacio, y Escape cancela. Las ediciones son locales
hasta pulsar Guardar al pie de la alineación, junto al icono de guardar. Envía un PUT
a la misma URL de la alineación con `userId`, `tactic`, `lineup`, `substitutes`
y `type: "default"`. Las cuatro posiciones de `substitutes` (`striker`, `midfielder`,
`defender` y `keeper`) se envían como cadenas vacías. Las claves de `lineup` van
de 1 a 11, contienen los ids como cadenas y
usan una cadena vacía para cada hueco libre. A continuación hace GET de la misma
URL y muestra la alineación devuelta por el servidor. Tras completar ambos pasos
abre un modal con «Alineación guardada» y un botón OK para cerrarlo.
Si falla el PUT, conserva la selección local y no recarga. Si falla solo el GET,
indica que el envío se completó pero la recarga falló, sin confirmar el guardado.
Guardar se bloquea durante la petición y no se reintenta automáticamente.
Los cambios sin guardar se descartan al consultar de nuevo u ocultar el resultado.
Con once titulares se permiten intercambios, conservando los once huecos.
`statusInfo` solo se muestra si no está vacío y el jugador
no está `ACTIVE`. Sus resultados y errores se pueden ocultar con Ocultar.
El botón Clasificaciones permite consultar Mes, Primera mitad, Segunda mitad o
Temporada (`period=total`). Solo Mes muestra el selector de meses.
Al abrirlo se consulta el mes actual; al cambiar el periodo o mes se actualizan
los resultados. Los meses disponibles van de agosto de la temporada al mes actual,
ambos incluidos, mostrando su año. En agosto comienza una nueva lista de temporada.
Las clasificaciones muestran los puntos totales del periodo en orden descendente.
Exceso plantilla muestra todos los usuarios y su número de jugadores: rojo suave
para más de 25 jugadores y verde suave para 25 o menos. Este sombreado solo se
aplica a esta consulta.
El recuento consulta primero `/matchdays` y toma el timestamp de la jornada
iniciada y sin finalizar (la más reciente si hay varias). Si no hay ninguna
activa, utiliza la jornada iniciada con timestamp más reciente. Solo cuenta
jugadores con `purchaseInfo.date` estrictamente anterior a ese instante,
respetando las zonas horarias; las fechas iguales, posteriores o ausentes no
cuentan. Si no hay jornadas iniciadas, muestra un error sin consultar plantillas.
La respuesta conserva el `matchdayKey` de la jornada seleccionada y el título
del resultado lo muestra como `Exceso de plantilla (Jornada {matchdayKey})`.

## Generar la APK

La pestaña Mercado permite consultar Historial de Fichajes para la comunidad y
el usuario de la sesión. La consulta solicita `limit=999999`, sin `offset`.
Cada fila muestra jugador y club con la fecha de ejecución (`datechanged`) al
lado, seguida del precio y del vendedor y comprador. La fecha conserva su zona
horaria. Los resultados y errores se muestran dentro de Mercado.
El botón usa el verde Comunio. El filtro de futbolista busca coincidencias
parciales sin distinguir mayúsculas. Comprador y Vendedor son desplegables con
los usuarios de la comunidad, Computer y los participantes del historial,
incluidos los que ya no estén en la comunidad, con la opción Todos. Los filtros se
combinan y se
aplican al historial descargado y vuelven a la primera página al cambiar.
Los resultados se paginan de 10 en 10 con botones -10, -1, +1 y +10 páginas;
los saltos se limitan a la primera y última página. Bajo el título Filtros,
Futbolista ocupa una fila y Comprador y Vendedor comparten la siguiente.
El listado aparece bajo el título Fichajes.
La dirección del fichaje es `tradingPartner` (vendedor) → `user` (comprador),
tanto para `SALE` como para `PURCHASE`; el tipo no invierte los participantes.

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
dotnet run --project Tests/SquadChecks/SquadChecks.csproj
dotnet run --project Tests/MarketChecks/MarketChecks.csproj
```

Para comprobar los gestos de la alineación con Edge instalado, sin cuentas ni
conexiones a Comunio:

```powershell
.\Tests\LineupDragChecks\BrowserChecks.ps1
```

Las comprobaciones de migración usan una API simulada. Cubren las cinco operaciones,
fallos parciales sin reintentos, caché del perfil, logout, renovación concurrente,
aislamiento de clientes. Las comprobaciones de interfaz e idiomas cubren los
resultados integrados y su ocultación en ambas pestañas. La instalación y firma final de la
APK, el uso real de Comunio, iOS/MacCatalyst y el despliegue PWA requieren su
verificación en los entornos correspondientes.
