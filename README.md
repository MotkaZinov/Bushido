# Bushido

Prototipo jugable de un juego de acción con estética de teatro de papel japonés. Un samurái recorre aldeas amenazadas por yōkai: corta ranas durante las oleadas, bloquea ataques del jefe, dibuja sellos y termina el encuentro con un desenvainado.

## Abrir el proyecto

1. Abre esta carpeta en Unity Hub con Unity **6000.6.7f1**.
2. Abre `Assets/Scenes/CampaignMap.unity` y pulsa Play.
3. Desde el mapa elige un encuentro disponible y viaja a él.

El mapa y los objetos de combate se construyen al iniciar la escena. Para una beta móvil hay que verificar los gestos y el rendimiento en un teléfono horizontal.

## Controles

- Mouse o dedo: desliza para cortar ranas y trazar símbolos. El sonido de tajo se reproduce al acertar.
- Contra un jefe: dibuja los símbolos indicados para bloquear los cuatro golpes. Cuando se agota, traza el sello mostrado. Repite la secuencia hasta completar tres sellos.
- Ejecución: tira del mango de la katana para desencadenar el corte final.
- `Escape` o el botón de pausa: abre el menú para continuar o regresar al mapa.

## Organización

La escena principal está en `Assets/Scenes/`. El código propio está en `Assets/Bushido/Scripts/`; la clase `BushidoPrototype` se reparte entre archivos `partial` agrupados por responsabilidad:

| Sistema | Archivos de entrada |
|---|---|
| Flujo y pantallas | `BushidoPrototype.cs`, `BushidoPrototypeUI.cs`, `BushidoScrollPaper.cs` |
| Mapa y progreso | `BushidoCampaignBoard.cs`, `CampaignInkArt.cs`, `CampaignProgress.cs` |
| Oleadas y cortes | `BushidoPrototype.cs`, `FrogSlicer.cs`, `BushidoCutFeedback.cs`, `BushidoSwingFeedback.cs` |
| Jefe y defensa | `BushidoBossDuel.cs`, `BushidoDefense.cs`, `BushidoBossActing.cs`, `BushidoBossRitual.cs`, `BushidoSpiritSeal.cs` |
| Katana y efectos del remate | `BushidoKatana.cs`, `KatanaSpiritVfx.cs` |
| Escenario y personajes | `BushidoPaperTheater.cs`, `BushidoPaperOgre.cs`, `BushidoCompanion.cs`, `BushidoForestTerrain.cs` |

Las herramientas de revisión están en `Assets/Bushido/Editor/`. Los recursos se agrupan en `Assets/Bushido/Resources/`. Conserva los archivos `.meta` junto a cada asset para preservar las referencias de Unity.

Los archivos originales que sirven para editar modelos y no se cargan en el juego están en `SourceAssets/`. Esta carpeta está fuera de `Assets/` para evitar que Unity los importe a la beta móvil.

## Compartir y trabajar en equipo

La entrega debe incluir `Assets`, `Packages`, `ProjectSettings`, `SourceAssets`, `Tools`, este README y `.gitignore`. También incluye las pruebas y herramientas de revisión de `Assets/Bushido/Editor` y las licencias de los recursos. `SourceAssets` conserva los originales para futuras ediciones.

No compartan `Library`, `Temp`, `Logs`, `obj`, `UserSettings`, las preferencias locales del editor ni los archivos de solución generados. Unity reconstruye estos archivos al abrir el proyecto. La primera apertura requiere descargar los paquetes declarados e importar los recursos; es normal que tarde más y que la carpeta vuelva a crecer.

Para generar otra entrega, pueden ejecutar `Tools/preparar_entrega.py` con Python 3. Crea una carpeta editable y un ZIP junto al proyecto, comprueba que las copias coincidan y conserva el original. Si ya existe una entrega, cambien su nombre de salida antes de repetirlo.

Todos deben abrir la misma versión de Unity indicada arriba. Para colaborar con Git, suban el contenido del proyecto junto a `.gitignore` y conserven siempre cada recurso con su archivo `.meta`.

## Recursos y créditos

- Los créditos de los iconos del mapa están en `Assets/Bushido/Resources/MapInk/CREDITS.txt`.
- Las licencias de recursos de terceros están dentro de `Assets/Bushido/Resources/ThirdParty/`.
- La procedencia de los sonidos proporcionados para el combate está en `Assets/Bushido/Resources/Audio/Combat/SOURCES.txt`.

## Estado de la beta

El proyecto contiene arte y animaciones provisionales. La compilación C# y la revisión visual en Unity son pasos distintos. La carpeta `.gitignore` excluye cachés, compilaciones y archivos locales de Unity.

No se han realizado pruebas en dispositivos móviles. Los controles táctiles, las zonas seguras de la interfaz y el rendimiento siguen pendientes de validar en un teléfono horizontal.

### Pendientes para siguientes iteraciones

- Realizar pruebas en dispositivos móviles y ajustar los controles táctiles y el rendimiento para teléfono horizontal.
- Mejorar los efectos visuales.
- Completar el último nivel.
- Consultar con el equipo la dirección de las peleas contra jefes, las animaciones y la música.

## Crear recursos con IA

Usa esta descripción como punto de partida y sustituye los campos entre corchetes por lo que necesite el recurso:

> Ilustración de [personaje, objeto o escenario] para un juego japonés de teatro de papel. Estética de recortes de papel washi pintados a mano, contornos de tinta oscura, textura de fibras y acuarela, formas claras y expresivas, luz cálida, tonos crema, ocres, verdes apagados y rojo bermellón. Diseño legible durante el combate, coherente con una aldea japonesa y criaturas yōkai. [Pose, expresión, encuadre y detalles concretos]. Sin texto ni marcas de agua.

Para personajes y objetos recortables, pide fondo transparente, silueta completa y margen alrededor. Para escenarios, indica la proporción de pantalla y solicita capas separadas cuando deban moverse de forma independiente. Para efectos espirituales, añade humo translúcido azul verdoso y brillo de tinta, manteniendo visibles las siluetas. Revisa cada resultado dentro de Unity y documenta su procedencia junto al recurso.
