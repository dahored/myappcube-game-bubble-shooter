extends Node
## SaveManager — Guardar/cargar progreso local (JSON encriptado) y sincronizar con cloud.
## Save format documentado en GDD sección 14.5.

const SAVE_PATH := "user://save_game.dat"
const SAVE_VERSION := "1.0.0"

signal save_loaded()
signal save_failed(reason: String)
signal cloud_sync_completed()

var data: Dictionary = {}

func _ready() -> void:
	print("[SaveManager] inicializado")
	load_save()

func load_save() -> void:
	if not FileAccess.file_exists(SAVE_PATH):
		data = _default_save()
		save_to_disk()
		save_loaded.emit()
		return
	# TODO: leer archivo, desencriptar AES-256, parsear JSON, validar versión
	# Por ahora carga vacía
	data = _default_save()
	save_loaded.emit()

func save_to_disk() -> void:
	# TODO: serializar `data` a JSON, encriptar AES-256 con key derivada del UUID + salt fijo
	# TODO: escribir a SAVE_PATH
	pass

func sync_with_cloud() -> void:
	# TODO: comparar timestamps con Firestore, resolver conflictos según GDD 14.5
	pass

func _default_save() -> Dictionary:
	return {
		"version": SAVE_VERSION,
		"player_id": _generate_player_id(),
		"username": "",
		"current_level": 1,
		"highest_level": 0,
		"creatures_rescued": [],
		"currencies": {"coins": 0, "gems": 50},  # arranque con 50 gemas
		"lives": 5,
		"lives_last_regen": Time.get_unix_time_from_system(),
		"streak": {
			"current": 0,
			"longest": 0,
			"last_claim_day": 0,
			"last_login_timestamp": 0
		},
		"battle_pass": {
			"season": 1,
			"is_premium": false,
			"tier": 0,
			"xp_current_tier": 0
		},
		"achievements": [],
		"settings": {
			"language": OS.get_locale_language(),
			"music_volume": 0.7,
			"ui_volume": 0.8,
			"pop_volume": 1.0,
			"vibration_enabled": true,
			"theme": "auto",  # auto | light | dark
			"notifications_enabled": true
		},
		"iap_history": [],
		"tutorial_completed": false
	}

func _generate_player_id() -> String:
	return "player_%d_%d" % [Time.get_unix_time_from_system(), randi() % 100000]
