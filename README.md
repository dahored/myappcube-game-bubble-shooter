# Coralia

> Cozy underwater Bubble Shooter

**Estudio:** myappcube
**Engine:** Godot 4.3+
**Plataformas:** Android + iOS
**Estado:** Pre-producción (Fase 0)

## Sobre el juego

Coralia es un Bubble Shooter cozy submarino donde Marina, una joven sirena, restaura un arrecife de coral apagado rescatando criaturas marinas atrapadas en burbujas. Cada nivel completado devuelve color y vida al arrecife.

Modelo F2P híbrido (Ads + IAP + Battle Pass), audiencia objetivo: mujeres 25-45 casual.

## Requisitos para desarrollar

- **Godot 4.3+** ([download](https://godotengine.org/download))
- Mac (para builds iOS) o Windows/Linux (para builds Android)
- Cuenta Apple Developer ($99/año, para iOS)
- Cuenta Google Play Console ($25 una vez, para Android)

## Estructura del proyecto

```
coralia/
├── scenes/         # Escenas .tscn agrupadas por feature
├── scripts/        # Scripts GDScript (autoloads, gameplay, ui, data, utils)
├── resources/      # Resources .tres (criaturas, power-ups, battle passes)
├── data/levels/    # Archivos JSON de niveles
├── assets/         # Sprites, audio, fuentes, shaders
├── localization/   # CSVs de traducciones (6 idiomas)
├── platform/       # Configuración específica Android/iOS
├── docs/           # GDD, concept doc, wireframes
└── tests/          # Tests unitarios (gdUnit4)
```

Para detalles arquitectónicos completos ver `docs/02_GDD_Coralia.md` sección 14.

## Documentación

- `docs/Plan_Maestro_Bubble_Shooter.docx` — Plan original del proyecto
- `docs/01_Concepto_Inicial.md` — Visión y decisiones creativas
- `docs/02_GDD_Coralia.md` — Game Design Document completo
- `docs/03_Wireframes_Coralia.md` — Especificación de las 17 pantallas

## Setup inicial

1. Clonar el repositorio
2. Abrir Godot 4.3+ y seleccionar la carpeta del proyecto
3. Esperar a que Godot importe los assets (la primera vez tarda)
4. Verificar que los autoloads aparecen en Project Settings → Autoload (deben estar los 11)
5. Ejecutar el proyecto (F5) — debería abrir la pantalla `boot.tscn`

## Convenciones

Ver GDD sección 14.10 para naming, tipado, i18n.

## Licencia

Propiedad de myappcube. No redistribuir.
