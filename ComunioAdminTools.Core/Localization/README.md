# Traducciones compartidas

Los textos de la interfaz y los errores de los servicios usan estos recursos:

- `Strings.es-ES.resx`: español.
- `Strings.ca-ES-valencia.resx`: valenciano.
- `TextKey.cs`: claves disponibles para usar desde C# y Razor.

`AppLocalization` se registra mediante `AddComunioCore()` y pertenece a una
instancia de cliente WebAssembly o MAUI. No modifica la cultura global del
proceso. Los dos catálogos se incluyen en el ensamblado Core para cambiar de
idioma sin descargar ensamblados satélite ni reiniciar la sesión.

Uso desde la interfaz y los servicios:

```csharp
text[TextKey.SignIn]
text.Format(TextKey.CashOperationError, userName)
text.ErrorMessage(exception)
```

La interfaz `ILocalizedError` conserva la clave y sus argumentos. Los errores
implementados en Core traducen también `Message`; la interfaz puede volver a
traducir una excepción ya producida cuando cambia el idioma. Los errores HTTP
mantienen su código de estado y su causa, pero no muestran respuestas externas
sin traducir. Los errores desconocidos tienen un mensaje genérico traducido.
Los motivos de los movimientos enviados a la API conservan los valores originales.

`LocalizationProvider` detecta la preferencia del navegador, muestra el selector,
propaga la cultura a los componentes y guarda únicamente el idioma en
`localStorage` bajo `comunio.language`. Si no hay un idioma compatible, usa
español. Las variantes `ca` se muestran como valenciano. Si el almacenamiento no
está disponible, la selección sigue funcionando en memoria.

El código de arranque JavaScript lee copias de los mismos `.resx` generadas al
compilar UI como archivos XML públicos. Traduce los avisos de carga y error antes de que
arranque .NET, cambia el atributo `lang` del documento y los avisos de error
estáticos. No hay una segunda lista de traducciones. La PWA incluye estos XML en
su caché offline. Los importes y puntos se formatean con la cultura seleccionada.

Para añadir un texto, crea una clave en `TextKey` y una entrada en ambos recursos.
Mantén los mismos marcadores `{0}`, `{1}`, etc. en las dos traducciones. Los
nombres propios de Comunio y los nombres de usuario no se traducen.

Verificación:

```powershell
dotnet run --project Tests/LocalizationChecks/LocalizationChecks.csproj
```

Comprueba todos los recursos de ambos idiomas, errores de autenticación y HTTP,
conexión, timeout, JSON, aislamiento entre clientes y cambio de idioma de un
diálogo ya abierto. No hace peticiones reales a Comunio.

Con Web ejecutándose en `http://localhost:5198` y Edge instalado en Windows:

```powershell
.\Tests\LocalizationChecks\BrowserChecks.ps1
```

Usa un perfil temporal de Edge sin ventana visible. Comprueba el selector,
validación de credenciales vacías, mensajes de error, recarga y persistencia de
ambos idiomas. No introduce credenciales ni realiza movimientos en Comunio.
