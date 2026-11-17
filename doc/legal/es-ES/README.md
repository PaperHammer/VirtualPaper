# VirtualPaper: privacidad, procesamiento con IA y derechos de autor

Versión del documento: 1.0. Actualizado: 2026-11-15. Editor y responsable del proyecto: PaperHammer.

Este documento explica cómo las ediciones EXE y Microsoft Store de VirtualPaper tratan los datos y qué derechos debe tener en cuenta al utilizar las funciones de IA y el contenido incluido. Los sitios y servicios externos tienen sus propias políticas.

<a id="privacy-policy"></a>
## Política de privacidad

### Archivos y ajustes locales

VirtualPaper lee las imágenes, vídeos, imágenes animadas y proyectos Web que elige para mostrarlos, editarlos o procesarlos. Los metadatos, miniaturas, favoritos, etiquetas, ajustes preestablecidos de efectos, rutas de proyectos recientes, ajustes y distribuciones de monitores se guardan localmente. No necesita una cuenta de VirtualPaper. Estos materiales y registros no se envían al responsable del proyecto para inferencia de IA, publicidad ni análisis de comportamiento.

La carpeta predeterminada es `%LOCALAPPDATA%\VirtualPaper`. Windows puede redirigir los datos de aplicaciones empaquetadas. Las bibliotecas pueden usar la ubicación elegida en Ajustes; los proyectos e imágenes exportados permanecen donde elija. La vista previa, deshacer, los mapas de profundidad y los datos del navegador pueden crear archivos locales adicionales. No son una copia de seguridad en la nube.

### Diagnóstico e informes

Los registros locales pueden contener versiones de la aplicación y Windows, idioma, información de CPU/GPU, errores, rutas de archivos y detalles de reproducción. Exportar registros crea un ZIP local con registros, ajustes de usuario y distribuciones de fondos. No se envía automáticamente a PaperHammer. Revíselo antes de compartirlo: las rutas pueden revelar su nombre de usuario de Windows o nombres de proyectos privados. Los informes y adjuntos que publique voluntariamente en GitHub son tratados por GitHub y pueden ser públicos.

### Ubicación

Al activar la adaptación horaria, la aplicación solicita acceso a la ubicación de Windows y utiliza latitud y longitud localmente para calcular el amanecer y el atardecer. Sin acceso utiliza una ubicación predeterminada; la iluminación puede no coincidir con su región. Desactive la función para detener sus solicitudes de ubicación y gestione el permiso en Privacidad y seguridad de Windows. VirtualPaper no envía estas coordenadas al responsable del proyecto para ese cálculo.

### Red y servicios de terceros

La edición EXE puede contactar con GitHub y servidores de descarga para buscar y obtener actualizaciones de la aplicación o complementos. La instalación puede descargar componentes de ejecución de Microsoft. Microsoft gestiona la distribución y las actualizaciones de Store. Las solicitudes revelan al destinatario información de conexión, como la dirección IP y las cabeceras. Abrir enlaces de ayuda, comentarios o código conecta con el sitio elegido. Los fondos y proyectos Web importados pueden cargar por su cuenta scripts, medios o servicios remotos controlados por sus autores.

