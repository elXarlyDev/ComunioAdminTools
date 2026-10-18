# Sesión de Comunio

Los avisos opcionales de login por Telegram se documentan en [Telegram.md](Telegram.md).

`AddComunioCore()` registra una sesión única por instancia de la app. Los servicios
deben recibir `IHttpClientFactory` y usar `CreateClient("Comunio")`, o heredar de
`ComunioService` pasando la factory a su constructor. Los clientes creados mediante
`new HttpClient()` no reciben autenticación. La carga inicial del perfil utiliza
`ComunioAuth`, sin handler, y establece Bearer explícitamente con el token recibido.

`AuthService.LoginAsync(usuario, contraseña)` guarda los tokens y su caducidad;
`LogoutAsync()` elimina los tokens y el perfil en memoria. El handler obtiene el token vigente por petición.
Los servicios usan rutas relativas sobre `https://api.comunio.es/` y no reintentan
operaciones automáticamente. El handler no valida el destino de las peticiones.

## Renovación

La renovación está configurada por defecto como `POST https://api.comunio.es/login`
con `Content-Type: application/json` y cuerpo `{ "refresh_token": "..." }`.
No envía usuario, contraseña ni el access token caducado. Todas las aplicaciones
reciben este comportamiento mediante `AddComunioCore()`.

La respuesta contiene `access_token`, `expires_in` (1800 segundos) y `refresh_token`.
Se calcula de nuevo la caducidad y se guarda el refresh token recibido. Si la
respuesta omite el refresh token, se conserva el anterior. Antes de cada petición
se renueva si quedan 60 segundos o menos; no se necesita un temporizador.
El margen de renovación es privado de `SessionManager`. `IComunioTokenClient`
encapsula las peticiones de login y renovación, la deserialización y el cálculo
de caducidad. Su implementación usa `ComunioAuth`, sin handler de sesión.

La renovación usa un bloqueo compartido. Solo se acepta HTTP 200 en las peticiones de autenticación; cualquier otro estado devuelve el mismo error genérico y elimina la sesión. Los errores de transporte durante la renovación mantienen los tokens.

## Plataformas

WebAssembly y Mobile mantienen tokens y perfil únicamente en la memoria de
SessionManager. Recargar la página o reiniciar el proceso requiere login. No hay
un almacén de tokens adicional. Mobile elimina al arrancar la antigua clave
`comunio.access-token.v1` de SecureStorage, que ya no se utiliza.
Para persistencia web protegida hay que añadir
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

AuthService solicita los tokens mediante IComunioTokenClient.LoginAsync y los
entrega mediante ISessionManager.LoadAsync. SessionManager utiliza
IComunioTokenClient.RefreshAsync para renovarlos, sin depender de AuthService.
ISessionManager expone LoadAsync para cargar tokens y perfil, ClearAsync para
eliminarlos, el perfil, los cambios de sesión y el acceso
al token vigente. Todos sus consumidores comparten la misma instancia por cliente.
SessionManager es el único propietario del estado de sesión. Su SessionState agrupa
TokenSession y SessionDataModel; la propiedad Session expone únicamente el perfil.
Session es null hasta completar el inicio y vuelve a null al eliminar la sesión.
No hay consultas asíncronas ni carga pública del perfil: se carga durante LoadAsync.
Un fallo de inicio elimina todo el estado y el access token almacenado.
La renovación sustituye los tokens conservando el perfil; logout y una renovación
rechazada eliminan ambos. Solo AuthService puede iniciar y cerrar la sesión; su semáforo coordina ambas operaciones.
SessionManager tiene un único semáforo para cambios de estado y renovación.
LoadAsync mantiene ese bloqueo durante toda la carga, incluida la petición del
perfil con token explícito. Esa petición no pasa por ComunioAuthHandler y no
vuelve a adquirir el semáforo. Si falla, se limpia el estado dentro del mismo bloqueo;
las notificaciones se emiten después de liberarlo.
El access token, refresh token, caducidad y perfil solo se conservan en memoria.
Tras cerrarse el proceso es obligatorio iniciar sesión de nuevo.

ComunioAuthHandler obtiene el token vigente mediante
ISessionManager.GetValidAccessTokenAsync antes de establecer Bearer en cada
petición autenticada. SessionManager comprueba la caducidad y renueva cuando
corresponde. El handler usa directamente el token devuelto por el gestor.
No se reenvían peticiones automáticamente.
