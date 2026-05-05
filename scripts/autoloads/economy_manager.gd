extends Node
## EconomyManager — Monedas, gemas, vidas con regen automática, transacciones internas.
## Reglas y costos detallados en GDD sección 6.

signal coins_changed(new_amount: int)
signal gems_changed(new_amount: int)
signal lives_changed(new_amount: int)
signal transaction_failed(reason: String)

const MAX_LIVES := 5
const LIFE_REGEN_SECONDS := 1800  # 30 minutos

var _regen_timer: Timer


func _ready() -> void:
	print("[EconomyManager] inicializado")
	SaveManager.save_loaded.connect(_on_save_loaded)


func _on_save_loaded() -> void:
	_calculate_offline_regen()
	_start_regen_timer()


# ── Vidas ─────────────────────────────────────────────────────────────────────

func get_lives() -> int:
	return SaveManager.data.get("lives", MAX_LIVES)


func consume_life() -> bool:
	if get_lives() <= 0:
		return false
	SaveManager.data["lives"] -= 1
	# Arrancar regen si estaba en cap (ahora ya hay espacio)
	if SaveManager.data.get("lives_last_regen", 0) == 0:
		SaveManager.data["lives_last_regen"] = Time.get_unix_time_from_system()
	SaveManager.save_to_disk()
	lives_changed.emit(get_lives())
	return true


func add_life() -> void:
	var current := get_lives()
	if current >= MAX_LIVES:
		return
	SaveManager.data["lives"] = current + 1
	# Avanzar el timestamp de regen para el próximo ciclo
	var last: float = SaveManager.data.get("lives_last_regen", Time.get_unix_time_from_system())
	SaveManager.data["lives_last_regen"] = last + LIFE_REGEN_SECONDS
	SaveManager.save_to_disk()
	lives_changed.emit(get_lives())


func seconds_until_next_life() -> int:
	if get_lives() >= MAX_LIVES:
		return 0
	var now := Time.get_unix_time_from_system()
	var last: float = SaveManager.data.get("lives_last_regen", now)
	var elapsed := now - last
	return max(0, int(LIFE_REGEN_SECONDS - elapsed))


func _calculate_offline_regen() -> void:
	var current := get_lives()
	if current >= MAX_LIVES:
		return
	var now := Time.get_unix_time_from_system()
	var last: float = SaveManager.data.get("lives_last_regen", now)
	var intervals := int((now - last) / LIFE_REGEN_SECONDS)
	if intervals <= 0:
		return
	var new_lives: int = mini(current + intervals, MAX_LIVES)
	var gained: int = new_lives - current
	SaveManager.data["lives"] = new_lives
	SaveManager.data["lives_last_regen"] = last + gained * LIFE_REGEN_SECONDS
	SaveManager.save_to_disk()
	lives_changed.emit(new_lives)
	print("[EconomyManager] regen offline: +%d vidas (total %d)" % [gained, new_lives])


func _start_regen_timer() -> void:
	if _regen_timer:
		_regen_timer.queue_free()
	_regen_timer = Timer.new()
	_regen_timer.wait_time = 60.0  # tick cada minuto, comprueba si toca regen
	_regen_timer.autostart = true
	_regen_timer.timeout.connect(_on_regen_tick)
	add_child(_regen_timer)


func _on_regen_tick() -> void:
	var current := get_lives()
	if current >= MAX_LIVES:
		return
	var now := Time.get_unix_time_from_system()
	var last: float = SaveManager.data.get("lives_last_regen", now)
	if now - last >= LIFE_REGEN_SECONDS:
		add_life()


# ── Monedas ───────────────────────────────────────────────────────────────────

func get_coins() -> int:
	return SaveManager.data.get("currencies", {}).get("coins", 0)


func add_coins(amount: int, source: String = "unknown") -> void:
	if amount <= 0:
		return
	SaveManager.data["currencies"]["coins"] += amount
	SaveManager.save_to_disk()
	coins_changed.emit(get_coins())
	AnalyticsManager.track("currency_earned", {"type": "coins", "amount": amount, "source": source})


func spend_coins(amount: int, item: String) -> bool:
	if get_coins() < amount:
		transaction_failed.emit("not_enough_coins")
		return false
	SaveManager.data["currencies"]["coins"] -= amount
	SaveManager.save_to_disk()
	coins_changed.emit(get_coins())
	AnalyticsManager.track("currency_spent", {"type": "coins", "amount": amount, "item": item})
	return true


# ── Gemas ─────────────────────────────────────────────────────────────────────

func get_gems() -> int:
	return SaveManager.data.get("currencies", {}).get("gems", 0)


func add_gems(amount: int, source: String = "unknown") -> void:
	if amount <= 0:
		return
	SaveManager.data["currencies"]["gems"] += amount
	SaveManager.save_to_disk()
	gems_changed.emit(get_gems())
	AnalyticsManager.track("currency_earned", {"type": "gems", "amount": amount, "source": source})


func spend_gems(amount: int, item: String) -> bool:
	if get_gems() < amount:
		transaction_failed.emit("not_enough_gems")
		return false
	SaveManager.data["currencies"]["gems"] -= amount
	SaveManager.save_to_disk()
	gems_changed.emit(get_gems())
	AnalyticsManager.track("currency_spent", {"type": "gems", "amount": amount, "item": item})
	return true


# ── Drops por nivel (GDD §6.6) ───────────────────────────────────────────────

## Calcula y entrega recompensas de monedas/gemas al completar un nivel.
## Retorna el dict con lo ganado: {"coins": N, "gems": M}
func award_level_completion(chapter: int, is_first_completion: bool) -> Dictionary:
	var base_coins := 50 + (chapter - 1) * 10  # ch1=50 … ch6=100
	if is_first_completion:
		base_coins = int(base_coins * 1.5)      # +50% bonus primera vez

	var gems := 0
	if randf() < 0.3:                           # 30% de probabilidad de gemas
		gems = randi_range(1, 3)

	add_coins(base_coins, "level_complete")
	if gems > 0:
		add_gems(gems, "level_complete")

	print("[EconomyManager] drops nivel: %d monedas, %d gemas (first=%s)" % [base_coins, gems, str(is_first_completion)])
	return {"coins": base_coins, "gems": gems}
