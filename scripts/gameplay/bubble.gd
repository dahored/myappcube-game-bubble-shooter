extends Node2D
class_name Bubble
## Una burbuja del juego. Cuatro estados:
##   IDLE       — visual estático (ej. preview en cañón)
##   IN_FLIGHT  — viaja con velocidad inicial, rebota en paredes, detecta colisiones
##   IN_GRID    — anclada a una celda hexagonal del grid
##   DROPPING   — cae con gravedad (cuando perdió conexión al techo tras un match)
## Renderiza con _draw() como círculo de color (placeholder).

const RADIUS := 40.0  # Diameter 96 - margen de 16 px

# Límites del play field en coords globales (Gameplay scene)
const PLAYFIELD_LEFT := 0.0
const PLAYFIELD_RIGHT := 1080.0
const CEILING_Y := 80.0  # techo donde la burbuja se "pega" — y de Grid container

# Físicas de drop
const GRAVITY := 2400.0  # px/s²
const DROP_OFFSCREEN_Y := 2200.0  # bajo este y global, la burbuja se libera

enum Type { RED, BLUE, GREEN, YELLOW, PURPLE, ORANGE, RAINBOW }
enum State { IDLE, IN_FLIGHT, IN_GRID, DROPPING }

const TYPE_COLORS := {
	Type.RED: Color(0.93, 0.48, 0.48),
	Type.BLUE: Color(0.49, 0.79, 0.89),
	Type.GREEN: Color(0.62, 0.83, 0.54),
	Type.YELLOW: Color(0.98, 0.85, 0.37),
	Type.PURPLE: Color(0.71, 0.62, 0.85),
	Type.ORANGE: Color(0.96, 0.61, 0.40),
	Type.RAINBOW: Color(1.0, 1.0, 1.0),
}

# Signal emitido cuando una burbuja en vuelo aterriza (impacto con techo o grid)
signal landed(bubble: Bubble)

var bubble_type: Type = Type.RED
var grid_pos: Vector2i = Vector2i(-1, -1)
var state: State = State.IDLE
var velocity: Vector2 = Vector2.ZERO
var prev_position: Vector2 = Vector2.ZERO
var is_creature: bool = false  # Si true, dibuja una estrella encima (objetivo "rescue")


func _draw() -> void:
	var color: Color = TYPE_COLORS.get(bubble_type, Color.WHITE)
	draw_circle(Vector2.ZERO, RADIUS, color)
	draw_circle(Vector2(-RADIUS * 0.35, -RADIUS * 0.35), RADIUS * 0.25, Color(1, 1, 1, 0.5))
	draw_arc(Vector2.ZERO, RADIUS, 0, TAU, 48, Color(0, 0, 0, 0.25), 2.5, true)
	if is_creature:
		_draw_creature_marker()


func _draw_creature_marker() -> void:
	# Estrella de 5 puntas dorada como marcador de "criatura atrapada"
	var radius_outer := 18.0
	var radius_inner := 9.0
	var num_points := 10  # 5 outer + 5 inner alternando
	var points := PackedVector2Array()
	for i in range(num_points):
		var angle: float = (float(i) / float(num_points)) * TAU - PI / 2.0
		var r: float = radius_outer if i % 2 == 0 else radius_inner
		points.append(Vector2(cos(angle), sin(angle)) * r)
	draw_colored_polygon(points, Color(1.0, 0.85, 0.2))
	# Outline sutil
	for i in range(num_points):
		draw_line(points[i], points[(i + 1) % num_points], Color(0.6, 0.4, 0.0, 0.6), 1.5)


func set_type(new_type: Type) -> void:
	bubble_type = new_type
	queue_redraw()


## Lanza la burbuja en vuelo con dirección y velocidad iniciales.
func launch(direction: Vector2, speed: float) -> void:
	state = State.IN_FLIGHT
	velocity = direction.normalized() * speed
	prev_position = global_position


## Animación de explosión cuando la burbuja forma parte de un match.
## La burbuja se hace ligeramente más grande, fade out, y se libera.
func explode() -> void:
	state = State.IDLE  # bloquea cualquier movimiento durante la animación
	var tween := create_tween()
	tween.set_parallel(true)
	tween.tween_property(self, "scale", Vector2(1.4, 1.4), 0.12)
	tween.tween_property(self, "modulate:a", 0.0, 0.18)
	tween.chain().tween_callback(queue_free)


## Inicia caída por gravedad. Usado cuando la burbuja queda flotando tras un match
## y pierde conexión al techo. Ligera variación horizontal aleatoria para que
## múltiples drops no caigan en columna perfecta.
func start_falling() -> void:
	state = State.DROPPING
	velocity = Vector2(randf_range(-80.0, 80.0), 100.0)


func _process(delta: float) -> void:
	match state:
		State.IN_FLIGHT:
			_update_flight(delta)
		State.DROPPING:
			_update_dropping(delta)


func _update_flight(delta: float) -> void:
	prev_position = global_position
	global_position += velocity * delta

	# Rebote en paredes laterales
	if global_position.x - RADIUS < PLAYFIELD_LEFT:
		global_position.x = PLAYFIELD_LEFT + RADIUS
		velocity.x = -velocity.x
	elif global_position.x + RADIUS > PLAYFIELD_RIGHT:
		global_position.x = PLAYFIELD_RIGHT - RADIUS
		velocity.x = -velocity.x

	# Impacto con techo: aterriza
	if global_position.y - RADIUS <= CEILING_Y:
		_land()
		return

	# Colisión con burbujas del grid: aterriza al tocar la primera
	var grid_node := _find_grid()
	if grid_node:
		for cell_key in grid_node.bubbles:
			var other: Bubble = grid_node.bubbles[cell_key]
			if other.state == State.DROPPING:
				continue  # ignorar burbujas que están cayendo
			if global_position.distance_to(other.global_position) < RADIUS * 2 - 4:
				_land()
				return


func _update_dropping(delta: float) -> void:
	velocity.y += GRAVITY * delta
	global_position += velocity * delta
	if global_position.y > DROP_OFFSCREEN_Y:
		queue_free()


func _land() -> void:
	state = State.IN_GRID
	velocity = Vector2.ZERO
	landed.emit(self)


func _find_grid() -> Node:
	var current_scene := get_tree().current_scene
	if current_scene:
		return current_scene.get_node_or_null("Grid")
	return null
