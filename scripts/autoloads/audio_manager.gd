extends Node
## AudioManager — Reproducción de música y SFX, volumen, fade in/out.
## 3 categorías independientes según convención cross-proyecto: sonidos del juego, efectos interfaz, sonidos pop.

enum AudioCategory {
	MUSICA_Y_AMBIENT,  # sonidos del juego
	UI_FX,             # efectos interfaz
	BUBBLE_POP,        # sonidos pop
}

# Volúmenes por categoría (0.0 - 1.0)
var volume_game: float = 0.7
var volume_ui: float = 0.8
var volume_pop: float = 1.0
var vibration_enabled: bool = true

# Buses de audio (configurar en project settings o crear en runtime)
var _bus_indices: Dictionary = {}

func _ready() -> void:
	print("[AudioManager] inicializado")
	# TODO: crear buses de audio si no existen, mapear categorías a buses
	# TODO: cargar preferencias guardadas desde SaveManager

func play_music(track_name: String, fade_in: float = 1.0) -> void:
	# TODO: stop current music con fade out, start new music con fade in
	pass

func play_sfx(sfx_name: String, category: AudioCategory = AudioCategory.UI_FX) -> void:
	# TODO: cargar AudioStream desde res://assets/audio/sfx/{sfx_name}.ogg y reproducir
	pass

func vibrate(duration_ms: int = 50) -> void:
	if not vibration_enabled:
		return
	# TODO: usar Input.vibrate_handheld(duration_ms) para vibración hardware
	pass

func set_volume(category: AudioCategory, value: float) -> void:
	value = clamp(value, 0.0, 1.0)
	match category:
		AudioCategory.MUSICA_Y_AMBIENT: volume_game = value
		AudioCategory.UI_FX: volume_ui = value
		AudioCategory.BUBBLE_POP: volume_pop = value
	# TODO: aplicar al bus correspondiente vía AudioServer.set_bus_volume_db()
