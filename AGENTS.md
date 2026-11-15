# Gobernanza del proyecto

Este archivo reúne las prácticas de desarrollo aplicables a todo el repositorio,
incluidos Web, UI, Core, Mobile y sus comprobaciones. Consultarlo antes de
modificar el proyecto.

## Mantenimiento de las prácticas

- Registrar aquí las convenciones compartidas y las decisiones que deban
  conservarse entre tareas, en la sección de su responsabilidad.
- Incorporar las prácticas acordadas con una indicación concreta de qué hacer
  y, cuando sea necesario, el motivo o el ámbito en que se aplican.
- Antes de añadir una regla, revisar las existentes y actualizar o unificar la
  correspondiente para evitar duplicaciones y contradicciones.
- Mantener aquí las reglas comunes. Documentar los detalles de un subsistema
  en su README y enlazarlo desde este archivo cuando sea necesario para aplicar
  una regla. Reservar los skills para procedimientos específicos reutilizables.
- No convertir una solución puntual en una regla general sin una necesidad
  compartida. Retirar o actualizar las prácticas que dejen de corresponder al
  funcionamiento real del proyecto.
- Mantener los comandos de ejecución y verificación en README.md y actualizar
  esa documentación cuando cambien, sin duplicarlos aquí.

## Ámbito de trabajo y migración

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
  En el gestor de sesión, usar Load y Clear; reservar Login y
  Logout para el servicio de autenticación.
- Nombrar los métodos asíncronos propios sin la palabra `Async`, también en
  interfaces, auxiliares, componentes Razor y pruebas. Por ejemplo: `Load`,
  `Clear`, `Login` y `Logout`. Actualizar sus referencias y documentación al
  renombrarlos. Conservar los nombres impuestos por contratos externos
  (overrides e implementaciones de interfaces del framework o bibliotecas),
  como `SendAsync`, `OnAfterRenderAsync` y `DisposeAsync`, y los de las llamadas
  a APIs externas.

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
  de sesión, permitiendo que Load proteja toda la carga y su limpieza con
  un único bloqueo. La limpieza dentro del bloqueo no debe volver a adquirirlo.
- Emitir las notificaciones de cambio de sesión después de liberar el bloqueo.

## Organización de clases C#

Separar con una línea en blanco las instrucciones que ocupen más de una línea
de las instrucciones adyacentes, tanto antes como después, en C# y en los
bloques `@code` de Razor, incluidas las pruebas. Aplicar la regla a declaraciones,
asignaciones, llamadas encadenadas y estructuras de control multilínea. Mantener
unidas las partes de una misma instrucción (`if/else`, `try/catch/finally`,
`do/while`), sus comentarios y sus argumentos o inicializadores. No añadir líneas
en blanco junto a las llaves que delimitan el bloque ni separar instrucciones
consecutivas de una sola línea por esta regla.

Usar llaves también cuando el cuerpo de un `if` contenga únicamente una
instrucción `return` o `throw`, aunque quepa en una sola línea. Escribir la
apertura, la instrucción y el cierre en líneas separadas. Aplicar esta regla en
C#, en los bloques `@code` de Razor y en las pruebas.

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
Separar con una línea en blanco las propiedades cuyos getters contengan lógica,
tanto con cuerpo `get { ... }` como con expresión `=>`, de los miembros adyacentes.
Aplicar también esta regla a indexadores y propiedades RenderFragment, incluidos
los bloques `@code` de Razor. Las propiedades automáticas relacionadas pueden
permanecer agrupadas sin líneas en blanco entre ellas.

Ordenar los `using` con System primero, dependencias externas después y espacios
de nombres del proyecto al final; ordenar alfabéticamente dentro de cada grupo.
Esta es la convención del repositorio; no existe un único orden universal de
miembros impuesto por Clean Code. Evitar mezclar miembros de distintas categorías
o añadir regiones solo para separarlos.
