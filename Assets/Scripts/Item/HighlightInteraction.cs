using UnityEngine;

public class HighlightInteraction : MonoBehaviour
{
    public static HighlightInteraction Instance { get; private set; }

    private GameObject current;

    private void Awake()
    {
        if (Instance != null) { Destroy(gameObject); return; }
        Instance = this;
    }

    public void Highlight(GameObject target)
    {
        if (current == target) return;

        // Retire le highlight de l'objet précédent
        if (current != null)
            SetOutline(current, false);

        SetOutline(target, true);
        current = target;
    }

    public void Unhighlight(GameObject target)
    {
        if (current != target) return;

        SetOutline(target, false);
        current = null;
    }

    private void SetOutline(GameObject target, bool state)
    {
        Outline outline = target.GetComponent<Outline>()
                      ?? target.GetComponentInChildren<Outline>()
                      ?? target.GetComponentInParent<Outline>();

        if (outline != null) outline.enabled = state;
        else Debug.LogWarning("Outline non trouvé sur : " + target.name);
    }
}
