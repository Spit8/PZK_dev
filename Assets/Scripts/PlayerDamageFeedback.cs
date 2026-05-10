using UnityEngine;
using UnityEngine.UI;
using System.Collections;
using Mirror;

/// <summary>
/// Système de feedback visuel de dégâts et mort/respawn (Phase 3.2 + 3.4).
/// Attaché au prefab joueur. Tous les effets sont client-only (zéro bande passante).
///
/// Fonctionnalités :
/// - Indicateur directionnel de dégâts (flèches UI pointant vers l'attaquant)
/// - Vignette rouge pulsée lors de la prise de dégâts
/// - Screen shake via offset caméra avec decay
/// - Hit marker pour l'attaquant (confirmation de touche)
/// - Overlay de mort avec countdown de respawn
/// - Transition caméra overhead à la mort, restauration au respawn
/// </summary>
public class PlayerDamageFeedback : NetworkBehaviour
{
    // ─────────────────────────────────────────────────────────────────────────
    // Configuration
    // ─────────────────────────────────────────────────────────────────────────

    [Header("Vignette")]
    [SerializeField] private float vignetteMaxAlpha = 0.6f;
    [SerializeField] private float vignetteFadeDuration = 0.5f;

    [Header("Screen Shake")]
    [SerializeField] private float shakeIntensity = 0.15f;
    [SerializeField] private float shakeDuration = 0.3f;

    [Header("Directional Indicator")]
    [SerializeField] private float indicatorDuration = 1.5f;
    [SerializeField] private float indicatorFadeDuration = 0.5f;

    [Header("Hit Marker")]
    [SerializeField] private float hitMarkerDuration = 0.3f;

    [Header("Death Camera")]
    [SerializeField] private float deathCameraDistance = 12f;
    [SerializeField] private float deathCameraLerpSpeed = 3f;

    // ─────────────────────────────────────────────────────────────────────────
    // UI References (créées dynamiquement au runtime)
    // ─────────────────────────────────────────────────────────────────────────

    private Canvas feedbackCanvas;
    private Image vignetteImage;
    private Image hitMarkerImage;
    private Text deathOverlayText;
    private GameObject deathOverlayPanel;
    private RectTransform[] directionIndicators;
    private CanvasGroup[] directionIndicatorGroups;

    // ─────────────────────────────────────────────────────────────────────────
    // State
    // ─────────────────────────────────────────────────────────────────────────

    private PlayerCameraController cameraController;
    private float savedCameraDistance;
    private bool isDying;

    private Coroutine vignetteCoroutine;
    private Coroutine shakeCoroutine;
    private Coroutine hitMarkerCoroutine;
    private Coroutine deathCoroutine;

    private const int DIRECTION_COUNT = 4;

    // ─────────────────────────────────────────────────────────────────────────
    // Initialization
    // ─────────────────────────────────────────────────────────────────────────

    public override void OnStartLocalPlayer()
    {
        cameraController = GetComponent<PlayerCameraController>();
        CreateFeedbackUI();
    }

    private void OnDestroy()
    {
        if (feedbackCanvas != null)
        {
            Destroy(feedbackCanvas.gameObject);
        }
    }

    private void CreateFeedbackUI()
    {
        GameObject canvasObj = new GameObject("DamageFeedbackCanvas");
        feedbackCanvas = canvasObj.AddComponent<Canvas>();
        feedbackCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
        feedbackCanvas.sortingOrder = 100;
        canvasObj.AddComponent<CanvasScaler>().uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;

        CreateVignette();
        CreateHitMarker();
        CreateDirectionIndicators();
        CreateDeathOverlay();
    }

    private void CreateVignette()
    {
        GameObject vigObj = new GameObject("Vignette");
        vigObj.transform.SetParent(feedbackCanvas.transform, false);

        vignetteImage = vigObj.AddComponent<Image>();
        vignetteImage.color = new Color(0.6f, 0f, 0f, 0f);
        vignetteImage.raycastTarget = false;

        RectTransform rt = vigObj.GetComponent<RectTransform>();
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.sizeDelta = Vector2.zero;
        rt.anchoredPosition = Vector2.zero;
    }

