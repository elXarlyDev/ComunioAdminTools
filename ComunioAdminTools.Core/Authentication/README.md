# Sesión de Comunio

`AddComunioCore()` registra una sesión única por instancia de la app. Los servicios
deben recibir `IHttpClientFactory` y usar `CreateClient("Comunio")`, o heredar de
`ComunioService` pasando la factory a su constructor. Los clientes creados mediante
`new HttpClient()` no reciben autenticación.

`AuthService.LoginAsync(usuario, contraseña)` guarda los tokens y su caducidad;
`LogoutAsync()` elimina la sesión. El handler obtiene el token vigente por petición.
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

La renovación usa un bloqueo compartido; un `invalid_grant`, 401 o 403 elimina la
sesión. Los errores temporales de transporte o servidor mantienen los tokens.

## Plataformas

`ComunioAdminTools.Mobile` registra `SecureTokenStore` antes de `AddComunioCore()`.
El proyecto MAUI original `ComunioAdminTools.csproj` está obsoleto y no integra esta
autenticación; el desarrollo continúa en Web, UI, Core y Mobile.
WebAssembly usa `MemoryTokenStore`: conserva la sesión mientras la app está abierta;
recargar la página requiere login. Para persistencia web protegida hay que añadir
un backend que guarde los tokens por usuario y autentique el navegador mediante
cookie HttpOnly. No se ha creado un backend ni se guardan tokens en localStorage.
No reutilices este registro singleton para usuarios de un servidor multiusuario.

## Verificación

`dotnet run --project Tests/AuthenticationChecks/AuthenticationChecks.csproj`
comprueba la renovación concurrente, restauración, rotación de tokens, restricción
de dominio, errores temporales, invalidación y logout con transportes simulados.
