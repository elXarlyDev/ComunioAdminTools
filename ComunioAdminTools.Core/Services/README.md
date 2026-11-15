# Operaciones migradas

La jerarquía es `ComunioService` → `CommunityService` → `CashService`,
`PointsService` y `SquadService`. `ComunioService` proporciona el cliente HTTP
autenticado y los métodos de envío. `CommunityService` es una clase abstracta que
recibe `ISessionManager` y proporciona el acceso a sesión y clasificación.
Los servicios concretos usan esos métodos heredados, sin inyectarse entre sí.
`AddComunioCore()` registra los servicios concretos y el gestor compartido.

| Servicio | Responsabilidad |
| --- | --- |
| `CommunityService` | Base abstracta para obtener la sesión y consultar la clasificación de la comunidad. |
| `CashService` (`ICashService`) | Preparar y aplicar abonos por puntos, reversiones, abonos de 5 millones y correcciones según noticias. |
| `PointsService` | Consultar los puntos de la última jornada y las clasificaciones mensuales o por mitades. |
| `SquadService` (`ISquadService`) | Consultar el exceso de todas las plantillas y el estado detallado de los jugadores del usuario con sesión. |

`ISquadService.GetSquadStatus()` obtiene `users/{usuario}/squad?eid=live` como
`SquadModel`. Para cada jugador consulta
`communities/{comunidad}/users/{usuario}/players/{jugador}`, con los identificadores
de la sesión, y devuelve los detalles ordenados por posición (PT, DF, MC, DL) y
después por nombre alfabético según el idioma seleccionado. Los jugadores sin
posición se muestran al final. Las peticiones
se realizan secuencialmente con el cliente autenticado. Si falla un detalle,
la operación falla sin presentar una lista parcial como resultado completo.
`PlayerDetailsModel` conserva nombre, club, posición (`type`), `status` y
`statusInfo`; los campos opcionales pueden ser nulos. La posición se deserializa
al enumerado `PlayerPosition`: `keeper`, `defender`, `midfielder` y `striker`.
`status` se deserializa al enumerado `PlayerStatus`, que incluye los trece estados
de Comunio. La interfaz decide sus iconos y oculta `statusInfo` para `Active`.

`ISquadService.GetLineup()` consulta
`communities/{comunidad}/users/{usuario}/lineup` con la sesión actual. Devuelve
`LineupResult` con la formación `tactic` y once huecos ordenados de `position` 11 a 1,
manteniendo vacíos los que no tienen
jugador. Los datos y estados de los titulares proceden de `items.lineup`.
Se rechazan posiciones fuera del intervalo, huecos duplicados y jugadores
repetidos. La interfaz excluye estos identificadores del listado de suplentes.
`LineupFormation` representa las cinco formaciones permitidas: 442, 343, 352, 433
y 451. Una formación ausente o no admitida produce un error de respuesta inválida.
La interfaz asigna la posición de cada hueco según la formación y comprueba ambos
destinos antes de intercambiar jugadores. Al cambiarla conserva únicamente los
jugadores compatibles con sus huecos actuales.
Arrastrar jugadores para quitar, añadir o intercambiar titulares modifica
únicamente la selección local. `ISquadService.SaveLineup()` valida la formación,
los once huecos, la ausencia de duplicados y la posición de cada jugador. Envía
un PUT al endpoint de alineación con el `userId` numérico de la sesión, `tactic`
y `lineup` (claves 1–11 e ids como cadenas, vacías para huecos libres), incluyendo
`substitutes` con `striker`, `midfielder`, `defender` y `keeper` vacíos, y
`type: "default"`. No realiza reintentos ni necesita deserializar una respuesta
del PUT, por lo que también acepta HTTP 204. La interfaz llama después a
`GetLineup()` y confirma el guardado únicamente tras completar la recarga.

`GetSquadExcess()` conserva el recuento de jugadores comprados antes del timestamp
de la jornada seleccionada en `/matchdays` para todos los usuarios.

