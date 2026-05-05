extends Control
class_name Sanctuary
## Pantalla principal del juego (main menu). Hub desde donde se accede a todo.
## GDD §5 (Meta-juego Santuario) + GDD §10.3 Pantalla 4.
## UI construida con VBoxContainer para layout fiable en Godot 4.

const CHAPTER_NAMES := [
	"", "La Cala Apagada", "Jardín de Anémonas",
	"Bosque de Algas", "Cueva de Cristales",
	"Profundidades de Coral", "Ciudad de las Perlas",
]

var _coins_label: Label
var _gems_label: Label
var _lives_label: Label
var _streak_label: Label
var _creatures_label: Label
var _lives_timer: Timer


func _ready() -> void:
	_collect_passive_income()
	_build_ui()
	_update_hud()
	_start_hud_timer()
	EconomyManager.coins_changed.connect(func(_v: int): _update_hud())
	EconomyManager.gems_changed.connect(func(_v: int): _update_hud())
	EconomyManager.lives_changed.connect(func(_v: int): _update_hud())
	AudioManager.play_music("gameplay")
	GameManager.change_screen("sanctuary")


# ── Construcción de UI ──────────────────────────────────────────────────────


func _build_ui() -> void:
	# Fondo decorativo de arrecife (detrás del layout)
	var reef_bg := ColorRect.new()
	reef_bg.name = "ReefBg"
	reef_bg.color = Color(0.659, 0.878, 0.835, 0.35)
	reef_bg.mouse_filter = Control.MOUSE_FILTER_IGNORE
	reef_bg.set_anchors_preset(Control.PRESET_FULL_RECT)
	add_child(reef_bg)

	# Layout vertical principal — ocupa toda la pantalla
	var vbox := VBoxContainer.new()
	vbox.name = "MainVBox"
	vbox.set_anchors_preset(Control.PRESET_FULL_RECT)
	vbox.add_theme_constant_override("separation", 0)
	add_child(vbox)

	_build_top_bar(vbox)
	_build_currency_row(vbox)
	_build_creatures_info(vbox)
	_build_reef_content(vbox)   # se expande para llenar el espacio
	_build_streak_label(vbox)
	_build_play_button(vbox)
	_add_spacer(vbox, 16)
	_build_bottom_bar(vbox)
	_add_spacer(vbox, 32)


func _build_top_bar(parent: Node) -> void:
	var row := HBoxContainer.new()
	row.name = "TopBar"
	row.custom_minimum_size = Vector2(0, 88)
	row.add_theme_constant_override("separation", 8)
	parent.add_child(row)

	_add_hpad(row, 8)
	row.add_child(_make_icon_button("⚙️", _on_settings_pressed))

	var spacer := Control.new()
	spacer.size_flags_horizontal = Control.SIZE_EXPAND_FILL
	row.add_child(spacer)

	row.add_child(_make_icon_button("👤", _on_profile_pressed))
	_add_hpad(row, 8)


func _build_currency_row(parent: Node) -> void:
	var row := HBoxContainer.new()
	row.name = "CurrencyRow"
	row.custom_minimum_size = Vector2(0, 52)
	row.alignment = BoxContainer.ALIGNMENT_CENTER
	row.add_theme_constant_override("separation", 24)
	parent.add_child(row)

	_coins_label = _make_currency_label(Color(0.6, 0.5, 0.1))
	row.add_child(_coins_label)

	_gems_label = _make_currency_label(Color(0.45, 0.35, 0.75))
	row.add_child(_gems_label)

	_lives_label = _make_currency_label(Color(0.85, 0.4, 0.4))
	row.add_child(_lives_label)


func _build_creatures_info(parent: Node) -> void:
	_creatures_label = Label.new()
	_creatures_label.name = "CreaturesInfo"
	_creatures_label.custom_minimum_size = Vector2(0, 36)
	_creatures_label.add_theme_font_size_override("font_size", 22)
	_creatures_label.add_theme_color_override("font_color", Color(0.35, 0.55, 0.45))
	_creatures_label.horizontal_alignment = HORIZONTAL_ALIGNMENT_CENTER
	parent.add_child(_creatures_label)


