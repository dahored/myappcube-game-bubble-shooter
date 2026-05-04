extends Node
## LevelManager — Carga niveles desde archivos JSON externos.
## Formato JSON detallado en GDD sección 14.4. Pipeline: lee → parsea → valida → emite.

signal level_loaded(level_data: Dictionary)
signal level_load_failed(level_id: int, reason: String)

const LEVELS_PATH := "res://data/levels/"


func _ready() -> void:
	print("[LevelManager] inicializado, niveles disponibles: %d" % get_total_levels())


## Carga un nivel desde su ID. Devuelve Dictionary con los datos parseados, o {} si falla.
func load_level(level_id: int) -> Dictionary:
	var path := LEVELS_PATH + "%03d.json" % level_id

	if not FileAccess.file_exists(path):
		var msg := "archivo no existe: %s" % path
		push_error("[LevelManager] " + msg)
		level_load_failed.emit(level_id, "file_not_found")
		return {}

	var file := FileAccess.open(path, FileAccess.READ)
	if not file:
		level_load_failed.emit(level_id, "open_failed")
		return {}
	var content := file.get_as_text()
	file.close()

	var json := JSON.new()
	var err := json.parse(content)
	if err != OK:
		var msg := "JSON parse error en %s línea %d: %s" % [path, json.get_error_line(), json.get_error_message()]
		push_error("[LevelManager] " + msg)
		level_load_failed.emit(level_id, "parse_error")
		return {}

	var data: Dictionary = json.data
	if not _validate_level(data):
		level_load_failed.emit(level_id, "validation_failed")
		return {}

	print("[LevelManager] cargado nivel %d (%s)" % [data.get("id", 0), data.get("name", "?")])
	level_loaded.emit(data)
	return data


## Valida que el level data tenga los campos requeridos. Retorna true si OK.
func _validate_level(data: Dictionary) -> bool:
	var required: Array[String] = ["id", "max_shots", "objective", "bubbles", "available_colors"]
	for key in required:
		if not data.has(key):
			push_error("[LevelManager] falta campo requerido: %s" % key)
			return false
	if not data.objective is Dictionary or not data.objective.has("type"):
		push_error("[LevelManager] objective debe ser un dict con campo 'type'")
		return false
	if not data.bubbles is Array:
		push_error("[LevelManager] bubbles debe ser un array")
		return false
	return true


## Cuenta cuántos archivos .json hay en data/levels/. Útil para clamp de prev/next.
func get_total_levels() -> int:
	var dir := DirAccess.open(LEVELS_PATH)
	if not dir:
		return 0
	dir.list_dir_begin()
	var count := 0
	var file_name := dir.get_next()
	while file_name != "":
		if file_name.ends_with(".json") and not file_name.begins_with("."):
			count += 1
		file_name = dir.get_next()
	dir.list_dir_end()
	return count
