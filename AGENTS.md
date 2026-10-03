# Ámbito de trabajo y migración

El proyecto original `ComunioAdminTools.csproj`, situado en la raíz, está obsoleto.
Se está migrando a estos proyectos activos:

- `ComunioAdminTools.Web`: aplicación web que se desplegará como PWA.
- `ComunioAdminTools.UI`: componentes de interfaz compartidos.
- `ComunioAdminTools.Core`: lógica, servicios y autenticación compartidos.
- `ComunioAdminTools.Mobile`: aplicación móvil MAUI.

Realizar el desarrollo y las verificaciones sobre los proyectos activos. No
modificar ni integrar funcionalidades en el proyecto original o sus archivos
(`MainPage.xaml`, `MainPage.xaml.cs`, `MauiProgram.cs`, `App*`, `Controls`, `Models`,
`Helpers`, `Platforms` y `Resources` de la raíz), salvo petición explícita del
usuario para ese proyecto. Pueden consultarse como referencia para la migración.

Mantener la sesión aislada por instancia de cliente en la PWA WebAssembly. No
trasladar un singleton de sesión a un backend multiusuario.
