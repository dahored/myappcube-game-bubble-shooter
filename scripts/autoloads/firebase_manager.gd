extends Node
## FirebaseManager — Auth, Firestore, Cloud Messaging, Remote Config, Crashlytics.
## Servicios Firebase documentados en GDD sección 14.6.

signal auth_state_changed(is_logged_in: bool)
signal remote_config_loaded()
signal cloud_save_synced()

var is_authenticated: bool = false
var user_id: String = ""

func _ready() -> void:
	print("[FirebaseManager] inicializado")
	# TODO: inicializar Firebase SDK (plugin de Godot)
	# TODO: anonymous auth si no hay usuario logueado
	# TODO: cargar Remote Config

func sign_in_with_apple() -> void:
	# TODO: flow OAuth nativo iOS
	pass

func sign_in_with_facebook() -> void:
	# TODO: flow OAuth Facebook SDK
	pass

func sign_in_with_google() -> void:
	# TODO: flow OAuth Google
	pass

func sign_out() -> void:
	is_authenticated = false
	user_id = ""
	auth_state_changed.emit(false)

func upload_save_to_cloud(data: Dictionary) -> void:
	# TODO: escribir a Firestore en colección "saves" doc id = user_id
	pass

func download_save_from_cloud() -> Dictionary:
	# TODO: leer Firestore doc, retornar dictionary
	return {}

func get_remote_config(key: String, default_value: Variant = null) -> Variant:
	# TODO: leer Firebase Remote Config
	return default_value
