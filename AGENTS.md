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

## Inyección de dependencias

Inyectar siempre interfaces de servicios en lugar de sus implementaciones
concretas, siempre que sea viable. Reutilizar y adaptar las interfaces existentes
cuando las operaciones pertenezcan a la misma responsabilidad. Crear una interfaz
nueva solo cuando represente una responsabilidad distinta y necesaria. Las clases
concretas se utilizan en el registro de dependencias y en las excepciones en
las que no resulte viable usar una interfaz.

## Simplicidad y responsabilidades

- Priorizar código directo, sencillo e intuitivo. Introducir abstracciones,
  clases de opciones o capas adicionales solo cuando resuelvan una necesidad
  actual del proyecto, no posibles usos futuros.
- Mantener como miembros privados la configuración y los auxiliares que solo
  necesita una clase, salvo que exista una necesidad real de configuración externa.
- Separar responsabilidades con dependencias claras. No usar métodos estáticos
  de otro servicio como atajo para evitar inyectarlo o para ocultar una dependencia
  circular. Si dos servicios comparten una responsabilidad, extraerla a un servicio
  con interfaz propia cuando eso elimine el acoplamiento y la duplicación.
- Simplificar el flujo real antes de extraer métodos. No considerar simplificado
  un método solo por trasladar su complejidad a auxiliares. Extraerlos cuando
  representen una operación coherente, reutilicen lógica o mejoren la comprensión.
- Unificar componentes que siempre colaboran en una misma operación cuando su
  separación solo añada encadenamiento o coordinación innecesaria.
- Elegir nombres que describan la operación y la responsabilidad del componente.
  En el gestor de sesión, usar LoadAsync y ClearAsync; reservar LoginAsync y
  LogoutAsync para el servicio de autenticación. Mantener el sufijo Async en los
  métodos asíncronos.

## Coordinación y concurrencia

- Usar únicamente los bloqueos necesarios para escenarios concurrentes concretos.
  Evitar semáforos que dupliquen la misma coordinación en varias capas y los
  bloqueos anidados innecesarios, sin eliminar la protección requerida por la
  renovación concurrente o los cambios de sesión.
- Mantener explícitos el ámbito y la finalidad de cada bloqueo. No introducir
  auxiliares solo para ocultar adquisiciones y liberaciones repetidas.
- Revisar las llamadas indirectas antes de mantener un bloqueo durante una
  operación asíncrona: un handler o servicio invocado puede intentar adquirir el
  mismo semáforo. Preferir eliminar esa dependencia circular del flujo.
- La carga inicial del perfil usa el token explícito y un cliente sin handler
  de sesión, permitiendo que LoadAsync proteja toda la carga y su limpieza con
  un único bloqueo. La limpieza dentro del bloqueo no debe volver a adquirirlo.
- Emitir las notificaciones de cambio de sesión después de liberar el bloqueo.

## Organización de clases C#

Mantener un orden uniforme de miembros para facilitar la lectura:

1. Constantes y campos, primero estáticos y después de instancia.
2. Constructores y finalizadores; si se usa constructor primario, mantenerlo en
   la declaración del tipo.
3. Propiedades e indexadores.
4. Eventos.
5. Métodos, agrupados por accesibilidad: públicos, internos, protegidos y privados.
   Dentro de cada grupo, ordenar por flujo de uso, de las operaciones principales
   a sus auxiliares.
6. Tipos anidados (clases, registros, estructuras y enumeraciones), al final.

Aplicar el mismo criterio a las interfaces para los miembros que admitan.
Ordenar los `using` con System primero, dependencias externas después y espacios
de nombres del proyecto al final; ordenar alfabéticamente dentro de cada grupo.
Esta es la convención del repositorio; no existe un único orden universal de
miembros impuesto por Clean Code. Evitar mezclar miembros de distintas categorías
o añadir regiones solo para separarlos.
