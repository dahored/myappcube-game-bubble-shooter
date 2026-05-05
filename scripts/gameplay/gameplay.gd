extends Node2D
## Gameplay scene root. Coordina Grid, Canon y HUD según el nivel cargado.
## Lee level data desde LevelManager (JSON), configura todo, y maneja win/lose.

@onready var grid: Grid = $Grid
@onready var canon: Canon = $Canon
@onready var score_label: Label = $HUD/ScoreLabel
@onready var shots_label: Label = $HUD/ShotsLabel
@onready var objective_label: Label = $HUD/ObjectiveLabel
@onready var level_label: Label = $HUD/LevelLabel
@onready var lives_label: Label = $HUD/LivesLabel
@onready var currency_label: Label = $HUD/CurrencyLabel
@onready var prev_button: Button = $HUD/DebugButtons/PrevButton
@onready var next_button: Button = $HUD/DebugButtons/NextButton
@onready var reset_button: Button = $HUD/DebugButtons/ResetButton
@onready var end_screen: Control = $HUD/EndScreen
@onready var end_title: Label = $HUD/EndScreen/Panel/VBox/TitleLabel
@onready var end_subtitle: Label = $HUD/EndScreen/Panel/VBox/SubtitleLabel
@onready var retry_button: Button = $HUD/EndScreen/Panel/VBox/RetryButton
@onready var sanctuary_button: Button = $HUD/EndScreen/Panel/VBox/SanctuaryButton
@onready var exit_button: Button = $HUD/ExitButton

# Fila a partir de la cual cualquier burbuja del grid dispara game-over.
# screen y ≈ 1332 (80 grid offset + 14 * 83px row + 88px bubble half). Ajustar si hace falta.
const DEATH_ROW := 14

var level_data: Dictionary = {}
var shots_remaining: int = 0
var level_ended: bool = false
var level_won: bool = false


func _ready() -> void:
	grid.score_changed.connect(_on_score_changed)
	grid.state_settled.connect(_on_state_settled)
	canon.shot_fired.connect(_on_shot_fired)
	retry_button.pressed.connect(_on_retry_pressed)
	sanctuary_button.pressed.connect(_on_sanctuary_pressed)
	exit_button.pressed.connect(_on_sanctuary_pressed)
	prev_button.pressed.connect(_on_prev_pressed)
	next_button.pressed.connect(_on_next_pressed)
	reset_button.pressed.connect(_on_reset_pressed)
	EconomyManager.lives_changed.connect(_on_lives_changed)
	EconomyManager.coins_changed.connect(_on_currency_changed)
	EconomyManager.gems_changed.connect(_on_currency_changed)
	end_screen.visible = false

	exit_button.text = tr("ui.gameplay.exit_sanctuary")
	AudioManager.play_music("gameplay")
	_load_current_level()


func _load_current_level() -> void:
	var level_id: int = GameManager.current_level_id
	SaveManager.record_level_started(level_id)
	level_data = LevelManager.load_level(level_id)
	if level_data.is_empty():
		grid.setup_from_level({})
		shots_remaining = 25
		_set_objective_text(tr("ui.gameplay.objective.clear_all"))
		level_label.text = tr("ui.gameplay.level_label").format({"id": level_id, "name": "?"})
		_update_hud()
		return

	grid.setup_from_level(level_data)

	var color_strs: Array = level_data.get("available_colors", [])
	var types: Array[int] = []
	for cs in color_strs:
		if Bubble.COLOR_STR_TO_TYPE.has(cs):
			types.append(Bubble.COLOR_STR_TO_TYPE[cs])
	canon.configure_playable_types(types)

	shots_remaining = level_data.get("max_shots", 25)
	_set_objective_text(_objective_to_text(level_data.objective))
	var best_score: int = SaveManager.get_best_score(level_id)
	if best_score > 0:
		level_label.text = tr("ui.gameplay.level_label_with_best").format({
			"id": level_data.get("id", 0),
			"name": level_data.get("name", ""),
			"best": best_score
		})
	else:
		level_label.text = tr("ui.gameplay.level_label").format({
			"id": level_data.get("id", 0),
			"name": level_data.get("name", "")
		})
	_update_hud()


