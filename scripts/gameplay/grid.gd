extends Node2D
class_name Grid
## Container del grid hexagonal. Carga el layout desde level_data (JSON parseado),
## recibe burbujas que aterrizan tras ser disparadas, detecta matches y maneja drops.

const BubbleScene := preload("res://scenes/gameplay/bubble.tscn")

const POINTS_PER_POPPED := 10
const POINTS_PER_DROPPED := 15
const DROP_DELAY := 0.15

# Mapa de string color → Bubble.Type enum
const COLOR_STR_TO_TYPE := {
	"red": Bubble.Type.RED,
	"blue": Bubble.Type.BLUE,
	"yellow": Bubble.Type.YELLOW,
	"green": Bubble.Type.GREEN,
	"purple": Bubble.Type.PURPLE,
	"orange": Bubble.Type.ORANGE,
	"rainbow": Bubble.Type.RAINBOW,
}

# Map de Vector2i (col, row) → instancia de Bubble
var bubbles: Dictionary = {}
var score: int = 0

signal score_changed(new_score: int)
signal state_settled


## Carga el grid inicial desde level_data parseado por LevelManager.
## Si el level_data está vacío, fallback a un grid random (por si falla el load).
func setup_from_level(level_data: Dictionary) -> void:
	_clear_grid()
	if level_data.is_empty():
		push_warning("[Grid] level_data vacío, usando grid random fallback")
		_spawn_random_fallback()
		return

	var bubbles_array: Array = level_data.get("bubbles", [])
	var creature_cell := Vector2i(-1, -1)
	if level_data.objective.get("type", "") == "rescue":
		var pos: Array = level_data.objective.get("creature_position", [-1, -1])
		creature_cell = Vector2i(pos[0], pos[1])

	for entry in bubbles_array:
		var col: int = entry[0]
		var row: int = entry[1]
		var color_str: String = entry[2]
		var bubble_type: int = COLOR_STR_TO_TYPE.get(color_str, Bubble.Type.RED)
		spawn_bubble(col, row, bubble_type)

		var cell := Vector2i(col, row)
		if cell == creature_cell:
			var b: Bubble = bubbles[cell]
			b.is_creature = true
			b.queue_redraw()

	print("[Grid] %d burbujas cargadas desde nivel %d" % [bubbles.size(), level_data.get("id", 0)])


func _clear_grid() -> void:
	for cell in bubbles.keys():
		bubbles[cell].queue_free()
	bubbles.clear()
	score = 0
	score_changed.emit(0)


func _spawn_random_fallback() -> void:
	# Fallback si el load del JSON falla. Genera 8 filas random.
	var fallback_types: Array[int] = [
		Bubble.Type.RED, Bubble.Type.BLUE, Bubble.Type.GREEN,
		Bubble.Type.YELLOW, Bubble.Type.PURPLE, Bubble.Type.ORANGE,
	]
	for row in range(8):
		var cols: int = GridLogic.COLS_EVEN if row % 2 == 0 else GridLogic.COLS_ODD
		for col in range(cols):
			spawn_bubble(col, row, fallback_types[randi() % fallback_types.size()])


func spawn_bubble(col: int, row: int, type: int) -> void:
	var bubble: Bubble = BubbleScene.instantiate()
	bubble.position = GridLogic.grid_to_pixel(col, row)
	bubble.grid_pos = Vector2i(col, row)
	bubble.state = Bubble.State.IN_GRID
	add_child(bubble)
	bubble.set_type(type)
	bubbles[Vector2i(col, row)] = bubble


func get_bubble_at(col: int, row: int) -> Bubble:
	return bubbles.get(Vector2i(col, row), null)


func add_landed_bubble(b: Bubble) -> void:
	var snap_global := b.prev_position
	var snap_local := snap_global - global_position
	var cell := GridLogic.pixel_to_grid(snap_local)
	cell = _clamp_to_bounds(cell)

	if bubbles.has(cell):
		cell = _find_empty_neighbor(cell, snap_local)

	if bubbles.has(cell):
		push_warning("[Grid] no se encontró celda vacía, descartando burbuja")
		b.queue_free()
		state_settled.emit()
		return

	var prev_parent := b.get_parent()
	if prev_parent:
		prev_parent.remove_child(b)
	add_child(b)
	b.position = GridLogic.grid_to_pixel(cell.x, cell.y)
	b.grid_pos = cell
	b.state = Bubble.State.IN_GRID
	bubbles[cell] = b

	_process_matches_and_drops(cell)


