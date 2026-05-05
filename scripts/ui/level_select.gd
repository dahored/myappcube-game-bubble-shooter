extends Control
class_name LevelSelect
## Mapa de niveles tipo Candy Crush. Muestra todos los niveles agrupados
## por capítulo con estado visual: completado / actual / bloqueado.
## GDD §10.3 Pantalla 12 + Wireframes §Pantalla 12.

const CHAPTER_NAMES := [
	"", "La Cala Apagada", "Jardín de Anémonas",
	"Bosque de Algas", "Cueva de Cristales",
	"Profundidades de Coral", "Ciudad de las Perlas",
]
const LEVELS_PER_CHAPTER := 15
const NODES_PER_ROW := 3

const COLOR_COMPLETED := Color(0.455, 0.737, 0.451)
const COLOR_CURRENT   := Color(0.957, 0.651, 0.627)
const COLOR_LOCKED    := Color(0.75, 0.75, 0.75)

var _highest_completed: int = 0
var _total_levels: int = 0

var _scroll: ScrollContainer
var _map_container: VBoxContainer
var _current_level_btn: Control = null

var _tooltip: Label


func _ready() -> void:
	_highest_completed = SaveManager.data.get("highest_level_completed", 0)
	_total_levels = LevelManager.get_total_levels()

	_build_ui()
	_build_level_map()
	_scroll_to_current()


# ── Construcción de UI estática ─────────────────────────────────────────────


func _build_ui() -> void:
	# Layout principal ocupa toda la pantalla
	var vbox := VBoxContainer.new()
	vbox.name = "MainVBox"
	vbox.set_anchors_preset(Control.PRESET_FULL_RECT)
	vbox.add_theme_constant_override("separation", 0)
	add_child(vbox)

	_build_header(vbox)

	# Línea separadora bajo el header
	var sep := ColorRect.new()
	sep.color = Color(0.659, 0.878, 0.835, 0.6)
	sep.custom_minimum_size = Vector2(0, 3)
	vbox.add_child(sep)

	_build_scroll_area(vbox)
	_build_tooltip()


func _build_header(parent: Node) -> void:
	var header := HBoxContainer.new()
	header.name = "Header"
	header.custom_minimum_size = Vector2(0, 88)
	header.add_theme_constant_override("separation", 8)
	parent.add_child(header)

	var back_btn := Button.new()
	back_btn.text = "←"
	back_btn.custom_minimum_size = Vector2(80, 72)
	back_btn.add_theme_font_size_override("font_size", 36)
	back_btn.pressed.connect(_on_back_pressed)
	header.add_child(back_btn)

	var title := Label.new()
	title.text = tr("ui.level_select.title")
	title.add_theme_font_size_override("font_size", 30)
	title.add_theme_color_override("font_color", Color(0.2, 0.25, 0.3))
	title.size_flags_horizontal = Control.SIZE_EXPAND_FILL
	title.horizontal_alignment = HORIZONTAL_ALIGNMENT_CENTER
	header.add_child(title)

	var currency_row := HBoxContainer.new()
	currency_row.add_theme_constant_override("separation", 16)
	header.add_child(currency_row)

	var coins_lbl := Label.new()
	coins_lbl.text = "🪙 %d" % EconomyManager.get_coins()
	coins_lbl.add_theme_font_size_override("font_size", 24)
	coins_lbl.add_theme_color_override("font_color", Color(0.6, 0.5, 0.1))
	currency_row.add_child(coins_lbl)

	var gems_lbl := Label.new()
	gems_lbl.text = "  💎 %d" % EconomyManager.get_gems()
	gems_lbl.add_theme_font_size_override("font_size", 24)
	gems_lbl.add_theme_color_override("font_color", Color(0.45, 0.35, 0.75))
	currency_row.add_child(gems_lbl)

	var lives_lbl := Label.new()
	lives_lbl.text = "  ❤️ %d" % EconomyManager.get_lives()
	lives_lbl.add_theme_font_size_override("font_size", 24)
	lives_lbl.add_theme_color_override("font_color", Color(0.85, 0.4, 0.4))
	currency_row.add_child(lives_lbl)

	var pad_r := Control.new()
	pad_r.custom_minimum_size = Vector2(8, 0)
	header.add_child(pad_r)


