extends Node2D
class_name Canon
## Cañón con apuntado por drag, línea de trayectoria con primer rebote, y disparo.
## Mantiene cola de 2 burbujas (current + next preview). Tap en current burbuja → swap colors.

const BubbleScene := preload("res://scenes/gameplay/bubble.tscn")

const SHOT_SPEED := 1500.0
const SCREEN_WIDTH := 1080.0
const CEILING_Y := 80.0
const TRAJECTORY_MAX_LENGTH := 2400.0
const MIN_AIM_DELTA_Y := 60.0  # mínimo pixels arriba del cañón para apuntar
const TAP_VS_DRAG_THRESHOLD := 20.0  # pixels de movimiento para considerar "drag"

const DEFAULT_PLAYABLE_TYPES: Array[int] = [
	Bubble.Type.RED, Bubble.Type.BLUE, Bubble.Type.GREEN,
	Bubble.Type.YELLOW, Bubble.Type.PURPLE, Bubble.Type.ORANGE,
]

# Tipos jugables del nivel actual. Se reconfigura desde gameplay.gd al cargar nivel.
var playable_types: Array[int] = DEFAULT_PLAYABLE_TYPES.duplicate()

@onready var trajectory_line: Line2D = $TrajectoryLine
@onready var current_visual: Bubble = $CurrentVisual
@onready var next_visual: Bubble = $NextVisual

# Signal: emitido cuando se dispara un tiro (para que Gameplay decremente disparos)
signal shot_fired

var current_type: int = Bubble.Type.RED
var next_type: int = Bubble.Type.BLUE
var is_aiming: bool = false
var has_dragged: bool = false
var touch_start_pos: Vector2 = Vector2.ZERO
var last_aim_direction: Vector2 = Vector2(0, -1)
var can_shoot: bool = true       # Bloqueo durante vuelo de la burbuja anterior
var level_active: bool = true    # Bloqueo cuando el nivel terminó (win/lose)

# Posiciones canónicas de los visuals (capturadas en _ready, se usan para reset tras animaciones)
var _current_canonical_pos: Vector2
var _current_canonical_scale: Vector2
var _next_canonical_pos: Vector2
var _next_canonical_scale: Vector2


func _ready() -> void:
	_load_initial_queue()
	trajectory_line.clear_points()
	queue_redraw()
	# Capturar posiciones canónicas de los visuals (definidas en canon.tscn)
	_current_canonical_pos = current_visual.position
	_current_canonical_scale = current_visual.scale
	_next_canonical_pos = next_visual.position
	_next_canonical_scale = next_visual.scale
	# Suscribirse al grid para refrescar la cola cuando el estado cambie tras un match/drop
	var grid_node := get_parent().get_node_or_null("Grid")
	if grid_node:
		grid_node.state_settled.connect(_refresh_queue_if_invalid)


## Dibuja la concha base del cañón debajo de la burbuja current (placeholder).
func _draw() -> void:
	var shell_color := Color(0.4, 0.65, 0.85, 0.9)
	var shell_radius := 75.0
	# Arco semicircular debajo del bubble current (de 0 a PI = mitad inferior)
	draw_arc(Vector2(0, 30), shell_radius, 0.0, PI, 48, shell_color, 10.0, true)
	# Línea horizontal sutil que marca la separación entre current y next
	draw_line(Vector2(55, 30), Vector2(95, 30), Color(0.55, 0.55, 0.55, 0.5), 3.0)


func _load_initial_queue() -> void:
	current_type = _random_type()
	next_type = _random_type()
	current_visual.set_type(current_type)
	next_visual.set_type(next_type)


func _input(event: InputEvent) -> void:
	# Usamos _input en vez de _unhandled_input para que el ColorRect del background
	# no consuma los eventos antes de que lleguen al cañón.
	if not can_shoot or not level_active:
		return

	if event is InputEventScreenTouch:
		if event.pressed:
			_on_touch_start(event.position)
		else:
			_on_touch_release(event.position)
	elif event is InputEventScreenDrag:
		_on_touch_drag(event.position)


func _on_touch_start(pos: Vector2) -> void:
	touch_start_pos = pos
	has_dragged = false


func _on_touch_drag(pos: Vector2) -> void:
	if not has_dragged and pos.distance_to(touch_start_pos) > TAP_VS_DRAG_THRESHOLD:
		has_dragged = true
		is_aiming = true

	if is_aiming:
		_update_aim(pos)