func _objective_to_text(obj: Dictionary) -> String:
	var t: String = obj.get("type", "")
	match t:
		"clear_all":
			return tr("ui.gameplay.objective.clear_all")
		"rescue":
			var creature: String = obj.get("creature_id", "")
			return tr("ui.gameplay.objective.rescue").format({"creature": creature.capitalize()})
		_:
			return t


func _set_objective_text(s: String) -> void:
	objective_label.text = s


func _on_score_changed(new_score: int) -> void:
	score_label.text = "%s: %d" % [tr("ui.gameplay.score"), new_score]


func _on_lives_changed(_new_lives: int) -> void:
	_update_lives_label()


func _on_currency_changed(_amount: int) -> void:
	_update_currency_label()


func _on_shot_fired() -> void:
	shots_remaining -= 1
	_update_hud()
	AudioManager.play_sfx("shoot", AudioManager.AudioCategory.UI_FX)
	if shots_remaining <= 0:
		canon.level_active = false


func _on_state_settled() -> void:
	if level_ended:
		return
	if _is_objective_complete():
		_show_end_screen(true)
	elif grid.has_bubbles_past_row(DEATH_ROW) or shots_remaining <= 0:
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
	shots_label.text = "%s: %d" % [tr("ui.gameplay.shots_remaining"), shots_remaining]
	_update_lives_label()
	_update_currency_label()


func _update_lives_label() -> void:
	var n := EconomyManager.get_lives()
	var hearts := "❤️".repeat(n) + "🖤".repeat(EconomyManager.MAX_LIVES - n)
	var secs := EconomyManager.seconds_until_next_life()
	if secs > 0 and n < EconomyManager.MAX_LIVES:
		lives_label.text = "%s  %02d:%02d" % [hearts, secs / 60, secs % 60]
	else:
		lives_label.text = hearts


func _update_currency_label() -> void:
	currency_label.text = "🪙 %d   💎 %d" % [EconomyManager.get_coins(), EconomyManager.get_gems()]


func _show_end_screen(victory: bool) -> void:
	level_ended = true
	level_won = victory
	canon.level_active = false
	var has_next: bool = GameManager.current_level_id < LevelManager.get_total_levels()
	var level_id: int = GameManager.current_level_id

	if victory:
		var creature_id := ""
		if level_data.objective.get("type", "") == "rescue":
			creature_id = level_data.objective.get("creature_id", "")
		var is_first := not SaveManager.is_level_completed(level_id)
		var chapter: int = level_data.get("chapter", 1)
		var prev_best: int = SaveManager.get_best_score(level_id)

		EconomyManager.award_level_completion(chapter, is_first)
		SaveManager.record_level_completion(level_id, grid.score, creature_id)
		AudioManager.play_sfx("victory", AudioManager.AudioCategory.UI_FX)

		end_title.text = tr("ui.victory.title")
		if grid.score > prev_best:
			end_subtitle.text = tr("ui.victory.subtitle.new_record").format({"score": grid.score, "best": prev_best})
		else:
			end_subtitle.text = tr("ui.victory.subtitle.score").format({"score": grid.score, "best": prev_best})
		retry_button.text = tr("ui.button.next_level") if has_next else tr("ui.button.retry")
	else:
		EconomyManager.consume_life()
		AudioManager.play_sfx("defeat", AudioManager.AudioCategory.UI_FX)
		end_title.text = tr("ui.gameover.title")
		end_subtitle.text = tr("ui.gameover.subtitle").format({"score": grid.score})
		retry_button.text = tr("ui.button.retry")

	sanctuary_button.text = tr("ui.gameplay.exit_sanctuary")
	end_screen.visible = true


func _on_sanctuary_pressed() -> void:
	AudioManager.play_sfx("button", AudioManager.AudioCategory.UI_FX)
	get_tree().change_scene_to_file("res://scenes/sanctuary/sanctuary.tscn")


func _on_retry_pressed() -> void:
	AudioManager.play_sfx("button", AudioManager.AudioCategory.UI_FX)
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


func _on_reset_pressed() -> void:
	SaveManager.reset_save()
	GameManager.current_level_id = 1
	get_tree().reload_current_scene()
