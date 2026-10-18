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

`ISessionManager` es singleton por instancia de cliente y centraliza tokens y perfil en un estado interno en memoria. `AuthService.LoginAsync` carga el perfil una
vez antes de navegar a MainPage. Los servicios consultan la propiedad `Session`,
sin peticiones HTTP adicionales ni carga automática. Logout, un login fallido
o la invalidación de tokens eliminan el perfil. Renovar el access token no
recarga el perfil. La clasificación se sigue consultando en cada operación.
Los permisos del perfil permanecen en caché hasta iniciar otra sesión o llamar
a `AuthService.LogoutAsync()`; la API sigue validando cada operación de dinero.

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
```
