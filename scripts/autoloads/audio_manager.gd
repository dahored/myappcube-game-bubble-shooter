extends Node
## AudioManager — Reproducción de música y SFX, volumen por bus, vibración.
## 3 buses: Music (música ambiente), UI_FX (interfaz), BubblePop (pop de burbujas).
## Todos los play_* son no-crash: si el archivo OGG no existe, loggea y retorna.

enum AudioCategory { MUSIC, UI_FX, BUBBLE_POP }

const BUS_MUSIC     := "Music"
const BUS_UI_FX     := "UI_FX"
const BUS_BUBBLE_POP := "BubblePop"

const SFX_PATH   := "res://assets/audio/sfx/%s.ogg"
const MUSIC_PATH := "res://assets/audio/music/%s.ogg"

var _music_player: AudioStreamPlayer

var volume_music: float = 0.7
var volume_ui: float = 0.8
var volume_pop: float = 1.0


func _ready() -> void:
	_setup_buses()
	_music_player = AudioStreamPlayer.new()
	_music_player.bus = BUS_MUSIC
	add_child(_music_player)
	# Cargar preferencias guardadas cuando SaveManager emita save_loaded
	SaveManager.save_loaded.connect(_on_save_loaded)


func _setup_buses() -> void:
	_ensure_bus(BUS_MUSIC)
	_ensure_bus(BUS_UI_FX)
	_ensure_bus(BUS_BUBBLE_POP)


func _ensure_bus(bus_name: String) -> void:
	if AudioServer.get_bus_index(bus_name) != -1:
		return
	AudioServer.add_bus()
	var idx := AudioServer.get_bus_count() - 1
	AudioServer.set_bus_name(idx, bus_name)
	AudioServer.set_bus_send(idx, "Master")


func _on_save_loaded() -> void:
	var s: Dictionary = SaveManager.data.get("settings", {})
	set_volume(AudioCategory.MUSIC,      s.get("music_volume", 0.7))
	set_volume(AudioCategory.UI_FX,      s.get("ui_volume",    0.8))
	set_volume(AudioCategory.BUBBLE_POP, s.get("pop_volume",   1.0))


# ── Música ────────────────────────────────────────────────────────────────────

func play_music(track_name: String, fade_in: float = 1.0) -> void:
	var path := MUSIC_PATH % track_name
	if not ResourceLoader.exists(path):
		push_warning("[AudioManager] música no encontrada: %s" % path)
		return
	var stream: AudioStream = load(path)
	if not stream:
		return
	if stream is AudioStreamOggVorbis:
		stream.loop = true
	_music_player.stream = stream
	_music_player.volume_db = -80.0
	_music_player.play()
	var tween := create_tween()
	tween.tween_property(_music_player, "volume_db", linear_to_db(volume_music), fade_in)


func stop_music(fade_out: float = 0.5) -> void:
	if not _music_player.playing:
		return
	var tween := create_tween()
	tween.tween_property(_music_player, "volume_db", -80.0, fade_out)
	tween.tween_callback(_music_player.stop)


# ── SFX ──────────────────────────────────────────────────────────────────────

func play_sfx(sfx_name: String, category: AudioCategory = AudioCategory.UI_FX) -> void:
	var path := SFX_PATH % sfx_name
	if not ResourceLoader.exists(path):
		push_warning("[AudioManager] sfx no encontrado: %s" % path)
		return
	var stream: AudioStream = load(path)
	if not stream:
		return
	var player := AudioStreamPlayer.new()
	player.stream = stream
	player.bus = _bus_name(category)
	if category == AudioCategory.BUBBLE_POP:
		player.pitch_scale = 1.0 + randf_range(-0.08, 0.08)
	add_child(player)
	player.play()
	player.finished.connect(player.queue_free)


func vibrate(duration_ms: int = 50) -> void:
	if SaveManager.data.get("settings", {}).get("vibration_enabled", true):
		Input.vibrate_handheld(duration_ms)


# ── Volumen ───────────────────────────────────────────────────────────────────

func set_volume(category: AudioCategory, value: float) -> void:
	value = clamp(value, 0.0, 1.0)
	match category:
		AudioCategory.MUSIC:
			volume_music = value
		AudioCategory.UI_FX:
			volume_ui = value
		AudioCategory.BUBBLE_POP:
			volume_pop = value
	var idx := AudioServer.get_bus_index(_bus_name(category))
	if idx >= 0:
		AudioServer.set_bus_volume_db(idx, linear_to_db(value) if value > 0.0 else -80.0)


func _bus_name(category: AudioCategory) -> String:
	match category:
		AudioCategory.MUSIC:      return BUS_MUSIC
		AudioCategory.UI_FX:      return BUS_UI_FX
		AudioCategory.BUBBLE_POP: return BUS_BUBBLE_POP
	return "Master"
