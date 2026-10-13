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
| `CashService` | Abonar dinero por puntos, revertir esos abonos y abonar 5 millones por el parón de selecciones. |
| `PointsService` | Consultar y ordenar los puntos de la última jornada. |
| `SquadService` | Consultar las plantillas y devolver las que superan los 25 jugadores. |

Las operaciones conservan las reglas de la aplicación original: 100.000 por punto
positivo, pagos de hasta 100 puntos por petición y los motivos originales.
Los puntos negativos o no numéricos no generan movimientos de dinero.
La API valida los permisos de las peticiones; la comprobación de administrador
en el cliente permanece desactivada, como en la implementación actual.

`ISessionManager` es singleton por instancia de cliente. `AuthService.LoginAsync`
carga los datos de usuario y comunidad al terminar el login; `CommunityService`
los reutiliza para obtener la comunidad. Las cargas simultáneas se comparten y los
fallos permiten reintentar. Un nuevo login, logout o invalidación de tokens limpia
la caché, sin conservar respuestas antiguas que todavía estuvieran en curso.
La restauración de tokens carga los datos en el primer uso; renovar el access token
no invalida el perfil. La clasificación se sigue consultando en cada operación.
Los permisos del perfil permanecen en caché hasta iniciar otra sesión o llamar
a `ISessionManager.Clear()`; la API sigue validando cada operación de dinero.

Los abonos se envían secuencialmente y no se reintentan. Ante un fallo,
`CashOperationException.ConfirmedPayments` contiene los importes cuya respuesta
fue satisfactoria, incluidos los bloques completados del usuario que falló.
La última petición fallida podría haberse aplicado aunque no se recibiera respuesta;
hay que revisar los movimientos antes de repetir una operación. No se ha añadido
idempotencia ni deduplicación que no esté soportada por la API.

`ComunioAdminTools.UI.AdminPage` gestiona la sesión, carga, errores y resultados y
conecta los callbacks del componente visual `MainPage` con estos servicios.
Web y Mobile muestran el mismo componente. Los modelos necesarios se han copiado
a `Core/Models`; el proyecto obsoleto permanece sin cambios.

Verificación con transportes simulados, sin operaciones reales en Comunio:

```sh
dotnet run --project Tests/ServiceChecks/ServiceChecks.csproj
```
