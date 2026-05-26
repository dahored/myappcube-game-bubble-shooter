using TMPro;
using UnityEngine;

[RequireComponent(typeof(TMP_Text))]
public class LocalizedText : MonoBehaviour
{
    [SerializeField] string key;

    void Start() => GetComponent<TMP_Text>().text = LocaleManager.Get(key);
}