Las operaciones conservan las reglas de la aplicación original: 100.000 por punto
positivo, pagos de hasta 100 puntos por petición y los motivos originales.
Los puntos negativos o no numéricos no generan movimientos de dinero.
La API valida los permisos de las peticiones; la comprobación de administrador
en el cliente permanece desactivada, como en la implementación actual.

`ISessionManager` es singleton por instancia de cliente y centraliza tokens y perfil en un estado interno en memoria. `AuthService.Login` carga el perfil una
vez antes de navegar a MainPage. Los servicios consultan la propiedad `Session`,
sin peticiones HTTP adicionales ni carga automática. Logout, un login fallido
o la invalidación de tokens eliminan el perfil. Renovar el access token no
recarga el perfil. La clasificación se sigue consultando en cada operación.
Los permisos del perfil permanecen en caché hasta iniciar otra sesión o llamar
a `AuthService.Logout()`; la API sigue validando cada operación de dinero.

`ICashService.Prepare()` consulta la clasificación y crea un `CashPlan` con los
usuarios e importes calculados, sin realizar movimientos. La interfaz muestra
esa instantánea para aceptar o cancelar las cuatro operaciones, incluida la
reversión con importes negativos. `Apply()` utiliza los mismos importes sin
recalcular, verifica que sigue siendo la misma sesión y consume el cálculo una
sola vez. Se mantienen los bloques de hasta 100 puntos y los motivos originales.
La interfaz inyecta `ICashService` y los resultados usan `CashResultModel`.

`CashOperation.Correction` obtiene la última jornada finalizada y el inicio de
la siguiente en `matchdays`. Lee `communities/{comunidad}/users/{usuario}/news`
con `originaltypes=true`, `limit=20` y `start=0,20,40...` hasta encontrar el cálculo
de puntos de esa jornada y completar su intervalo. Suma todos los
`TRANSACTION_BONUS` cuya fecha sea mayor o igual a la noticia y menor que el
inicio siguiente, sin consultar sus razones. Ignora noticias repetidas por id y
continúa otra página si coincide la fecha límite inferior con el final de página.
El destinatario se extrae de `message.text` antes del administrador; los importes
se comparan con los puntos positivos a 100.000 por punto. Solo las diferencias
no nulas forman el cálculo a confirmar. Fechas ausentes, abonos ilegibles,
destinatarios ambiguos o páginas repetidas impiden calcular importes incorrectos.
El lector admite abonos y penas disciplinarias con o sin símbolo €, y convierte
«han sido restados» en importes negativos sin invertir dos veces un importe ya
negativo. Se compara el saldo neto de abonos y cargos, manteniendo la exclusión del
nombre del administrador y sin interpretar la razón del movimiento.
Las lecturas reciben un `CancellationToken`. Cada página de noticias aplica
un plazo de 30 segundos a toda la lectura y a su espera, incluso si el transporte
no atiende la cancelación. Los GET validan el estado HTTP al recibir las cabeceras
y cancelan también la descarga del cuerpo, manteniendo el timeout del cliente.
Las trazas de depuración señalan cada etapa sin registrar credenciales ni cuerpos.
La prueba opcional `dotnet run --project Tests/CashChecks/CashChecks.csproj -- --timeout`
comprueba además el plazo real de 30 segundos con una respuesta simulada bloqueada.

Los abonos se envían secuencialmente y no se reintentan. Ante un fallo,
`CashOperationException.ConfirmedPayments` contiene los importes cuya respuesta
fue satisfactoria, incluidos los bloques completados del usuario que falló.
La última petición fallida podría haberse aplicado aunque no se recibiera respuesta;
hay que revisar los movimientos antes de repetir una operación. No se ha añadido
idempotencia ni deduplicación que no esté soportada por la API.

LoginPage gestiona el acceso y MainPage las operaciones, la carga, los errores y los resultados. Web y Mobile comparten ambas páginas. Los modelos necesarios se han copiado
a `Core/Models`; el proyecto obsoleto se ha eliminado.

Verificación con transportes simulados, sin operaciones reales en Comunio:

```sh
dotnet run --project Tests/MigrationChecks/MigrationChecks.csproj
dotnet run --project Tests/CashChecks/CashChecks.csproj
```
