extends Node
## GameManager — Estado global del juego, signals y transiciones de pantalla.
## Este es el bus central de comunicación entre autoloads y escenas.

# Signals globales
signal level_started(level_id: int)
signal level_completed(level_id: int, score: int, stars: int)
signal level_failed(level_id: int, reason: String)
signal creature_rescued(creature_id: String)
signal chapter_completed(chapter_id: int)
signal screen_changed(from_screen: String, to_screen: String)

# Estado global
var current_level_id: int = 1
var current_chapter: int = 1
var is_paused: bool = false
var current_screen: String = "boot"

func _ready() -> void:
	print("[GameManager] inicializado")
	# TODO: cargar estado inicial desde SaveManager

func change_screen(screen_name: String) -> void:
	# TODO: implementar transición animada entre pantallas
	var prev := current_screen
	current_screen = screen_name
	screen_changed.emit(prev, screen_name)

func pause_game() -> void:
	is_paused = true
	get_tree().paused = true

func resume_game() -> void:
	is_paused = false
	get_tree().paused = false
