# Ámbito de trabajo y migración

El proyecto original de la raíz se ha retirado tras migrar sus funcionalidades.
Los proyectos activos son:

- `ComunioAdminTools.Web`: aplicación web que se desplegará como PWA.
- `ComunioAdminTools.UI`: componentes de interfaz compartidos.
- `ComunioAdminTools.Core`: lógica, servicios y autenticación compartidos.
- `ComunioAdminTools.Mobile`: aplicación móvil MAUI.

Realizar el desarrollo y las verificaciones sobre los proyectos activos. No
recrear el proyecto original ni sus carpetas de código en la raíz.
El script de APK y la carpeta privada de firma están en
`ComunioAdminTools.Mobile/Platforms/Android`.

La revisión de migración y los comandos de verificación se documentan en README.md.

Mantener la sesión aislada por instancia de cliente en la PWA WebAssembly. No
trasladar un singleton de sesión a un backend multiusuario.