func _on_touch_release(pos: Vector2) -> void:
	if has_dragged:
		# Drag-and-release → disparar
		_release_shot()
	else:
		# Tap puro: si fue sobre la burbuja current, swap colors
		if pos.distance_to(current_visual.global_position) < Bubble.RADIUS * 1.3:
			_swap_colors()
	is_aiming = false
	has_dragged = false
	trajectory_line.clear_points()


func _update_aim(global_pos: Vector2) -> void:
	# Transformar a coords locales del cañón (origen es el cañón)
	var local_target := to_local(global_pos)
	# Clamp Y para que no apunte hacia abajo (siempre apuntar arriba o lateral)
	if local_target.y > -MIN_AIM_DELTA_Y:
		local_target.y = -MIN_AIM_DELTA_Y
	last_aim_direction = local_target.normalized()
	_draw_trajectory(last_aim_direction)


func _release_shot() -> void:
	_shoot(last_aim_direction)


func _swap_colors() -> void:
	var tmp := current_type
	current_type = next_type
	next_type = tmp
	current_visual.set_type(current_type)
	next_visual.set_type(next_type)


func _shoot(direction: Vector2) -> void:
	can_shoot = false
	var b: Bubble = BubbleScene.instantiate()
	get_parent().add_child(b)  # parent = Gameplay scene root, así global_position se respeta
	b.global_position = global_position
	b.set_type(current_type)  # disparada con el color del current actual
	# La señal ya emite la burbuja como argumento — no hay que hacer bind aquí.
	b.landed.connect(_on_shot_landed)
	b.launch(direction, SHOT_SPEED)
	# Avanzar la cola lógicamente
	var new_current_color: int = next_type
	var new_next_color: int = _random_type()
	current_type = new_current_color
	next_type = new_next_color
	# Animar la rotación visual de la cola (no bloqueante — corre durante el vuelo)
	# El current "ya se fue" en el disparo, así que no necesita drop animation
	_animate_queue_advance(new_current_color, new_next_color, false)
	shot_fired.emit()


func _on_shot_landed(b: Bubble) -> void:
	var grid: Grid = get_parent().get_node("Grid")
	grid.add_landed_bubble(b)
	can_shoot = true


func _draw_trajectory(direction: Vector2) -> void:
	# Trayectoria en coords locales del cañón. Calcula primer rebote contra paredes
	# y se detiene al hit del techo.
	var points: PackedVector2Array = [Vector2.ZERO]
	var current_pos := Vector2.ZERO
	var current_dir := direction

	# Convertir paredes globales a locales del cañón
	var wall_left := PLAYFIELD_LEFT - global_position.x
	var wall_right := SCREEN_WIDTH - global_position.x
	var ceiling := CEILING_Y - global_position.y
	var max_bounces := 1

	for i in range(max_bounces + 1):
		var t_wall := INF
		var t_ceiling := INF
		# Tiempo hasta pared lateral más cercana en la dirección actual
		if current_dir.x > 0.001:
			t_wall = (wall_right - Bubble.RADIUS - current_pos.x) / current_dir.x
		elif current_dir.x < -0.001:
			t_wall = (wall_left + Bubble.RADIUS - current_pos.x) / current_dir.x
		# Tiempo hasta techo
		if current_dir.y < -0.001:
			t_ceiling = (ceiling + Bubble.RADIUS - current_pos.y) / current_dir.y

		var t: float = min(t_wall, t_ceiling)
		t = min(t, TRAJECTORY_MAX_LENGTH)
		var next_pos := current_pos + current_dir * t
		points.append(next_pos)

		if t_ceiling <= t_wall:
			break  # llegó al techo, no rebota más
		if i == max_bounces:
			break  # consumió todos los rebotes

		current_pos = next_pos
		current_dir = Vector2(-current_dir.x, current_dir.y)

	trajectory_line.points = points


## Smart queue: solo selecciona colores que tienen 2+ instancias en el grid actual,
## para evitar que el jugador reciba colores "muertos" (los que no pueden matchear).
## Si ningún color cumple el threshold (ej. al final del nivel), fallback a random
## entre los playable_types del nivel.
func _random_type() -> int:
	var grid_colors := _get_smart_colors()
	if grid_colors.is_empty():
		return playable_types[randi() % playable_types.size()]
	return grid_colors[randi() % grid_colors.size()]


