# Avisos de login por Telegram

`AuthService` solicita un aviso solo después de completar el login y cargar el
perfil de `ElGatFumat` (comparación sin distinguir mayúsculas). El mensaje empieza
por `Chinchilla` e incluye el usuario y la fecha UTC. Nunca recibe ni envía la
contraseña, access token, refresh token ni el perfil de Comunio.

El envío se realiza mediante `ILoginNotifier`, implementado por
`TelegramLoginNotifier` con `Telegram.Bot`. Se ejecuta después de liberar el
bloqueo de autenticación. Si Telegram falla, la sesión permanece válida; no se
reintenta el envío y se registra un aviso genérico sin la excepción ni su URL,
que podría contener el token del bot. El cliente HTTP tiene un límite de cinco
segundos y sus logs HTTP están desactivados para no registrar ese token.

## Crear el bot y obtener el destino

1. En Telegram, abre https://t.me/BotFather y envía `/newbot`.
2. Elige el nombre y un usuario terminado en `bot`. Guarda el token en privado.
3. Abre el chat del bot y envía `/start` desde la cuenta que recibirá los avisos.
4. Desde un entorno privado, usa `GetUpdates` del cliente Telegram para obtener
   `update.Message.Chat.Id`. Ese número es el `chat_id` del destinatario.
   Un correo electrónico no identifica un chat de Telegram.

Documentación: https://telegrambots.github.io/book/1/quickstart.html

## Activación

La integración es opcional y no está activada en Web ni Mobile hasta configurar
un token y un `chat_id` reales. No se han enviado mensajes reales durante las pruebas.

El punto de entrada de la aplicación que realizará el envío debe registrar:

```csharp
builder.Services.AddComunioCore();
builder.Services.AddTelegramLoginNotifications(botToken, chatId);
```

`botToken` y `chatId` deben proceder de la configuración del entorno que ejecuta
el envío, no de constantes en el repositorio. No poner el token en `wwwroot`,
en un `appsettings.json` distribuido con la PWA ni en los ejemplos.

Esta implementación envía directamente desde el proceso que registra el servicio.
Si se activa en WebAssembly, el token será accesible a los usuarios del navegador;
una clave incluida en la APK también puede extraerse. Para conservarlo privado,
el envío debe ejecutarse en un entorno servidor. La librería en Core no cambia
esa limitación. El registro mediante `ILoginNotifier` permite sustituir el envío
directo por una llamada a un endpoint sin cambiar el login.

Las comprobaciones de migración usan exclusivamente transportes simulados para
Comunio y Telegram. Cubren el usuario destinatario, login fallido, carga del perfil,
contenido del aviso y conservación de sesión cuando falla Telegram.
