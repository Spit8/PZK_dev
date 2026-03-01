using UnityEngine;
using TMPro;

public class HighlightInteraction : MonoBehaviour
{
    public static HighlightInteraction Instance { get; private set; }

    [Header("UI")]
    public TextMeshProUGUI hoverLabel;

    private GameObject current;

    private void Awake()
    {
        if (Instance != null) { Destroy(gameObject); return; }
        Instance = this;

        if (hoverLabel != null)
            hoverLabel.gameObject.SetActive(false);
    }

    public void Highlight(GameObject target)
    {
        if (current == target) return;
        if (current != null) SetOutline(current, false);

        SetOutline(target, true);
        current = target;

        if (hoverLabel != null)
        {
            PickupItem pickup = target.GetComponentInParent<PickupItem>()
                            ?? target.GetComponent<PickupItem>()
                            ?? target.GetComponentInChildren<PickupItem>();

            hoverLabel.text = (pickup != null && !string.IsNullOrEmpty(pickup.displayName))
                ? pickup.displayName
                : target.name;

            // Positionne d'abord, affiche ensuite
            Vector3 screenPos = Camera.main.WorldToScreenPoint(current.transform.position);
            hoverLabel.transform.position = screenPos + Vector3.up * 50f;
            hoverLabel.gameObject.SetActive(true);
        }
    }

    public void Unhighlight(GameObject target)
    {
        if (current != target) return;
        SetOutline(target, false);
        current = null;

        if (hoverLabel != null)
            hoverLabel.gameObject.SetActive(false); // cache quand plus survolé
    }

    private void SetOutline(GameObject target, bool state)
    {
        Outline outline = target.GetComponent<Outline>()
                      ?? target.GetComponentInChildren<Outline>()
                      ?? target.GetComponentInParent<Outline>();

        if (outline != null) outline.enabled = state;
    }

    private void Update()
    {
        if (current != null && hoverLabel != null)
        {
            Vector3 screenPos = Camera.main.WorldToScreenPoint(current.transform.position);
            hoverLabel.transform.position = screenPos + Vector3.up * 50f; // décalage vertical
        }
    }
}