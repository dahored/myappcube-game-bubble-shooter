extends Node
## SaveManager — Guarda y carga el progreso del jugador en JSON local.
## En el MVP no usamos encriptación todavía (placeholder para post-launch).
## Esquema de save documentado en GDD sección 14.5.

const SAVE_PATH := "user://save_game.json"
const SAVE_VERSION := "1.0.0"

signal save_loaded()
signal save_failed(reason: String)

var data: Dictionary = {}


func _ready() -> void:
	print("[SaveManager] inicializado")
	load_save()


## Carga el save desde disco. Si no existe o está corrupto, usa el default.
func load_save() -> void:
	if not FileAccess.file_exists(SAVE_PATH):
		print("[SaveManager] no existe save previo, usando default")
		data = _default_save()
		save_to_disk()
		save_loaded.emit()
		return

	var file := FileAccess.open(SAVE_PATH, FileAccess.READ)
	if not file:
		push_error("[SaveManager] no se pudo abrir save file")
		data = _default_save()
		save_failed.emit("open_failed")
		return

	var content := file.get_as_text()
	file.close()

	var json := JSON.new()
	var err := json.parse(content)
	if err != OK:
		push_error("[SaveManager] save corrupto en línea %d: %s" % [json.get_error_line(), json.get_error_message()])
		data = _default_save()
		save_failed.emit("parse_error")
		save_to_disk()  # overwrite del corrupto
		save_loaded.emit()
		return

	var loaded: Dictionary = json.data

	# Migrar versiones antiguas si aplica
	if loaded.get("version", "0.0.0") != SAVE_VERSION:
		loaded = _migrate(loaded)

	# Mergear con defaults para asegurar que todas las keys existan
	data = _default_save()
	for key in loaded:
		data[key] = loaded[key]

	var lvl: int = data.get("highest_level_completed", 0)
	var n_creatures: int = data.get("creatures_rescued", []).size()
	print("[SaveManager] save cargado: nivel max %d, criaturas %d" % [lvl, n_creatures])
	save_loaded.emit()


## Escribe el save actual al disco como JSON.
func save_to_disk() -> void:
	data["last_open_timestamp"] = Time.get_unix_time_from_system()
	var json_str := JSON.stringify(data, "  ")
	var file := FileAccess.open(SAVE_PATH, FileAccess.WRITE)
	if not file:
		push_error("[SaveManager] no se pudo escribir save file")
		save_failed.emit("write_failed")
		return
	file.store_string(json_str)
	file.close()


## Resetea el save al estado default (para debug / testing).
func reset_save() -> void:
	data = _default_save()
	save_to_disk()
	print("[SaveManager] save reseteado a default")
	save_loaded.emit()


# ── API de conveniencia para el resto del juego ───────────────────────────


## Llamar cuando el jugador completa un nivel exitosamente.
## Actualiza highest_level_completed, best_scores, creatures_rescued y stats.
func record_level_completion(level_id: int, score: int, creature_id: String = "") -> void:
	var current_highest: int = data.get("highest_level_completed", 0)
	if level_id > current_highest:
		data["highest_level_completed"] = level_id

	data["last_played_level"] = level_id
	data["total_levels_played"] = data.get("total_levels_played", 0) + 1
	data["total_score"] = data.get("total_score", 0) + score

	# Best score por nivel (almacenado con key string para JSON)
	var key := str(level_id)
	var best_scores: Dictionary = data.get("best_scores", {})
	if not best_scores.has(key) or best_scores[key] < score:
		best_scores[key] = score
		data["best_scores"] = best_scores

	# Criatura rescatada (si aplica)
	if creature_id != "":
		var rescued: Array = data.get("creatures_rescued", [])
		if not creature_id in rescued:
			rescued.append(creature_id)
			data["creatures_rescued"] = rescued

	save_to_disk()
	print("[SaveManager] nivel %d completado, score %d, criatura: %s" % [level_id, score, creature_id])


## Devuelve el mejor score guardado para un nivel dado, o 0 si nunca se completó.
func get_best_score(level_id: int) -> int:
	var key := str(level_id)
	return data.get("best_scores", {}).get(key, 0)


## ¿El nivel ya fue completado al menos una vez?
func is_level_completed(level_id: int) -> bool:
	return level_id <= data.get("highest_level_completed", 0)


## Llamar cuando el jugador empieza/carga un nivel (para tracking).
func record_level_started(level_id: int) -> void:
	data["last_played_level"] = level_id
	# No save inmediato — se persistirá al ganar/perder/cerrar


# ── Default y migración ───────────────────────────────────────────────────


func _default_save() -> Dictionary:
	var os_locale: String = OS.get_locale_language()
	var supported_locales: Array[String] = ["es", "en", "it", "fr", "de", "pt"]
	if not os_locale in supported_locales:
		os_locale = "en"

	return {
		"version": SAVE_VERSION,
		"player_id": _generate_player_id(),
		"username": "",
		"first_open_timestamp": Time.get_unix_time_from_system(),
		"last_open_timestamp": Time.get_unix_time_from_system(),
		"highest_level_completed": 0,  # 0 = ningún nivel completado
		"last_played_level": 1,
		"best_scores": {},
		"creatures_rescued": [],
		"total_score": 0,
		"total_shots_fired": 0,
		"total_levels_played": 0,
		"currencies": {"coins": 0, "gems": 50},  # arranque con 50 gemas (welcome bonus)
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
			"language": os_locale,
			"music_volume": 0.7,
			"ui_volume": 0.8,
			"pop_volume": 1.0,
			"vibration_enabled": true,
			"theme": "auto",
			"notifications_enabled": true
		},
		"iap_history": [],
		"tutorial_completed": false
	}


func _migrate(old_data: Dictionary) -> Dictionary:
	# Futuras versiones agregar lógica de migración acá
	print("[SaveManager] migrating from %s to %s" % [old_data.get("version", "?"), SAVE_VERSION])
	var migrated := _default_save()
	for key in old_data:
		if migrated.has(key):
			migrated[key] = old_data[key]
	migrated["version"] = SAVE_VERSION
	return migrated


func _generate_player_id() -> String:
	return "player_%d_%d" % [Time.get_unix_time_from_system(), randi() % 100000]
