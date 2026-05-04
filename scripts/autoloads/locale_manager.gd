extends Node
## LocaleManager — Localización i18n, cambio de idioma en runtime.
## 6 idiomas soportados según convención cross-proyecto. Pipeline en GDD sección 12.2bis.

signal locale_changed(new_locale: String)

const SUPPORTED_LOCALES := ["es", "en", "it", "fr", "de", "pt"]
const LOCALE_NAMES := {
	"es": "Español",
	"en": "English",
	"it": "Italiano",
	"fr": "Français",
	"de": "Deutsch",
	"pt": "Português",
}

func _ready() -> void:
	print("[LocaleManager] inicializado")
	var saved_locale: String = SaveManager.data.get("settings", {}).get("language", "")
	if saved_locale and saved_locale in SUPPORTED_LOCALES:
		set_locale(saved_locale)
	else:
		var os_locale := OS.get_locale_language()
		if os_locale in SUPPORTED_LOCALES:
			set_locale(os_locale)
		else:
			set_locale("en")  # fallback global

func set_locale(locale: String) -> void:
	if locale not in SUPPORTED_LOCALES:
		push_warning("Locale '%s' no soportado, usando 'en'" % locale)
		locale = "en"
	TranslationServer.set_locale(locale)
	SaveManager.data["settings"]["language"] = locale
	SaveManager.save_to_disk()
	locale_changed.emit(locale)

func get_current_locale() -> String:
	return TranslationServer.get_locale()

func t(key: String) -> String:
	return tr(key)