    private void CreateHitMarker()
    {
        GameObject hitObj = new GameObject("HitMarker");
        hitObj.transform.SetParent(feedbackCanvas.transform, false);

        hitMarkerImage = hitObj.AddComponent<Image>();
        hitMarkerImage.color = new Color(1f, 1f, 1f, 0f);
        hitMarkerImage.raycastTarget = false;

        RectTransform rt = hitObj.GetComponent<RectTransform>();
        rt.anchorMin = new Vector2(0.5f, 0.5f);
        rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.sizeDelta = new Vector2(32f, 32f);
        rt.anchoredPosition = Vector2.zero;

        CreateCrosshairGraphic(hitObj);
    }

    private void CreateCrosshairGraphic(GameObject parent)
    {
        float lineLength = 10f;
        float lineWidth = 2f;
        float gap = 4f;

        Vector2[] positions = {
            new Vector2(0, gap + lineLength / 2f),
            new Vector2(0, -(gap + lineLength / 2f)),
            new Vector2(gap + lineLength / 2f, 0),
            new Vector2(-(gap + lineLength / 2f), 0)
        };

        Vector2[] sizes = {
            new Vector2(lineWidth, lineLength),
            new Vector2(lineWidth, lineLength),
            new Vector2(lineLength, lineWidth),
            new Vector2(lineLength, lineWidth)
        };

        for (int i = 0; i < 4; i++)
        {
            GameObject line = new GameObject("HitLine_" + i);
            line.transform.SetParent(parent.transform, false);

            Image img = line.AddComponent<Image>();
            img.color = Color.white;
            img.raycastTarget = false;

            RectTransform rt = line.GetComponent<RectTransform>();
            rt.anchorMin = new Vector2(0.5f, 0.5f);
            rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = sizes[i];
            rt.anchoredPosition = positions[i];
        }
    }

    private void CreateDirectionIndicators()
    {
        directionIndicators = new RectTransform[DIRECTION_COUNT];
        directionIndicatorGroups = new CanvasGroup[DIRECTION_COUNT];

        float[] rotations = { 0f, 180f, 90f, -90f };
        float offset = 200f;
        Vector2[] anchors = {
            new Vector2(0.5f, 1f),
            new Vector2(0.5f, 0f),
            new Vector2(1f, 0.5f),
            new Vector2(0f, 0.5f)
        };
        Vector2[] offsets = {
            new Vector2(0, -offset),
            new Vector2(0, offset),
            new Vector2(-offset, 0),
            new Vector2(offset, 0)
        };

        for (int i = 0; i < DIRECTION_COUNT; i++)
        {
            GameObject indObj = new GameObject("DmgIndicator_" + i);
            indObj.transform.SetParent(feedbackCanvas.transform, false);

            Image img = indObj.AddComponent<Image>();
            img.color = new Color(1f, 0.15f, 0.1f, 0f);
            img.raycastTarget = false;

            CanvasGroup cg = indObj.AddComponent<CanvasGroup>();
            cg.alpha = 0f;
            cg.blocksRaycasts = false;
            directionIndicatorGroups[i] = cg;

            RectTransform rt = indObj.GetComponent<RectTransform>();
            rt.anchorMin = anchors[i];
            rt.anchorMax = anchors[i];
            rt.sizeDelta = new Vector2(60f, 30f);
            rt.anchoredPosition = offsets[i];
            rt.localEulerAngles = new Vector3(0, 0, rotations[i]);
            directionIndicators[i] = rt;
        }
    }

