# Bushido

Prototipo jugable para móviles: un samurái recorre aldeas amenazadas por yōkai. El jugador corta objetivos, bloquea cuatro ataques del jefe, dibuja sellos y termina el encuentro con un desenvainado.

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
| Escenario y personajes | `BushidoPaperTheater.cs`, `BushidoPaperOgre.cs`, `BushidoCompanion.cs`, `BushidoForestTerrain.cs` |

Las herramientas de revisión están en `Assets/Bushido/Editor/`. Los recursos se agrupan en `Assets/Bushido/Resources/`. Conserva los archivos `.meta` junto a cada asset para preservar las referencias de Unity.

Los archivos originales que sirven para editar modelos y no se cargan en el juego están en `SourceAssets/`. Esta carpeta está fuera de `Assets/` para evitar que Unity los importe a la beta móvil.

## Recursos y créditos

- Los créditos de los iconos del mapa están en `Assets/Bushido/Resources/MapInk/CREDITS.txt`.
- Las licencias de recursos de terceros están dentro de `Assets/Bushido/Resources/ThirdParty/`.
- La procedencia de los sonidos proporcionados para el combate está en `Assets/Bushido/Resources/Audio/Combat/SOURCES.txt`.

## Estado de la beta

El proyecto contiene arte y animaciones provisionales. La compilación C# y la revisión visual en Unity son pasos distintos; los controles táctiles y el rendimiento requieren prueba en dispositivo. La carpeta `.gitignore` excluye cachés, compilaciones y archivos locales de Unity.

### Pendientes para siguientes iteraciones

- No se han realizado pruebas en dispositivos móviles. Los controles táctiles y el rendimiento siguen pendientes de validar en un teléfono horizontal.
- Se prevé mejorar los efectos y completar el último nivel.
- Consultar con el equipo la dirección de las peleas contra jefes, las animaciones y la música.