func _build_reef_content(parent: Node) -> void:
	var panel := Control.new()
	panel.name = "ReefPanel"
	panel.size_flags_vertical = Control.SIZE_EXPAND_FILL
	panel.mouse_filter = Control.MOUSE_FILTER_IGNORE
	parent.add_child(panel)

	var emoji := Label.new()
	emoji.name = "ReefEmojis"
	emoji.text = "🌊       🐠      🐡\n   🐙    🦐       🐚\n🐢      🦀     🐟"
	emoji.add_theme_font_size_override("font_size", 44)
	emoji.horizontal_alignment = HORIZONTAL_ALIGNMENT_CENTER
	emoji.vertical_alignment = VERTICAL_ALIGNMENT_CENTER
	emoji.mouse_filter = Control.MOUSE_FILTER_IGNORE
	emoji.set_anchors_preset(Control.PRESET_FULL_RECT)
	panel.add_child(emoji)


func _build_streak_label(parent: Node) -> void:
	_streak_label = Label.new()
	_streak_label.name = "StreakLabel"
	_streak_label.custom_minimum_size = Vector2(0, 44)
	_streak_label.add_theme_font_size_override("font_size", 28)
	_streak_label.add_theme_color_override("font_color", Color(0.9, 0.5, 0.1))
	_streak_label.horizontal_alignment = HORIZONTAL_ALIGNMENT_LEFT
	parent.add_child(_streak_label)


func _build_play_button(parent: Node) -> void:
	var margin := MarginContainer.new()
	margin.add_theme_constant_override("margin_left", 80)
	margin.add_theme_constant_override("margin_right", 80)
	parent.add_child(margin)

	var btn := Button.new()
	btn.name = "PlayButton"
	btn.text = tr("ui.sanctuary.play")
	btn.custom_minimum_size = Vector2(0, 90)
	btn.size_flags_horizontal = Control.SIZE_EXPAND_FILL
	btn.add_theme_font_size_override("font_size", 52)

	var style := StyleBoxFlat.new()
	style.bg_color = Color(0.957, 0.651, 0.627)
	style.set_corner_radius_all(32)
	style.content_margin_top = 12.0
	style.content_margin_bottom = 12.0
	btn.add_theme_stylebox_override("normal", style)

	var style_hover := style.duplicate() as StyleBoxFlat
	style_hover.bg_color = Color(0.847, 0.482, 0.482)
	btn.add_theme_stylebox_override("hover", style_hover)
	btn.add_theme_stylebox_override("pressed", style_hover)
	btn.add_theme_color_override("font_color", Color.WHITE)
	btn.pressed.connect(_on_play_pressed)
	margin.add_child(btn)


func _build_bottom_bar(parent: Node) -> void:
	var row := HBoxContainer.new()
	row.name = "BottomBar"
	row.custom_minimum_size = Vector2(0, 90)
	row.alignment = BoxContainer.ALIGNMENT_CENTER
	row.add_theme_constant_override("separation", 8)
	parent.add_child(row)

	row.add_child(_make_shortcut_button("🛒 " + tr("ui.sanctuary.shop"), _on_shop_pressed))
	row.add_child(_make_shortcut_button("🎫 " + tr("ui.sanctuary.battle_pass"), _on_battle_pass_pressed))
	row.add_child(_make_shortcut_button("📅 " + tr("ui.sanctuary.daily"), _on_daily_pressed))


# ── Helpers de UI ───────────────────────────────────────────────────────────


func _add_spacer(parent: Node, height: int) -> void:
	var s := Control.new()
	s.custom_minimum_size = Vector2(0, height)
	parent.add_child(s)


func _add_hpad(parent: Node, width: int) -> void:
	var p := Control.new()
	p.custom_minimum_size = Vector2(width, 0)
	parent.add_child(p)


