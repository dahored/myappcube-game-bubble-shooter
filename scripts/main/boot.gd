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
	# Durante Fase 1 / Chunk 1 saltamos directo al gameplay para iterar el grid.
	# TODO: cuando se construyan onboarding y santuario, restaurar el routing real:
	#   var tutorial_done: bool = SaveManager.data.get("tutorial_completed", false)
	#   if not tutorial_done: change_scene_to_file("res://scenes/main/onboarding.tscn")
	#   else: change_scene_to_file("res://scenes/santuario/santuario.tscn")
	print("[Boot] cargando Gameplay (Chunk 1 — grid hexagonal)")
	get_tree().change_scene_to_file("res://scenes/gameplay/gameplay.tscn")