El diagnóstico de WebView2 y SmartScreen envían información a Microsoft; consulte su [declaración de privacidad](https://privacy.microsoft.com/privacystatement). Algunos informes siguen los ajustes de diagnóstico de Windows. Estos mecanismos son distintos de la inferencia local de VirtualPaper.

### Conservación, seguridad y opciones

Los ajustes, bibliotecas, proyectos, cachés de profundidad y perfiles del navegador guardados permanecen localmente hasta que usted o una operación de limpieza o desinstalación aplicable los elimine. Los resultados temporales de IA o edición pueden borrarse al iniciar de nuevo; guárdelos o añádalos a la biblioteca para conservarlos. No todos los registros y cachés tienen un plazo fijo de eliminación automática. Desinstalar no garantiza eliminar bibliotecas trasladadas ni archivos exportados.

Puede consultar, exportar, corregir o eliminar sus archivos locales y cambiar los ajustes correspondientes. Antes de borrar datos manualmente, cierre VirtualPaper y sus procesos en segundo plano y haga copia de los archivos que desee conservar. Los datos locales dependen de las protecciones de la cuenta de Windows y del sistema de archivos; VirtualPaper no ofrece un almacén cifrado independiente. Use proyectos de confianza y proteja su cuenta de Windows.

### Contacto y cambios

Para consultas de privacidad o solicitudes sobre información facilitada voluntariamente al proyecto, contacte con PaperHammer mediante [GitHub Issues](https://github.com/PaperHammer/VirtualPaper/issues). No publique información sensible; consulte primero cómo enviarla de forma adecuada. La información conservada por GitHub, Microsoft o servicios externos de un fondo está sujeta a los controles de esos proveedores. Este aviso se actualizará cuando cambie el comportamiento relevante; su versión y fecha identifican la copia incluida con la aplicación.

<a id="ai-processing-and-rights"></a>
## Procesamiento con IA y derechos

La estimación de profundidad, transferencia de estilo y mejora de imágenes usan modelos en su equipo y CPU/GPU según la función y los ajustes. Estas inferencias no llaman a servicios de IA en la nube. Los modelos deben estar disponibles localmente. La calidad y la velocidad dependen de la fuente, el modelo y el hardware; los detalles generados pueden ser inexactos y el mapa de profundidad es una estimación, no una medición.

Procesar contenido con IA no le otorga, por sí solo, derechos de autor sobre el material original o el resultado, ni permiso para utilizar material ajeno. La protección del resultado y la titularidad de sus derechos dependen de la legislación aplicable, de la licencia del material original y de la contribución creativa humana. Las restricciones existentes no desaparecen automáticamente tras el procesamiento con IA. Antes de publicar, redistribuir o utilizar un resultado con fines comerciales, compruebe que dispone de los derechos o permisos necesarios. También debe tener en cuenta los derechos de autor, de imagen, de marca y otros derechos de terceros.

VirtualPaper no reclama la propiedad de sus entradas o resultados por el mero uso de la aplicación. La licencia de código abierto no somete automáticamente sus materiales o salidas a GPL. Las licencias de modelos y componentes de ejecución son independientes de los permisos de las imágenes; un modelo gratuito no garantiza resultados sin restricciones. VirtualPaper no garantiza ausencia de infracción, exclusividad ni idoneidad comercial. Este aviso no sustituye los permisos necesarios ni limita sus derechos legales.

Algunos paisajes incluidos se crearon con la generación de imágenes de OpenAI al preparar la colección. Es independiente de la inferencia local de la aplicación y no implica enviar sus imágenes a OpenAI. Sus avisos individuales indican procedencia y condiciones; no se reclama copyright exclusivo sobre esas imágenes generadas con IA.

<a id="copyright-and-licenses"></a>
## Derechos de autor y licencias

El código de VirtualPaper se distribuye bajo [GNU GPL versión 3](../licenses/GPL-3.0.txt). El código fuente y las versiones están en el [repositorio de VirtualPaper](https://github.com/PaperHammer/VirtualPaper). Las condiciones de garantía y redistribución figuran en la licencia; los derechos legales imperativos no se ven afectados.

Los componentes de terceros y los pesos de los modelos están sujetos a sus propios avisos de derechos de autor y licencias. Consulte la [información sobre terceros](../THIRD-PARTY.txt), junto con las licencias y los archivos NOTICE originales incluidos. Las traducciones y explicaciones se ofrecen como referencia; no sustituyen las condiciones originales ni conceden una autorización general sobre materiales de terceros.

Los fondos incluidos tienen su propia procedencia y condiciones. El vídeo original de Santa Rosa Waves es de Blervis y las imágenes originales de Bigora Cascade son de Termininja; las adaptaciones incluidas usan CC0. Otros avisos identifican las imágenes generadas o el código de animación de VirtualPaper. Conserve el `NOTICE.txt` y las licencias de cada fondo al redistribuirlo. El crédito de un editor o adaptador no sustituye al autor original ni cambia la licencia original.

CC0 no exige atribución; los datos de autor y fuente documentan la procedencia sin añadir condiciones de redistribución.
