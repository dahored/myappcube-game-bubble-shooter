class_name GridLogic
extends RefCounted
## Pure math para el grid hexagonal: conversión de coordenadas y vecinos.
## Usa "offset coordinates" con filas impares desplazadas medio diámetro a la derecha.
## Diseño: pointy-top hex packed circles, columnas alineadas en filas pares.

const BUBBLE_DIAMETER := 96.0
const ROW_HEIGHT := BUBBLE_DIAMETER * 0.866025  # sqrt(3)/2 — vertical spacing real entre filas

const COLS_EVEN := 11   # burbujas por fila en filas pares (0, 2, 4...)
const COLS_ODD := 10    # burbujas por fila en filas impares (1, 3, 5...)


## Convierte (col, row) a posición en pixels relativos al grid container.
## La burbuja en (0, 0) queda en pixel (D/2, D/2) — su borde izq y top tocan (0,0).
static func grid_to_pixel(col: int, row: int) -> Vector2:
	var x := col * BUBBLE_DIAMETER + BUBBLE_DIAMETER * 0.5
	if row % 2 == 1:
		x += BUBBLE_DIAMETER * 0.5
	var y := row * ROW_HEIGHT + BUBBLE_DIAMETER * 0.5
	return Vector2(x, y)


## Convierte una posición en pixels a la celda (col, row) más cercana.
## Útil para snap de la burbuja disparada a su celda destino.
static func pixel_to_grid(pos: Vector2) -> Vector2i:
	var row := int(round((pos.y - BUBBLE_DIAMETER * 0.5) / ROW_HEIGHT))
	var x_offset := BUBBLE_DIAMETER * 0.5
	if row % 2 == 1:
		x_offset += BUBBLE_DIAMETER * 0.5
	var col := int(round((pos.x - x_offset) / BUBBLE_DIAMETER))
	return Vector2i(col, row)


## Devuelve hasta 6 vecinos de una celda hexagonal (sin filtro de bounds).
## El caller debe filtrar coordenadas inválidas según los bounds del grid.
static func get_neighbors(col: int, row: int) -> Array[Vector2i]:
	var neighbors: Array[Vector2i] = []
	var is_odd := (row % 2 == 1)
	# Mismo row (siempre hay vecinos potenciales izq/der)
	neighbors.append(Vector2i(col - 1, row))
	neighbors.append(Vector2i(col + 1, row))
	if is_odd:
		# Filas impares: vecinos sup en (col, row-1) y (col+1, row-1)
		neighbors.append(Vector2i(col, row - 1))
		neighbors.append(Vector2i(col + 1, row - 1))
		neighbors.append(Vector2i(col, row + 1))
		neighbors.append(Vector2i(col + 1, row + 1))
	else:
		# Filas pares: vecinos sup en (col-1, row-1) y (col, row-1)
		neighbors.append(Vector2i(col - 1, row - 1))
		neighbors.append(Vector2i(col, row - 1))
		neighbors.append(Vector2i(col - 1, row + 1))
		neighbors.append(Vector2i(col, row + 1))
	return neighbors


## Verifica si una celda (col, row) es válida dentro del grid (bounds check).
static func is_valid_cell(col: int, row: int, total_rows: int) -> bool:
	if row < 0 or row >= total_rows:
		return false
	var max_cols := COLS_EVEN if row % 2 == 0 else COLS_ODD
	return col >= 0 and col < max_cols