func _process_matches_and_drops(cell: Vector2i) -> void:
	var match_group := find_connected_same_color(cell)
	if match_group.size() >= 3:
		_explode_group(match_group)
		get_tree().create_timer(DROP_DELAY).timeout.connect(_drop_then_settle)
	else:
		state_settled.emit()


func _drop_then_settle() -> void:
	_drop_floating_bubbles()
	state_settled.emit()


func find_connected_same_color(start: Vector2i) -> Array:
	var start_bubble: Bubble = bubbles.get(start)
	if not start_bubble:
		return []
	var color: int = start_bubble.bubble_type
	var connected: Array[Vector2i] = []
	var to_visit: Array[Vector2i] = [start]
	var visited: Dictionary = {}

	while not to_visit.is_empty():
		var cell: Vector2i = to_visit.pop_back()
		if visited.has(cell):
			continue
		visited[cell] = true

		var b: Bubble = bubbles.get(cell)
		if not b or b.bubble_type != color:
			continue

		connected.append(cell)

		for n in GridLogic.get_neighbors(cell.x, cell.y):
			if not visited.has(n) and _is_in_bounds(n):
				to_visit.append(n)

	return connected


func find_floating_bubbles() -> Array:
	var connected_to_ceiling: Dictionary = {}
	var queue: Array[Vector2i] = []

	for col in range(GridLogic.COLS_EVEN):
		var cell := Vector2i(col, 0)
		if bubbles.has(cell):
			queue.append(cell)
			connected_to_ceiling[cell] = true

	while not queue.is_empty():
		var cell: Vector2i = queue.pop_front()
		for n in GridLogic.get_neighbors(cell.x, cell.y):
			if not connected_to_ceiling.has(n) and bubbles.has(n) and _is_in_bounds(n):
				connected_to_ceiling[n] = true
				queue.append(n)

	var floating: Array[Vector2i] = []
	for cell in bubbles.keys():
		if not connected_to_ceiling.has(cell):
			floating.append(cell)
	return floating


func _explode_group(cells: Array) -> void:
	for cell in cells:
		var b: Bubble = bubbles[cell]
		bubbles.erase(cell)
		b.explode()
	score += cells.size() * POINTS_PER_POPPED
	score_changed.emit(score)
	print("[Grid] match de %d burbujas, score = %d" % [cells.size(), score])


func _drop_floating_bubbles() -> void:
	var floating := find_floating_bubbles()
	if floating.is_empty():
		return
	for cell in floating:
		var b: Bubble = bubbles[cell]
		bubbles.erase(cell)
		b.start_falling()
	score += floating.size() * POINTS_PER_DROPPED
	score_changed.emit(score)
	print("[Grid] %d burbujas cayeron, score = %d" % [floating.size(), score])


# ── Helpers de bounds y vecinos ────────────────────────────────────────────


func _clamp_to_bounds(cell: Vector2i) -> Vector2i:
	var row: int = max(0, cell.y)
	var max_cols: int = GridLogic.COLS_EVEN if row % 2 == 0 else GridLogic.COLS_ODD
	var col: int = clamp(cell.x, 0, max_cols - 1)
	return Vector2i(col, row)


func _is_in_bounds(cell: Vector2i) -> bool:
	if cell.y < 0:
		return false
	var max_cols: int = GridLogic.COLS_EVEN if cell.y % 2 == 0 else GridLogic.COLS_ODD
	return cell.x >= 0 and cell.x < max_cols


func _find_empty_neighbor(cell: Vector2i, ref_pos_local: Vector2) -> Vector2i:
	var neighbors: Array[Vector2i] = GridLogic.get_neighbors(cell.x, cell.y)
	var best_cell := cell
	var best_dist := INF
	for n in neighbors:
		if _is_in_bounds(n) and not bubbles.has(n):
			var n_pixel := GridLogic.grid_to_pixel(n.x, n.y)
			var d := ref_pos_local.distance_to(n_pixel)
			if d < best_dist:
				best_dist = d
				best_cell = n
	return best_cell
