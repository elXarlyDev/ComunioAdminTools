# Sesión de Comunio

`AddComunioCore()` registra una sesión única por instancia de la app. Los servicios
deben recibir `IHttpClientFactory` y usar `CreateClient("Comunio")`, o heredar de
`ComunioService` pasando la factory a su constructor. Los clientes creados mediante
`new HttpClient()` no reciben autenticación.

`AuthService.LoginAsync(usuario, contraseña)` guarda los tokens y su caducidad;
`LogoutAsync()` elimina los tokens y el perfil en memoria. El handler obtiene el token vigente por petición.
Solo admite HTTPS en `api.comunio.es` y no reintenta operaciones automáticamente.

## Renovación

La renovación está configurada por defecto como `POST https://api.comunio.es/login`
con `Content-Type: application/json` y cuerpo `{ "refresh_token": "..." }`.
No envía usuario, contraseña ni el access token caducado. Todas las aplicaciones
reciben este comportamiento mediante `AddComunioCore()`.

La respuesta contiene `access_token`, `expires_in` (1800 segundos) y `refresh_token`.
Se calcula de nuevo la caducidad y se guarda el refresh token recibido. Si la
respuesta omite el refresh token, se conserva el anterior. Antes de cada petición
se renueva si quedan 60 segundos o menos; no se necesita un temporizador.
`ComunioAuthOptions.CreateRefreshRequest` permite sustituir el contrato si cambia.

La renovación usa un bloqueo compartido. Solo se acepta HTTP 200 en las peticiones de autenticación; cualquier otro estado devuelve el mismo error genérico y elimina la sesión. Los errores de transporte durante la renovación mantienen los tokens.

## Plataformas

`ComunioAdminTools.Mobile` registra `SecureTokenStore` antes de `AddComunioCore()`.
El proyecto MAUI original se ha retirado; el desarrollo continúa en Web, UI, Core
y Mobile.
WebAssembly usa `MemoryTokenStore`: conserva la sesión mientras la app está abierta;
recargar la página requiere login. Para persistencia web protegida hay que añadir
un backend que guarde los tokens por usuario y autentique el navegador mediante
cookie HttpOnly. No se ha creado un backend ni se guardan tokens en localStorage.
No reutilices este registro singleton para usuarios de un servidor multiusuario.

## Verificación

`dotnet run --project Tests/MigrationChecks/MigrationChecks.csproj`
comprueba la renovación concurrente, el login obligatorio tras reiniciar el proceso,
logout y aislamiento entre instancias de cliente, además de las operaciones de
negocio, con transportes simulados. No realiza peticiones reales a Comunio.

El perfil se carga una sola vez durante el login y se guarda en SessionManager.Session.
Las consultas de sesión no hacen HTTP. Tras logout, permanece vacío hasta el siguiente login.

## Responsabilidades de autenticación

ITokenStore solo expone SetTokenAsync(string?) y GetTokenAsync(). En Web,
MemoryTokenStore mantiene el access token en memoria; en MAUI, SecureTokenStore
lo lee y escribe mediante SecureStorage. No valida ni renueva tokens.

AuthService solicita el token por HTTP, lo guarda mediante ITokenStore y,
después de liberar el bloqueo, entrega los datos a SessionManager.StartAsync.
SessionManager conserva los datos del token y el perfil, y controla su renovación
y eliminación. El perfil, refresh token y caducidad solo se conservan en memoria. Tras cerrarse el proceso es obligatorio iniciar sesión de nuevo, aunque SecureStorage conserve el access token.

SessionRefreshHandler comprueba la vigencia con SessionManager antes de cada
petición autenticada. Después, ComunioAuthHandler solo obtiene el string de
ITokenStore y establece Bearer; no valida la caducidad ni renueva tokens.
No se reenvían peticiones automáticamente.
