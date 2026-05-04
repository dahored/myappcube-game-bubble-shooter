extends Node2D
## Gameplay scene root. Coordina Grid, Canon y HUD según el nivel cargado.
## Lee level data desde LevelManager (JSON), configura todo, y maneja win/lose.

@onready var grid: Grid = $Grid
@onready var canon: Canon = $Canon
@onready var score_label: Label = $HUD/ScoreLabel
@onready var shots_label: Label = $HUD/ShotsLabel
@onready var objective_label: Label = $HUD/ObjectiveLabel
@onready var level_label: Label = $HUD/LevelLabel
@onready var prev_button: Button = $HUD/DebugButtons/PrevButton
@onready var next_button: Button = $HUD/DebugButtons/NextButton
@onready var end_screen: Control = $HUD/EndScreen
@onready var end_title: Label = $HUD/EndScreen/Panel/VBox/TitleLabel
@onready var end_subtitle: Label = $HUD/EndScreen/Panel/VBox/SubtitleLabel
@onready var retry_button: Button = $HUD/EndScreen/Panel/VBox/RetryButton

# Mapeo string → Bubble.Type para available_colors del nivel
const COLOR_STR_TO_TYPE := {
	"red": Bubble.Type.RED,
	"blue": Bubble.Type.BLUE,
	"yellow": Bubble.Type.YELLOW,
	"green": Bubble.Type.GREEN,
	"purple": Bubble.Type.PURPLE,
	"orange": Bubble.Type.ORANGE,
	"rainbow": Bubble.Type.RAINBOW,
}

var level_data: Dictionary = {}
var shots_remaining: int = 0
var level_ended: bool = false
var level_won: bool = false


func _ready() -> void:
	# Cablear señales antes de cargar el nivel
	grid.score_changed.connect(_on_score_changed)
	grid.state_settled.connect(_on_state_settled)
	canon.shot_fired.connect(_on_shot_fired)
	retry_button.pressed.connect(_on_retry_pressed)
	prev_button.pressed.connect(_on_prev_pressed)
	next_button.pressed.connect(_on_next_pressed)
	end_screen.visible = false

	_load_current_level()


func _load_current_level() -> void:
	var level_id: int = GameManager.current_level_id
	level_data = LevelManager.load_level(level_id)
	if level_data.is_empty():
		# Fallback: setup random grid + objective default
		grid.setup_from_level({})
		shots_remaining = 25
		_set_objective_text("Objetivo: limpiar todas las burbujas (fallback)")
		level_label.text = "Nivel %d (fallback)" % level_id
		_update_hud()
		return

	# Configurar grid con bubbles + creature
	grid.setup_from_level(level_data)

	# Configurar cañón con los colores disponibles del nivel
	var color_strs: Array = level_data.get("available_colors", [])
	var types: Array[int] = []
	for cs in color_strs:
		if COLOR_STR_TO_TYPE.has(cs):
			types.append(COLOR_STR_TO_TYPE[cs])
	canon.configure_playable_types(types)

	# Setup de shots y HUD
	shots_remaining = level_data.get("max_shots", 25)
	_set_objective_text(_objective_to_text(level_data.objective))
	level_label.text = "Nivel %d — %s" % [level_data.get("id", 0), level_data.get("name", "")]
	_update_hud()


func _objective_to_text(obj: Dictionary) -> String:
	var t: String = obj.get("type", "")
	match t:
		"clear_all":
			return "Objetivo: limpiar todas las burbujas"
		"rescue":
			var creature: String = obj.get("creature_id", "criatura")
			return "Objetivo: rescatar a %s" % creature.capitalize()
		_:
			return "Objetivo: %s" % t


func _set_objective_text(s: String) -> void:
	objective_label.text = s


func _on_score_changed(new_score: int) -> void:
	score_label.text = "Score: %d" % new_score


func _on_shot_fired() -> void:
	shots_remaining -= 1
	_update_hud()
	if shots_remaining <= 0:
		canon.level_active = false


func _on_state_settled() -> void:
	if level_ended:
		return
	if _is_objective_complete():
		_show_end_screen(true)
	elif shots_remaining <= 0:
		_show_end_screen(false)


func _is_objective_complete() -> bool:
	if level_data.is_empty():
		return grid.bubbles.is_empty()
	var obj_type: String = level_data.objective.get("type", "")
	match obj_type:
		"clear_all":
			return grid.bubbles.is_empty()
		"rescue":
			var pos: Array = level_data.objective.get("creature_position", [-1, -1])
			var cell := Vector2i(pos[0], pos[1])
			return not grid.bubbles.has(cell)
		_:
			return grid.bubbles.is_empty()


func _update_hud() -> void:
	shots_label.text = "Disparos: %d" % shots_remaining


func _show_end_screen(victory: bool) -> void:
	level_ended = true
	level_won = victory
	canon.level_active = false
	var has_next: bool = GameManager.current_level_id < LevelManager.get_total_levels()
	if victory:
		end_title.text = "¡LO LOGRASTE!"
		end_subtitle.text = "Score: %d" % grid.score
		retry_button.text = "Siguiente nivel →" if has_next else "Reintentar"
	else:
		end_title.text = "SIN DISPAROS"
		end_subtitle.text = "Score: %d" % grid.score
		retry_button.text = "Reintentar"
	end_screen.visible = true


func _on_retry_pressed() -> void:
	# Si ganaste y hay siguiente nivel, avanzar. Si no, recargar el actual.
	if level_won and GameManager.current_level_id < LevelManager.get_total_levels():
		GameManager.current_level_id += 1
	get_tree().reload_current_scene()


func _on_prev_pressed() -> void:
	var prev: int = max(1, GameManager.current_level_id - 1)
	if prev == GameManager.current_level_id:
		return
	GameManager.current_level_id = prev
	get_tree().reload_current_scene()


func _on_next_pressed() -> void:
	var total: int = LevelManager.get_total_levels()
	var next_id: int = min(total, GameManager.current_level_id + 1)
	if next_id == GameManager.current_level_id:
		return
	GameManager.current_level_id = next_id
	get_tree().reload_current_scene()
