# ShadPs4Plus PKG Extractor GUI

Interfaz gráfica para Windows del extractor de paquetes PKG de ShadPs4Plus.

Este proyecto permite utilizar el motor de extracción `pkg_extractor` mediante una interfaz gráfica, facilitando la gestión de archivos PKG sin necesidad de ejecutar comandos manualmente.

## Características

- Añadir archivos PKG individualmente.
- Buscar archivos PKG dentro de carpetas.
- Administrar una cola de paquetes para extraer.
- Seleccionar la carpeta de destino de la extracción.
- Consultar el progreso, el estado y los mensajes del proceso.
- Mostrar información de los paquetes obtenida de `param.sfo`, cuando esté disponible.
- Identificar visualmente el tipo de paquete a partir de su nombre.

## Versiones disponibles

La aplicación se distribuye en dos versiones para adaptarse a diferentes necesidades.

### Light

Paquete ligero que contiene la aplicación principal.

- Requiere tener instalado el **.NET Desktop Runtime** compatible con la versión de la aplicación.
- No incluye el entorno de ejecución de .NET, por lo que debe instalarse por separado si aún no está disponible en el sistema.
- Recomendado para usuarios que ya tienen los componentes de ejecución necesarios instalados.

Descarga oficial de Microsoft: [.NET Desktop Runtime](https://dotnet.microsoft.com/en-us/download/dotnet/10.0)

En la página de Microsoft, selecciona la versión correspondiente y descarga el **.NET Desktop Runtime para Windows x64**.

### Standalone

Paquete que incluye la aplicación y los componentes de ejecución necesarios para ejecutarla de forma independiente.

- **No requiere instalar .NET Desktop Runtime por separado.**
- Recomendado para usuarios que desean ejecutar la aplicación sin tener que instalar previamente el entorno de ejecución de .NET.
- Ocupa más espacio que la versión Light debido a los componentes incluidos.

### ¿Qué versión debería descargar?

| Característica | Light | Standalone |
|---|---|---|
| Aplicación principal | Sí | Sí |
| .NET Desktop Runtime incluido | No | Sí |
| Requiere instalar .NET por separado | Sí, si no está instalado | No |
| Tamaño de descarga | Menor | Mayor |

## Detección del tipo de paquete

La aplicación utiliza el nombre del archivo para identificar visualmente el tipo de paquete:

- **Juego (`Juego`):** el nombre contiene `game`.
- **Actualización (`Update`):** el nombre contiene `update`.
- **Contenido descargable (`DLC`):** el nombre contiene `dlc`.
- **Desconocido:** el nombre no contiene ninguna de esas palabras o contiene varias de ellas.

Esta clasificación se basa únicamente en el nombre del archivo y no garantiza que el contenido real del paquete corresponda al tipo indicado.

## Requisitos

- Windows.
- La versión **Light** requiere el .NET Desktop Runtime compatible con la aplicación.
- La versión **Standalone** incluye los componentes de ejecución necesarios y no requiere instalarlos por separado.
- El motor de extracción `pkg_extractor` y sus bibliotecas necesarias, según la distribución utilizada.

## Instalación y uso

1. Descarga la versión deseada desde la sección [Releases](https://github.com/Dubstroy/ShadPs4Plus-PKG-Extractor-GUI/releases) de GitHub.
2. Extrae los archivos descargados en una carpeta.
3. Ejecuta `ShadPs4Plus.PKGExtractor.GUI.exe`.
4. Añade los archivos PKG que deseas procesar o busca paquetes dentro de una carpeta.
5. Selecciona la carpeta de destino.
6. Inicia la extracción y consulta el progreso y los mensajes de la aplicación.

**Importante:** si la versión incluye la carpeta `Engine`, conserva sus archivos junto al resto de la aplicación. El ejecutable del motor y sus bibliotecas deben permanecer en las ubicaciones esperadas por la aplicación.

## Motor de extracción

Esta aplicación utiliza el motor `pkg_extractor` del proyecto ShadPs4Plus.

Proyecto original: [AzaharPlus/shadPS4Plus](https://github.com/AzaharPlus/shadPS4Plus)

Según la documentación del proyecto original, ShadPs4Plus es un extractor independiente de paquetes PKG basado en código que anteriormente formaba parte de ShadPS4.

El motor de extracción original funciona mediante línea de comandos. Esta aplicación proporciona una interfaz gráfica para facilitar su utilización.

## Licencias y atribuciones

### Interfaz gráfica

El código de la interfaz gráfica de ShadPs4Plus PKG Extractor GUI se distribuye bajo la licencia MIT. Consulta el archivo [LICENSE](https://github.com/Dubstroy/ShadPs4Plus-PKG-Extractor-GUI/blob/ShadPs4Plus/LICENSE) para conocer sus condiciones.

### Motor de extracción

El motor `pkg_extractor` se identifica en el proyecto original como software bajo la licencia GPL-2.0.

La licencia MIT de esta interfaz gráfica no sustituye ni modifica las obligaciones aplicables al motor de extracción. Al redistribuirlo, deben respetarse las condiciones de su licencia y conservarse los avisos de autoría correspondientes.

Las bibliotecas y los componentes de terceros pueden estar sujetos a licencias adicionales.

## Aviso legal

Este proyecto es una interfaz gráfica independiente y no es un producto oficial de Sony ni está afiliado a Sony Interactive Entertainment o respaldado por dicha empresa.

El usuario es responsable de asegurarse de que tiene derecho a acceder, utilizar y extraer los archivos procesados mediante esta aplicación.