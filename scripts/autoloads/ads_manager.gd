extends Node
## AdsManager — Wrapper sobre AdMob/AppLovin MAX, tracking de fatigue.
## Reglas y caps detallados en GDD sección 9.2.

signal rewarded_ad_completed(placement: String)
signal rewarded_ad_failed(placement: String, reason: String)
signal interstitial_shown()

# Caps diarios por tipo (resetean a la medianoche local)
const CAPS := {
	"life_extra": 3,
	"continue_level": 5,
	"double_reward": 10,
	"daily_chest_extra": 1,
	"free_powerup": 3,
}

var _shown_today: Dictionary = {}

func _ready() -> void:
	print("[AdsManager] inicializado")
	# TODO: inicializar AdMob SDK + AppLovin MAX mediation
	# TODO: pre-cargar próximo rewarded ad para reducir latencia

func can_show_rewarded(placement: String) -> bool:
	var cap: int = CAPS.get(placement, 0)
	var shown: int = _shown_today.get(placement, 0)
	return shown < cap

func show_rewarded(placement: String) -> void:
	if not can_show_rewarded(placement):
		rewarded_ad_failed.emit(placement, "cap_reached")
		return
	# TODO: llamar SDK para mostrar rewarded
	# TODO: si ad completed → incrementar _shown_today, emit completed signal
	# Por ahora simula éxito inmediato (placeholder)
	_shown_today[placement] = _shown_today.get(placement, 0) + 1
	AnalyticsManager.track("ad_completed", {"placement": placement, "ad_type": "rewarded"})
	rewarded_ad_completed.emit(placement)

func show_interstitial() -> void:
	# TODO: llamar SDK, respetar regla de máx 1 cada 3 niveles ganados
	interstitial_shown.emit()

func _on_day_changed() -> void:
	# Reset caps al cambio de día local
	_shown_today.clear()
