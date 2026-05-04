extends Node
## BattlePassManager — XP, tiers, recompensas, temporada activa.
## Reglas detalladas en GDD sección 8.

signal xp_added(new_total: int)
signal tier_unlocked(tier: int)
signal season_ended()

const XP_PER_TIER := 1000
const TOTAL_TIERS := 40

func _ready() -> void:
	print("[BattlePassManager] inicializado")
	# TODO: cargar temporada activa desde Firebase Remote Config

func get_current_tier() -> int:
	return SaveManager.data.get("battle_pass", {}).get("tier", 0)

func get_xp_in_tier() -> int:
	return SaveManager.data.get("battle_pass", {}).get("xp_current_tier", 0)

func is_premium() -> bool:
	return SaveManager.data.get("battle_pass", {}).get("is_premium", false)

func add_xp(amount: int) -> void:
	if amount <= 0: return
	var bp = SaveManager.data["battle_pass"]
	bp["xp_current_tier"] += amount
	while bp["xp_current_tier"] >= XP_PER_TIER and bp["tier"] < TOTAL_TIERS:
		bp["xp_current_tier"] -= XP_PER_TIER
		bp["tier"] += 1
		tier_unlocked.emit(bp["tier"])
	SaveManager.save_to_disk()
	xp_added.emit(amount)

func upgrade_to_premium() -> void:
	# TODO: validar transacción IAP completada antes de marcar como premium
	SaveManager.data["battle_pass"]["is_premium"] = true
	SaveManager.save_to_disk()