func _make_icon_button(emoji: String, callback: Callable) -> Button:
	var btn := Button.new()
	btn.text = emoji
	btn.custom_minimum_size = Vector2(80, 72)
	btn.add_theme_font_size_override("font_size", 32)
	btn.pressed.connect(callback)
	return btn


func _make_currency_label(color: Color) -> Label:
	var lbl := Label.new()
	lbl.add_theme_font_size_override("font_size", 30)
	lbl.add_theme_color_override("font_color", color)
	return lbl


func _make_shortcut_button(text_str: String, callback: Callable) -> Button:
	var btn := Button.new()
	btn.text = text_str
	btn.custom_minimum_size = Vector2(240, 80)
	btn.add_theme_font_size_override("font_size", 24)
	btn.size_flags_horizontal = Control.SIZE_EXPAND_FILL
	btn.pressed.connect(callback)
	return btn


# ── Lógica del santuario ────────────────────────────────────────────────────


func _collect_passive_income() -> void:
	var rescued: Array = SaveManager.data.get("creatures_rescued", [])
	if rescued.is_empty():
		return
	var now := Time.get_unix_time_from_system()
	var last: float = SaveManager.data.get("last_open_timestamp", now)
	var hours := minf((now - last) / 3600.0, 8.0)
	var earned := int(hours * rescued.size() * 5)
	if earned > 0:
		EconomyManager.add_coins(earned, "passive_sanctuary")
		print("[Sanctuary] income pasivo: +%d monedas (%d criaturas × %.1fh)" % [earned, rescued.size(), hours])


func _update_hud() -> void:
	_coins_label.text = "🪙 %d" % EconomyManager.get_coins()
	_gems_label.text = "  💎 %d" % EconomyManager.get_gems()
	var n := EconomyManager.get_lives()
	var secs := EconomyManager.seconds_until_next_life()
	if secs > 0 and n < EconomyManager.MAX_LIVES:
		_lives_label.text = "  ❤️ %d  %02d:%02d" % [n, secs / 60, secs % 60]
	else:
		_lives_label.text = "  ❤️ %d" % n

	var rescued: Array = SaveManager.data.get("creatures_rescued", [])
	_creatures_label.text = tr("ui.sanctuary.creatures_rescued").format({"n": rescued.size()})

	var days: int = SaveManager.data.get("streak_days", 0)
	_streak_label.text = tr("ui.sanctuary.streak").format({"n": days}) if days > 1 else ""


func _start_hud_timer() -> void:
	_lives_timer = Timer.new()
	_lives_timer.wait_time = 1.0
	_lives_timer.autostart = true
	_lives_timer.timeout.connect(func(): _update_hud())
	add_child(_lives_timer)


# ── Navegación ──────────────────────────────────────────────────────────────


func _on_play_pressed() -> void:
	AudioManager.play_sfx("button", AudioManager.AudioCategory.UI_FX)
	get_tree().change_scene_to_file("res://scenes/ui/level_select.tscn")


func _on_settings_pressed() -> void:
	AudioManager.play_sfx("button", AudioManager.AudioCategory.UI_FX)
	push_warning("[Sanctuary] Settings screen no implementado aún (issue #8)")


func _on_profile_pressed() -> void:
	AudioManager.play_sfx("button", AudioManager.AudioCategory.UI_FX)
	push_warning("[Sanctuary] Profile screen no implementado aún")


func _on_shop_pressed() -> void:
	AudioManager.play_sfx("button", AudioManager.AudioCategory.UI_FX)
	push_warning("[Sanctuary] Shop screen no implementado aún")


func _on_battle_pass_pressed() -> void:
	AudioManager.play_sfx("button", AudioManager.AudioCategory.UI_FX)
	push_warning("[Sanctuary] Battle Pass screen no implementado aún")


func _on_daily_pressed() -> void:
	AudioManager.play_sfx("button", AudioManager.AudioCategory.UI_FX)
	push_warning("[Sanctuary] Daily reward no implementado aún (issue #8)")
