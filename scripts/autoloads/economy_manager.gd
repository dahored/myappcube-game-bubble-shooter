extends Node
## EconomyManager — Monedas, gemas, vidas, transacciones internas.
## Reglas y costos detallados en GDD sección 6.

signal coins_changed(new_amount: int)
signal gems_changed(new_amount: int)
signal lives_changed(new_amount: int)
signal transaction_failed(reason: String)

const MAX_LIVES := 5
const LIFE_REGEN_SECONDS := 1800  # 30 minutos

func _ready() -> void:
	print("[EconomyManager] inicializado")
	# TODO: arrancar timer de regen de vidas

func get_coins() -> int:
	return SaveManager.data.get("currencies", {}).get("coins", 0)

func get_gems() -> int:
	return SaveManager.data.get("currencies", {}).get("gems", 0)

func get_lives() -> int:
	return SaveManager.data.get("lives", 0)

func add_coins(amount: int, source: String = "unknown") -> void:
	if amount <= 0: return
	SaveManager.data["currencies"]["coins"] += amount
	SaveManager.save_to_disk()
	coins_changed.emit(get_coins())
	AnalyticsManager.track("currency_earned", {"type": "coins", "amount": amount, "source": source})

func add_gems(amount: int, source: String = "unknown") -> void:
	if amount <= 0: return
	SaveManager.data["currencies"]["gems"] += amount
	SaveManager.save_to_disk()
	gems_changed.emit(get_gems())
	AnalyticsManager.track("currency_earned", {"type": "gems", "amount": amount, "source": source})

func spend_coins(amount: int, item: String) -> bool:
	if get_coins() < amount:
		transaction_failed.emit("not_enough_coins")
		return false
	SaveManager.data["currencies"]["coins"] -= amount
	SaveManager.save_to_disk()
	coins_changed.emit(get_coins())
	AnalyticsManager.track("currency_spent", {"type": "coins", "amount": amount, "item": item})
	return true

func spend_gems(amount: int, item: String) -> bool:
	if get_gems() < amount:
		transaction_failed.emit("not_enough_gems")
		return false
	SaveManager.data["currencies"]["gems"] -= amount
	SaveManager.save_to_disk()
	gems_changed.emit(get_gems())
	AnalyticsManager.track("currency_spent", {"type": "gems", "amount": amount, "item": item})
	return true

func consume_life() -> bool:
	if get_lives() <= 0:
		return false
	SaveManager.data["lives"] -= 1
	SaveManager.save_to_disk()
	lives_changed.emit(get_lives())
	return true

func add_life() -> void:
	if get_lives() >= MAX_LIVES: return
	SaveManager.data["lives"] += 1
	SaveManager.save_to_disk()
	lives_changed.emit(get_lives())
