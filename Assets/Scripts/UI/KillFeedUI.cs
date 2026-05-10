using UnityEngine;
using UnityEngine.UI;
using System.Collections;
using System.Collections.Generic;

/// <summary>
/// UI du kill feed (Phase 3.3).
/// Affiche les kills récents avec un pool d'entrées qui s'effacent automatiquement.
/// Piloté par l'événement SessionScoreboard.OnKillEvent via RpcBroadcastKill.
///
/// Setup :
/// - Attacher à un GameObject enfant du Canvas de scène
/// - Le pool d'entrées est créé dynamiquement au runtime
/// </summary>
public class KillFeedUI : MonoBehaviour
{
    [Header("Configuration")]
    [SerializeField] private int maxVisibleEntries = 6;
    [SerializeField] private float entryDisplayDuration = 5f;
    [SerializeField] private float entryFadeDuration = 1f;

    [Header("Style")]
    [SerializeField] private int fontSize = 16;
    [SerializeField] private Color killerColor = new Color(1f, 0.4f, 0.4f, 1f);
    [SerializeField] private Color victimColor = new Color(0.7f, 0.7f, 0.7f, 1f);
    [SerializeField] private Color separatorColor = Color.white;

    // ─────────────────────────────────────────────────────────────────────────
    // Internals
    // ─────────────────────────────────────────────────────────────────────────

    private RectTransform container;
    private Queue<KillFeedEntry> entryPool;
    private List<KillFeedEntry> activeEntries;

    private struct KillFeedEntry
    {
        public GameObject gameObject;
        public Text text;
        public CanvasGroup canvasGroup;
        public Coroutine fadeCoroutine;
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Lifecycle
    // ─────────────────────────────────────────────────────────────────────────

    private void Awake()
    {
        entryPool = new Queue<KillFeedEntry>(maxVisibleEntries + 2);
        activeEntries = new List<KillFeedEntry>(maxVisibleEntries);

        CreateContainer();
        WarmPool();
    }

    private void OnEnable()
    {
        SessionScoreboard.OnKillEvent += HandleKill;
    }

    private void OnDisable()
    {
        SessionScoreboard.OnKillEvent -= HandleKill;
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Setup
    // ─────────────────────────────────────────────────────────────────────────

    private void CreateContainer()
    {
        GameObject containerObj = new GameObject("KillFeedContainer");
        containerObj.transform.SetParent(transform, false);

        container = containerObj.AddComponent<RectTransform>();
        container.anchorMin = new Vector2(1f, 1f);
        container.anchorMax = new Vector2(1f, 1f);
        container.pivot = new Vector2(1f, 1f);
        container.anchoredPosition = new Vector2(-10f, -10f);
        container.sizeDelta = new Vector2(350f, 300f);

        VerticalLayoutGroup layout = containerObj.AddComponent<VerticalLayoutGroup>();
        layout.childAlignment = TextAnchor.UpperRight;
        layout.spacing = 4f;
        layout.childControlWidth = true;
        layout.childControlHeight = false;
        layout.childForceExpandWidth = true;
        layout.childForceExpandHeight = false;

        ContentSizeFitter fitter = containerObj.AddComponent<ContentSizeFitter>();
        fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
    }

    private void WarmPool()
    {
        for (int i = 0; i < maxVisibleEntries + 2; i++)
        {
            KillFeedEntry entry = CreateEntry();
            entry.gameObject.SetActive(false);
            entryPool.Enqueue(entry);
        }
    }

    private KillFeedEntry CreateEntry()
    {
        GameObject entryObj = new GameObject("KillEntry");
        entryObj.transform.SetParent(container, false);

        Text text = entryObj.AddComponent<Text>();
        text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        text.fontSize = fontSize;
        text.alignment = TextAnchor.MiddleRight;
        text.horizontalOverflow = HorizontalWrapMode.Overflow;
        text.supportRichText = true;
        text.raycastTarget = false;

        RectTransform rt = entryObj.GetComponent<RectTransform>();
        rt.sizeDelta = new Vector2(0f, fontSize + 8f);

        CanvasGroup cg = entryObj.AddComponent<CanvasGroup>();
        cg.blocksRaycasts = false;

        return new KillFeedEntry
        {
            gameObject = entryObj,
            text = text,
            canvasGroup = cg,
            fadeCoroutine = null
        };
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Kill Handling
    // ─────────────────────────────────────────────────────────────────────────

    private void HandleKill(string killerName, string victimName)
    {
        while (activeEntries.Count >= maxVisibleEntries)
        {
            ReturnEntry(0);
        }

        KillFeedEntry entry = GetEntry();
        string killerHex = ColorUtility.ToHtmlStringRGBA(killerColor);
        string victimHex = ColorUtility.ToHtmlStringRGBA(victimColor);
        string sepHex = ColorUtility.ToHtmlStringRGBA(separatorColor);

        entry.text.text = $"<color=#{killerHex}>{killerName}</color>" +
                          $" <color=#{sepHex}>a éliminé</color> " +
                          $"<color=#{victimHex}>{victimName}</color>";

        entry.canvasGroup.alpha = 1f;
        entry.gameObject.SetActive(true);
        entry.gameObject.transform.SetAsLastSibling();

        GameObject trackedObj = entry.gameObject;
        entry.fadeCoroutine = StartCoroutine(FadeAndReturn(trackedObj));
        activeEntries[activeEntries.Count - 1] = entry;
    }

    private KillFeedEntry GetEntry()
    {
        KillFeedEntry entry;
        if (entryPool.Count > 0)
        {
            entry = entryPool.Dequeue();
        }
        else
        {
            entry = CreateEntry();
        }

        activeEntries.Add(entry);
        return entry;
    }

    private void ReturnEntry(int index)
    {
        if (index < 0 || index >= activeEntries.Count) return;

        KillFeedEntry entry = activeEntries[index];
        if (entry.fadeCoroutine != null)
        {
            StopCoroutine(entry.fadeCoroutine);
            entry.fadeCoroutine = null;
        }

        entry.gameObject.SetActive(false);
        entry.canvasGroup.alpha = 0f;
        activeEntries.RemoveAt(index);
        entryPool.Enqueue(entry);
    }

    private IEnumerator FadeAndReturn(GameObject trackedObj)
    {
        yield return new WaitForSeconds(entryDisplayDuration);

        int index = FindEntryIndex(trackedObj);
        if (index < 0) yield break;

        CanvasGroup cg = activeEntries[index].canvasGroup;
        float elapsed = 0f;

        while (elapsed < entryFadeDuration)
        {
            elapsed += Time.deltaTime;
            cg.alpha = Mathf.Lerp(1f, 0f, elapsed / entryFadeDuration);
            yield return null;
        }

        int finalIndex = FindEntryIndex(trackedObj);
        if (finalIndex >= 0)
        {
            ReturnEntry(finalIndex);
        }
    }

    private int FindEntryIndex(GameObject obj)
    {
        for (int i = 0; i < activeEntries.Count; i++)
        {
            if (activeEntries[i].gameObject == obj) return i;
        }
        return -1;
    }
}