func _build_scroll_area(parent: Node) -> void:
	_scroll = ScrollContainer.new()
	_scroll.name = "ScrollContainer"
	_scroll.size_flags_vertical = Control.SIZE_EXPAND_FILL
	_scroll.horizontal_scroll_mode = ScrollContainer.SCROLL_MODE_DISABLED
	parent.add_child(_scroll)

	_map_container = VBoxContainer.new()
	_map_container.name = "MapContainer"
	_map_container.size_flags_horizontal = Control.SIZE_EXPAND_FILL
	_map_container.add_theme_constant_override("separation", 8)
	_scroll.add_child(_map_container)


func _build_tooltip() -> void:
	_tooltip = Label.new()
	_tooltip.name = "Tooltip"
	_tooltip.visible = false
	_tooltip.add_theme_font_size_override("font_size", 22)
	_tooltip.add_theme_color_override("font_color", Color.WHITE)
	_tooltip.set_anchor_and_offset(SIDE_LEFT, 0.5, -200.0)
	_tooltip.set_anchor_and_offset(SIDE_RIGHT, 0.5, 200.0)
	_tooltip.set_anchor_and_offset(SIDE_BOTTOM, 1.0, -80.0)
	_tooltip.set_anchor_and_offset(SIDE_TOP, 1.0, -120.0)
	_tooltip.horizontal_alignment = HORIZONTAL_ALIGNMENT_CENTER
	_tooltip.vertical_alignment = VERTICAL_ALIGNMENT_CENTER

	var tooltip_bg := ColorRect.new()
	tooltip_bg.color = Color(0.1, 0.1, 0.1, 0.85)
	tooltip_bg.mouse_filter = Control.MOUSE_FILTER_IGNORE
	tooltip_bg.set_anchor_and_offset(SIDE_LEFT, 0.5, -220.0)
	tooltip_bg.set_anchor_and_offset(SIDE_RIGHT, 0.5, 220.0)
	tooltip_bg.set_anchor_and_offset(SIDE_BOTTOM, 1.0, -72.0)
	tooltip_bg.set_anchor_and_offset(SIDE_TOP, 1.0, -128.0)
	add_child(tooltip_bg)
	add_child(_tooltip)


# ── Construcción dinámica del mapa ──────────────────────────────────────────


func _build_level_map() -> void:
	var current_chapter := -1
	var current_row: HBoxContainer = null
	var items_in_row := 0
	var row_index := 0

	for level_id in range(1, _total_levels + 1):
		var chapter := _chapter_of(level_id)

		if chapter != current_chapter:
			current_chapter = chapter
			_add_chapter_header(chapter)
			current_row = null
			items_in_row = 0
			row_index = 0

		if items_in_row == 0:
			current_row = _make_row(row_index)
			_map_container.add_child(current_row)

		var node := _make_level_node(level_id)
		current_row.add_child(node)
		items_in_row += 1

		if items_in_row >= NODES_PER_ROW:
			items_in_row = 0
			row_index += 1

	var bottom_spacer := Control.new()
	bottom_spacer.custom_minimum_size = Vector2(0, 64)
	_map_container.add_child(bottom_spacer)


func _add_chapter_header(chapter: int) -> void:
	var spacer := Control.new()
	spacer.custom_minimum_size = Vector2(0, 32)
	_map_container.add_child(spacer)

	var separator := ColorRect.new()
	separator.color = Color(0.659, 0.878, 0.835, 0.5)
	separator.custom_minimum_size = Vector2(0, 4)
	separator.size_flags_horizontal = Control.SIZE_EXPAND_FILL
	_map_container.add_child(separator)

	var name_idx: int = mini(chapter, CHAPTER_NAMES.size() - 1)
	var lbl := Label.new()
	lbl.text = tr("ui.level_select.chapter").format({"n": chapter, "name": CHAPTER_NAMES[name_idx]})
	lbl.horizontal_alignment = HORIZONTAL_ALIGNMENT_CENTER
	lbl.add_theme_font_size_override("font_size", 22)
	lbl.add_theme_color_override("font_color", Color(0.35, 0.4, 0.5))
	lbl.custom_minimum_size = Vector2(0, 40)
	_map_container.add_child(lbl)


func _make_row(row_index: int) -> HBoxContainer:
	var row := HBoxContainer.new()
	row.alignment = BoxContainer.ALIGNMENT_CENTER
	row.add_theme_constant_override("separation", 24)
	row.size_flags_horizontal = Control.SIZE_EXPAND_FILL
	# Zigzag: filas impares desplazadas a la derecha
	var margin := Control.new()
	margin.custom_minimum_size = Vector2(96 if row_index % 2 == 1 else 0, 0)
	row.add_child(margin)
	row.move_child(margin, 0)
	return row


