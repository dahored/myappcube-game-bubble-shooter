extends Node
## Boot — primer script que se ejecuta al abrir el juego.
## Inicializa servicios y decide si mostrar Onboarding o ir directo al Santuario.

func _ready() -> void:
	print("[Boot] arrancando Coralia v" + ProjectSettings.get_setting("application/config/version", "0.0.0"))
	await get_tree().create_timer(0.1).timeout  # dejar que autoloads terminen _ready
	_initialize_services()
	_route_to_first_screen()

func _initialize_services() -> void:
	# TODO: Firebase init, remote config fetch, IAP fetch products
	# TODO: pre-cargar assets críticos (UI base, sprites de splash)
	print("[Boot] servicios inicializados (stub)")

func _route_to_first_screen() -> void:
	# Durante Fase 1/2 saltamos directo al gameplay para iterar.
	# TODO: cuando se construyan onboarding y santuario, restaurar routing real
	# Convención casual mobile: cargar el SIGUIENTE nivel a jugar = highest_completed + 1
	# Si ganaste el nivel 2, al reabrir cargás el 3 (listo para avanzar).
	# Si nunca ganaste nada, cargás el 1.
	# Si ganaste todos, cargás el último (replayable).
	var highest: int = SaveManager.data.get("highest_level_completed", 0)
	var total_levels: int = LevelManager.get_total_levels()
	var next_to_play: int = clamp(highest + 1, 1, max(1, total_levels))
	GameManager.current_level_id = next_to_play
	print("[Boot] cargando Gameplay desde nivel %d (highest completed: %d)" % [GameManager.current_level_id, highest])
	get_tree().change_scene_to_file("res://scenes/gameplay/gameplay.tscn")