    private void CreateDeathOverlay()
    {
        deathOverlayPanel = new GameObject("DeathOverlay");
        deathOverlayPanel.transform.SetParent(feedbackCanvas.transform, false);

        Image bg = deathOverlayPanel.AddComponent<Image>();
        bg.color = new Color(0f, 0f, 0f, 0.5f);
        bg.raycastTarget = false;

        RectTransform bgRt = deathOverlayPanel.GetComponent<RectTransform>();
        bgRt.anchorMin = Vector2.zero;
        bgRt.anchorMax = Vector2.one;
        bgRt.sizeDelta = Vector2.zero;

        GameObject textObj = new GameObject("DeathText");
        textObj.transform.SetParent(deathOverlayPanel.transform, false);

        deathOverlayText = textObj.AddComponent<Text>();
        deathOverlayText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        deathOverlayText.fontSize = 36;
        deathOverlayText.color = Color.white;
        deathOverlayText.alignment = TextAnchor.MiddleCenter;
        deathOverlayText.raycastTarget = false;

        RectTransform textRt = textObj.GetComponent<RectTransform>();
        textRt.anchorMin = new Vector2(0.2f, 0.3f);
        textRt.anchorMax = new Vector2(0.8f, 0.7f);
        textRt.sizeDelta = Vector2.zero;

        deathOverlayPanel.SetActive(false);
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Public API — Appelé par PlayerHealth et WeaponSystem
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>Affiche la vignette rouge et le screen shake (victime).</summary>
    public void OnDamageReceived(int amount, Vector3 hitDirection)
    {
        if (!isLocalPlayer) return;

        ShowVignette();
        ShakeCamera();
        ShowDirectionIndicator(hitDirection);
    }

    /// <summary>Affiche le hit marker au centre (attaquant).</summary>
    public void ShowHitMarker()
    {
        if (!isLocalPlayer) return;

        if (hitMarkerCoroutine != null) StopCoroutine(hitMarkerCoroutine);
        hitMarkerCoroutine = StartCoroutine(HitMarkerSequence());
    }

    /// <summary>Active l'overlay de mort avec countdown.</summary>
    public void ShowDeathOverlay(float respawnDelay)
    {
        if (!isLocalPlayer) return;

        isDying = true;

        if (cameraController != null)
        {
            savedCameraDistance = cameraController.currentDistance;
        }

        if (deathCoroutine != null) StopCoroutine(deathCoroutine);
        deathCoroutine = StartCoroutine(DeathOverlaySequence(respawnDelay));
    }

    /// <summary>Cache l'overlay de mort et restaure la caméra.</summary>
    public void HideDeathOverlay()
    {
        if (!isLocalPlayer) return;

        isDying = false;

        if (deathCoroutine != null)
        {
            StopCoroutine(deathCoroutine);
            deathCoroutine = null;
        }

        if (deathOverlayPanel != null) deathOverlayPanel.SetActive(false);

        if (cameraController != null)
        {
            cameraController.currentDistance = savedCameraDistance;
        }
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Vignette
    // ─────────────────────────────────────────────────────────────────────────

    private void ShowVignette()
    {
        if (vignetteImage == null) return;
        if (vignetteCoroutine != null) StopCoroutine(vignetteCoroutine);
        vignetteCoroutine = StartCoroutine(VignetteSequence());
    }

    private IEnumerator VignetteSequence()
    {
        vignetteImage.color = new Color(0.6f, 0f, 0f, vignetteMaxAlpha);

        float elapsed = 0f;
        while (elapsed < vignetteFadeDuration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / vignetteFadeDuration;
            float alpha = Mathf.Lerp(vignetteMaxAlpha, 0f, t);
            vignetteImage.color = new Color(0.6f, 0f, 0f, alpha);
            yield return null;
        }

        vignetteImage.color = new Color(0.6f, 0f, 0f, 0f);
        vignetteCoroutine = null;
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Screen Shake
    // ─────────────────────────────────────────────────────────────────────────

    private void ShakeCamera()
    {
        if (shakeCoroutine != null) StopCoroutine(shakeCoroutine);
        shakeCoroutine = StartCoroutine(ShakeSequence());
    }

    private IEnumerator ShakeSequence()
    {
        if (cameraController == null || cameraController.playerCamera == null)
        {
            yield break;
        }

        Transform camTransform = cameraController.playerCamera.transform;
        Vector3 originalLocalPos = camTransform.localPosition;

        float elapsed = 0f;
        while (elapsed < shakeDuration)
        {
            elapsed += Time.deltaTime;
            float decay = 1f - (elapsed / shakeDuration);
            float offsetX = Random.Range(-shakeIntensity, shakeIntensity) * decay;
            float offsetY = Random.Range(-shakeIntensity, shakeIntensity) * decay;

            camTransform.localPosition = originalLocalPos + new Vector3(offsetX, offsetY, 0f);
            yield return null;
        }

        camTransform.localPosition = originalLocalPos;
        shakeCoroutine = null;
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Directional Indicator
    // ─────────────────────────────────────────────────────────────────────────

    private void ShowDirectionIndicator(Vector3 hitDirection)
    {
        if (directionIndicatorGroups == null) return;

        Vector3 localDir = transform.InverseTransformDirection(hitDirection);
        float angle = Mathf.Atan2(localDir.x, localDir.z) * Mathf.Rad2Deg;

        // 0=front(up), 1=back(down), 2=right, 3=left
        int index;
        if (angle > -45f && angle <= 45f) index = 0;
        else if (angle > 45f && angle <= 135f) index = 2;
        else if (angle < -45f && angle >= -135f) index = 3;
        else index = 1;

        StartCoroutine(DirectionIndicatorSequence(index));
    }

    private IEnumerator DirectionIndicatorSequence(int index)
    {
        CanvasGroup cg = directionIndicatorGroups[index];
        cg.alpha = 1f;

        float showTime = indicatorDuration - indicatorFadeDuration;
        if (showTime > 0f) yield return new WaitForSeconds(showTime);

        float elapsed = 0f;
        while (elapsed < indicatorFadeDuration)
        {
            elapsed += Time.deltaTime;
            cg.alpha = Mathf.Lerp(1f, 0f, elapsed / indicatorFadeDuration);
            yield return null;
        }

        cg.alpha = 0f;
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Hit Marker
    // ─────────────────────────────────────────────────────────────────────────

    private IEnumerator HitMarkerSequence()
    {
        if (hitMarkerImage == null) yield break;

        hitMarkerImage.color = Color.white;

        foreach (Image child in hitMarkerImage.GetComponentsInChildren<Image>())
        {
            child.color = Color.white;
        }

        float elapsed = 0f;
        while (elapsed < hitMarkerDuration)
        {
            elapsed += Time.deltaTime;
            float alpha = Mathf.Lerp(1f, 0f, elapsed / hitMarkerDuration);
            hitMarkerImage.color = new Color(1f, 1f, 1f, alpha);

            foreach (Image child in hitMarkerImage.GetComponentsInChildren<Image>())
            {
                child.color = new Color(1f, 1f, 1f, alpha);
            }

            yield return null;
        }

        hitMarkerImage.color = new Color(1f, 1f, 1f, 0f);
        hitMarkerCoroutine = null;
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Death Overlay + Camera (Phase 3.4)
    // ─────────────────────────────────────────────────────────────────────────

    private IEnumerator DeathOverlaySequence(float respawnDelay)
    {
        if (deathOverlayPanel != null)
        {
            deathOverlayPanel.SetActive(true);
        }

        float remaining = respawnDelay;
        while (remaining > 0f && isDying)
        {
            if (deathOverlayText != null)
            {
                int secs = Mathf.CeilToInt(remaining);
                deathOverlayText.text = "MORT\nRespawn dans " + secs.ToString() + "s";
            }

            if (cameraController != null)
            {
                cameraController.currentDistance = Mathf.Lerp(
                    cameraController.currentDistance,
                    deathCameraDistance,
                    Time.deltaTime * deathCameraLerpSpeed);
            }

            remaining -= Time.deltaTime;
            yield return null;
        }

        deathCoroutine = null;
    }

    private void LateUpdate()
    {
        if (!isLocalPlayer) return;
        if (!isDying) return;

        if (cameraController != null)
        {
            cameraController.currentDistance = Mathf.Lerp(
                cameraController.currentDistance,
                deathCameraDistance,
                Time.deltaTime * deathCameraLerpSpeed);
        }
    }
}