## Tras cada disparo aterrizado (state_settled), revalida que current y next sigan
## siendo colores presentes en grid. Si current ya no es válido, dispara la animación
## de drop + reemplazo. Si solo next es inválido, se reemplaza sin animación.
func _refresh_queue_if_invalid() -> void:
	var smart := _get_smart_colors()
	if smart.is_empty():
		return

	if current_type not in smart:
		can_shoot = false
		var color_for_new_current: int = next_type if next_type in smart else smart[randi() % smart.size()]
		var color_for_new_next: int = smart[randi() % smart.size()]
		# Si el next también era inválido, ajustar su color antes de animar para que
		# la burbuja que se desliza muestre el color correcto
		if next_type not in smart and next_visual:
			next_visual.set_type(color_for_new_current)
		current_type = color_for_new_current
		next_type = color_for_new_next
		await _animate_queue_advance(color_for_new_current, color_for_new_next, true)
		can_shoot = true
	elif next_type not in smart:
		next_type = smart[randi() % smart.size()]
		if next_visual:
			next_visual.set_type(next_type)


## Animación unificada de rotación de cola.
##   - drop_current=false: usado al disparar — la burbuja current "ya se fue" en
##     el disparo, así que se invisibiliza al instante. Solo se anima el slide del
##     next al center y el fade-in de la nueva en el preview.
##   - drop_current=true: usado al refrescar (color inválido) — la burbuja current
##     cae con fade + shrink antes de que el next la reemplace.
## En ambos casos, la fase final es: slide visualmente claro del next al center,
## seguido del fade-in de una nueva burbuja en el slot del preview.
func _animate_queue_advance(new_current_color: int, new_next_color: int, drop_current: bool) -> void:
	if not current_visual or not next_visual:
		return

	var slide_duration: float = 0.28 if drop_current else 0.22

	var tween1 := create_tween()
	tween1.set_parallel(true)

	if drop_current:
		# Refresh case: current cae con fade out + shrink
		tween1.tween_property(current_visual, "position:y", _current_canonical_pos.y + 80.0, slide_duration)
		tween1.tween_property(current_visual, "modulate:a", 0.0, slide_duration * 0.8)
		tween1.tween_property(current_visual, "scale", _current_canonical_scale * 0.7, slide_duration)
	else:
		# Shot case: current ya se fue como burbuja disparada — invisible al instante
		current_visual.modulate.a = 0.0

	# En ambos casos: next se desliza al center y crece al tamaño de current
	tween1.tween_property(next_visual, "position", _current_canonical_pos, slide_duration)
	tween1.tween_property(next_visual, "scale", _current_canonical_scale, slide_duration)
	await tween1.finished

	# Swap atómico: current_visual se convierte en el "nuevo current" en posición canónica
	current_visual.position = _current_canonical_pos
	current_visual.scale = _current_canonical_scale
	current_visual.modulate.a = 1.0
	current_visual.set_type(new_current_color)

	# next_visual vuelve al slot de preview con el nuevo color, invisible (fade in después)
	next_visual.position = _next_canonical_pos
	next_visual.scale = _next_canonical_scale
	next_visual.modulate.a = 0.0
	next_visual.set_type(new_next_color)

	# Fade in del nuevo preview
	var tween2 := create_tween()
	tween2.tween_property(next_visual, "modulate:a", 1.0, 0.18)
	await tween2.finished


## Devuelve los tipos de burbuja que tienen 1+ instancias en el grid (filtrados también
## por playable_types del nivel para no spawnear colores no permitidos).
## Usamos threshold de 1 (no 2) para que los colores "huérfanos" sigan apareciendo en
## el cañón — el jugador puede acumular instancias adicionales para futuros matches.
## Mismo enfoque que Candy Crush: siempre dar colores que existen en el board.
func _get_smart_colors() -> Array:
	var grid_node: Grid = get_parent().get_node_or_null("Grid") as Grid
	if not grid_node:
		return []
	var colors_set: Dictionary = {}
	for cell in grid_node.bubbles:
		var b: Bubble = grid_node.bubbles[cell]
		if b.state == Bubble.State.IN_GRID and b.bubble_type in playable_types:
			colors_set[b.bubble_type] = true
	var result: Array = []
	for c in colors_set:
		result.append(c)
	return result


## Reconfigura los colores jugables basado en el nivel actual. Llamado desde gameplay.gd
## tras cargar un nivel desde JSON.
func configure_playable_types(types: Array[int]) -> void:
	if types.is_empty():
		playable_types = DEFAULT_PLAYABLE_TYPES.duplicate()
	else:
		playable_types = types.duplicate()
	# Refrescar la cola con los colores del nuevo nivel
	current_type = _random_type()
	next_type = _random_type()
	if current_visual:
		current_visual.set_type(current_type)
	if next_visual:
		next_visual.set_type(next_type)


# Necesitamos PLAYFIELD_LEFT en este script también (alineado con bubble.gd)
const PLAYFIELD_LEFT := 0.0
