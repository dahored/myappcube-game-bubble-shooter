extends Node
## IAPManager — Wrapper sobre RevenueCat, productos disponibles, restore purchases.
## Productos detallados en GDD sección 6.5.

signal purchase_completed(product_id: String, transaction_id: String)
signal purchase_failed(product_id: String, reason: String)
signal restored(products: Array)

# IDs de productos (deben coincidir con configuración en App Store / Play Console / RevenueCat)
const PRODUCTS := {
	"gems_burbujita": "com.myappcube.coralia.gems.burbujita",
	"gems_concha": "com.myappcube.coralia.gems.concha",
	"gems_coral": "com.myappcube.coralia.gems.coral",
	"gems_tesoro": "com.myappcube.coralia.gems.tesoro",
	"gems_perla_real": "com.myappcube.coralia.gems.perla_real",
	"gems_cofre_mitico": "com.myappcube.coralia.gems.cofre_mitico",
	"starter_pack": "com.myappcube.coralia.starter_pack",
	"battle_pass_s1": "com.myappcube.coralia.battle_pass.s1",
	"infinite_lives_1h": "com.myappcube.coralia.lives.infinite_1h",
	"infinite_lives_24h": "com.myappcube.coralia.lives.infinite_24h",
}

func _ready() -> void:
	print("[IAPManager] inicializado")
	# TODO: inicializar RevenueCat SDK con API key
	# TODO: fetch productos disponibles desde RevenueCat

func purchase(product_id: String) -> void:
	# TODO: validar que el producto existe
	# TODO: trigger flow de compra nativo via RevenueCat
	# TODO: on completion: emit purchase_completed o purchase_failed
	AnalyticsManager.track("iap_initiated", {"product_id": product_id})

func restore_purchases() -> void:
	# TODO: llamar a RevenueCat.restorePurchases()
	# TODO: emit restored con array de products restaurados
	pass
