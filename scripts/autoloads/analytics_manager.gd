extends Node
## AnalyticsManager — Tracking de eventos hacia Firebase Analytics + GameAnalytics.
## Eventos detallados en GDD sección 15.3.

func _ready() -> void:
	print("[AnalyticsManager] inicializado")
	# TODO: inicializar Firebase Analytics
	# TODO: inicializar GameAnalytics
	# TODO: respetar consent de GDPR/CCPA/LGPD (ATT en iOS)

func track(event_name: String, parameters: Dictionary = {}) -> void:
	# TODO: enviar a Firebase Analytics
	# TODO: enviar a GameAnalytics si es evento de juego (level, currency, etc.)
	# TODO: respetar PII rules — nunca incluir datos sensibles en parameters
	if OS.is_debug_build():
		print("[Analytics] %s %s" % [event_name, parameters])

func set_user_property(key: String, value: String) -> void:
	# TODO: Firebase user property
	pass