func _make_level_node(level_id: int) -> Control:
	var is_completed := level_id <= _highest_completed
	var is_current   := level_id == _highest_completed + 1
	var is_locked    := level_id > _highest_completed + 1

	var best_score: int = SaveManager.get_best_score(level_id)
	var level_data := LevelManager.load_level(level_id)
	var creature_id := ""
	if not level_data.is_empty():
		creature_id = level_data.get("objective", {}).get("creature_id", "")

	var vbox := VBoxContainer.new()
	vbox.alignment = BoxContainer.ALIGNMENT_CENTER
	vbox.add_theme_constant_override("separation", 4)
	vbox.custom_minimum_size = Vector2(180, 200)

	var btn := Button.new()
	btn.custom_minimum_size = Vector2(140, 140)

	var color: Color
	if is_completed:
		color = COLOR_COMPLETED
	elif is_current:
		color = COLOR_CURRENT
	else:
		color = COLOR_LOCKED

	var style := StyleBoxFlat.new()
	style.bg_color = color
	style.set_corner_radius_all(70)
	style.border_width_top    = 4
	style.border_width_bottom = 4
	style.border_width_left   = 4
	style.border_width_right  = 4
	style.border_color = color.lightened(0.2)
	btn.add_theme_stylebox_override("normal", style)
	btn.add_theme_stylebox_override("hover", _make_hover_style(style))
	btn.add_theme_stylebox_override("pressed", _make_hover_style(style))

	if is_completed:
		btn.text = "✓\n%d" % level_id
	elif is_locked:
		btn.text = "🔒\n%d" % level_id
	else:
		btn.text = "%d" % level_id

	btn.add_theme_font_size_override("font_size", 28)
	btn.add_theme_color_override("font_color", Color.WHITE)

	if is_completed or is_current:
		btn.pressed.connect(_on_level_pressed.bind(level_id))
		if is_current:
			_current_level_btn = vbox
	else:
		btn.pressed.connect(_on_locked_pressed)

	vbox.add_child(btn)

	var info_lbl := Label.new()
	info_lbl.horizontal_alignment = HORIZONTAL_ALIGNMENT_CENTER
	info_lbl.add_theme_font_size_override("font_size", 18)
	info_lbl.add_theme_color_override("font_color", Color(0.3, 0.35, 0.4))

	if is_completed and best_score > 0:
		info_lbl.text = tr("ui.level_select.best").format({"score": best_score})
	elif is_completed and not creature_id.is_empty():
		info_lbl.text = "🐠 %s" % creature_id
	elif is_current:
		info_lbl.text = "← aquí"
	else:
		info_lbl.text = ""

	vbox.add_child(info_lbl)
	return vbox


func _make_hover_style(base: StyleBoxFlat) -> StyleBoxFlat:
	var s := base.duplicate() as StyleBoxFlat
	s.bg_color = base.bg_color.darkened(0.1)
	return s


# ── Lógica de navegación ────────────────────────────────────────────────────


func _scroll_to_current() -> void:
	if _current_level_btn == null:
		return
	await get_tree().process_frame
	await get_tree().process_frame
	var target_y := _current_level_btn.global_position.y - get_viewport_rect().size.y * 0.4
	_scroll.scroll_vertical = int(max(0.0, target_y))


func _chapter_of(level_id: int) -> int:
	return int((level_id - 1) / LEVELS_PER_CHAPTER) + 1


func _on_level_pressed(level_id: int) -> void:
	AudioManager.play_sfx("button", AudioManager.AudioCategory.UI_FX)
	GameManager.current_level_id = level_id
	get_tree().change_scene_to_file("res://scenes/gameplay/gameplay.tscn")


func _on_locked_pressed() -> void:
	AudioManager.play_sfx("button", AudioManager.AudioCategory.UI_FX)
	_tooltip.text = tr("ui.level_select.locked")
	_tooltip.visible = true
	get_tree().create_timer(2.0).timeout.connect(func(): _tooltip.visible = false)


func _on_back_pressed() -> void:
	AudioManager.play_sfx("button", AudioManager.AudioCategory.UI_FX)
	get_tree().change_scene_to_file("res://scenes/sanctuary/sanctuary.tscn")
