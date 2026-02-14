using UnityEngine;

/// <summary>
/// Script pour afficher les axes X, Y, Z de l'environnement dans l'éditeur Unity
/// Attachez ce script à n'importe quel GameObject dans votre scène
/// </summary>
public class EnvironmentAxesGizmo : MonoBehaviour
{
    [Header("Paramètres des axes")]
    [Tooltip("Longueur des axes affichés")]
    public float axisLength = 5f;

    [Tooltip("Épaisseur des lignes des axes")]
    public float axisThickness = 0.1f;

    [Tooltip("Utiliser des cylindres pleins au lieu de lignes")]
    public bool useSolidAxes = true;

    [Header("Couleurs des axes")]
    [Tooltip("Couleur de l'axe X (Rouge par défaut)")]
    public Color xAxisColor = Color.red;

    [Tooltip("Couleur de l'axe Y (Vert par défaut)")]
    public Color yAxisColor = Color.green;

    [Tooltip("Couleur de l'axe Z (Bleu par défaut)")]
    public Color zAxisColor = Color.blue;

    [Header("Options d'affichage")]
    [Tooltip("Afficher les axes à partir de l'origine du monde (0,0,0)")]
    public bool useWorldOrigin = true;

    [Tooltip("Point d'origine personnalisé (utilisé seulement si useWorldOrigin et useCustomOrigin sont false)")]
    public Vector3 customOrigin = Vector3.zero;

    [Tooltip("Utiliser le point d'origine personnalisé au lieu de la position du GameObject")]
    public bool useCustomOrigin = false;

    [Tooltip("Afficher les flèches aux extrémités")]
    public bool showArrows = true;

    [Tooltip("Taille des flèches")]
    public float arrowSize = 0.3f;

    [Tooltip("Épaisseur des flèches (rayon de la base du cône)")]
    public float arrowThickness = 0.15f;

    [Tooltip("Afficher les labels des axes")]
    public bool showLabels = true;

    /// <summary>
    /// Dessine les gizmos dans l'éditeur
    /// </summary>
    private void OnDrawGizmos()
    {
        DrawEnvironmentAxes();
    }

    /// <summary>
    /// Dessine les trois axes de l'environnement
    /// </summary>
    private void DrawEnvironmentAxes()
    {
        // Détermine le point d'origine selon les paramètres
        Vector3 origin;

        if (useWorldOrigin)
        {
            origin = Vector3.zero;
        }
        else if (useCustomOrigin)
        {
            origin = customOrigin;
        }
        else
        {
            origin = transform.position;
        }

        // Axe X (Rouge)
        DrawAxis(origin, Vector3.right, xAxisColor, "X");

        // Axe Y (Vert)
        DrawAxis(origin, Vector3.up, yAxisColor, "Y");

        // Axe Z (Bleu)
        DrawAxis(origin, Vector3.forward, zAxisColor, "Z");
    }

    /// <summary>
    /// Dessine un axe individuel avec option de flèche et label
    /// </summary>
    private void DrawAxis(Vector3 origin, Vector3 direction, Color color, string label)
    {
        Gizmos.color = color;
        Vector3 end = origin + direction * axisLength;

        if (useSolidAxes)
        {
            // Dessine un cylindre plein pour l'axe
            DrawCylinder(origin, end, axisThickness, color);
        }
        else
        {
            // Dessine la ligne principale avec épaisseur simulée
            int segments = Mathf.Max(1, Mathf.RoundToInt(axisThickness * 2));
            float offset = axisThickness * 0.01f;

            // Ligne principale
            Gizmos.DrawLine(origin, end);

            // Lignes supplémentaires pour simuler l'épaisseur
            if (axisThickness > 1f)
            {
                Vector3 perpendicular1 = Vector3.Cross(direction, Vector3.up).normalized;
                if (perpendicular1 == Vector3.zero)
                    perpendicular1 = Vector3.Cross(direction, Vector3.right).normalized;

                Vector3 perpendicular2 = Vector3.Cross(direction, perpendicular1).normalized;

                for (int i = 1; i <= segments; i++)
                {
                    float dist = offset * i;
                    Gizmos.DrawLine(origin + perpendicular1 * dist, end + perpendicular1 * dist);
                    Gizmos.DrawLine(origin - perpendicular1 * dist, end - perpendicular1 * dist);
                    Gizmos.DrawLine(origin + perpendicular2 * dist, end + perpendicular2 * dist);
                    Gizmos.DrawLine(origin - perpendicular2 * dist, end - perpendicular2 * dist);
                }
            }
        }

        // Dessine une sphère à l'origine
        float sphereRadius = useSolidAxes ? axisThickness * 1.5f : axisThickness * 0.02f;
        Gizmos.DrawSphere(origin, sphereRadius);

        // Dessine la flèche à l'extrémité
        if (showArrows)
        {
            DrawArrow(end, direction, color);
        }

        // Affiche le label
        if (showLabels)
        {
#if UNITY_EDITOR
            UnityEditor.Handles.Label(end + direction * 0.2f, label, GetLabelStyle(color));
#endif
        }
    }

    /// <summary>
    /// Dessine un cylindre plein entre deux points
    /// </summary>
    private void DrawCylinder(Vector3 start, Vector3 end, float radius, Color color)
    {
        Gizmos.color = color;

        Vector3 direction = (end - start).normalized;
        float length = Vector3.Distance(start, end);
        Vector3 center = start + direction * (length / 2f);

        // Crée une matrice pour orienter le cylindre
        Quaternion rotation = Quaternion.LookRotation(direction);

        // Sauvegarde la matrice actuelle
        Matrix4x4 oldMatrix = Gizmos.matrix;

        // Applique la rotation et la position
        Gizmos.matrix = Matrix4x4.TRS(center, rotation, new Vector3(radius * 2, radius * 2, length));

        // Dessine un cube qui sera transformé en cylindre visuellement
        // Note: Gizmos n'a pas de DrawCylinder, donc on utilise un mesh de cylindre
        Gizmos.DrawCube(Vector3.zero, Vector3.one);

        // Restaure la matrice
        Gizmos.matrix = oldMatrix;

        // Pour un meilleur rendu, on peut aussi dessiner avec des segments
        DrawCylinderMesh(start, end, radius, color);
    }

    /// <summary>
    /// Dessine un cylindre avec des segments pour un meilleur rendu
    /// </summary>
    private void DrawCylinderMesh(Vector3 start, Vector3 end, float radius, Color color)
    {
        Gizmos.color = color;

        Vector3 direction = (end - start).normalized;

        // Trouve un vecteur perpendiculaire
        Vector3 perpendicular = Vector3.Cross(direction, Vector3.up).normalized;
        if (perpendicular == Vector3.zero)
            perpendicular = Vector3.Cross(direction, Vector3.right).normalized;

        Vector3 perpendicular2 = Vector3.Cross(direction, perpendicular).normalized;

        // Nombre de segments pour former le cylindre
        int segments = 12;
        Vector3[] startPoints = new Vector3[segments];
        Vector3[] endPoints = new Vector3[segments];

        // Calcule les points du cercle
        for (int i = 0; i < segments; i++)
        {
            float angle = (float)i / segments * Mathf.PI * 2;
            Vector3 offset = (Mathf.Cos(angle) * perpendicular + Mathf.Sin(angle) * perpendicular2) * radius;

            startPoints[i] = start + offset;
            endPoints[i] = end + offset;
        }

        // Dessine les lignes du cylindre
        for (int i = 0; i < segments; i++)
        {
            int next = (i + 1) % segments;

            // Lignes longitudinales
            Gizmos.DrawLine(startPoints[i], endPoints[i]);

            // Cercles aux extrémités
            Gizmos.DrawLine(startPoints[i], startPoints[next]);
            Gizmos.DrawLine(endPoints[i], endPoints[next]);
        }
    }
    /// <summary>
    /// Dessine une flèche à l'extrémité de l'axe
    /// </summary>
    private void DrawArrow(Vector3 position, Vector3 direction, Color color)
    {
        Gizmos.color = color;

        // Crée deux vecteurs perpendiculaires pour former la pointe de la flèche
        Vector3 right = Vector3.Cross(direction, Vector3.up).normalized;
        if (right == Vector3.zero)
            right = Vector3.Cross(direction, Vector3.right).normalized;

        Vector3 up = Vector3.Cross(direction, right).normalized;

        if (useSolidAxes)
        {
            // Dessine un cône plein pour la flèche
            DrawCone(position, direction, arrowSize, arrowThickness, color);
        }
        else
        {
            // Version lignes de la flèche
            float adjustedArrowSize = arrowSize * (1f + axisThickness * 0.1f);

            Vector3 arrowTip1 = position - direction * adjustedArrowSize + right * adjustedArrowSize * 0.5f;
            Vector3 arrowTip2 = position - direction * adjustedArrowSize + up * adjustedArrowSize * 0.5f;
            Vector3 arrowTip3 = position - direction * adjustedArrowSize - right * adjustedArrowSize * 0.5f;
            Vector3 arrowTip4 = position - direction * adjustedArrowSize - up * adjustedArrowSize * 0.5f;

            Gizmos.DrawLine(position, arrowTip1);
            Gizmos.DrawLine(position, arrowTip2);
            Gizmos.DrawLine(position, arrowTip3);
            Gizmos.DrawLine(position, arrowTip4);

            Gizmos.DrawLine(arrowTip1, arrowTip2);
            Gizmos.DrawLine(arrowTip2, arrowTip3);
            Gizmos.DrawLine(arrowTip3, arrowTip4);
            Gizmos.DrawLine(arrowTip4, arrowTip1);
        }
    }

    /// <summary>
    /// Dessine un cône plein pour la flèche
    /// </summary>
    private void DrawCone(Vector3 tip, Vector3 direction, float height, float radius, Color color)
    {
        Gizmos.color = color;

        Vector3 baseCenter = tip - direction * height;

        // Trouve un vecteur perpendiculaire
        Vector3 perpendicular = Vector3.Cross(direction, Vector3.up).normalized;
        if (perpendicular == Vector3.zero)
            perpendicular = Vector3.Cross(direction, Vector3.right).normalized;

        Vector3 perpendicular2 = Vector3.Cross(direction, perpendicular).normalized;

        // Nombre de segments pour former le cône
        int segments = 12;
        Vector3[] basePoints = new Vector3[segments];

        // Calcule les points du cercle de base
        for (int i = 0; i < segments; i++)
        {
            float angle = (float)i / segments * Mathf.PI * 2;
            Vector3 offset = (Mathf.Cos(angle) * perpendicular + Mathf.Sin(angle) * perpendicular2) * radius;
            basePoints[i] = baseCenter + offset;
        }

        // Dessine les lignes du cône
        for (int i = 0; i < segments; i++)
        {
            int next = (i + 1) % segments;

            // Lignes de la pointe à la base
            Gizmos.DrawLine(tip, basePoints[i]);

            // Cercle de base
            Gizmos.DrawLine(basePoints[i], basePoints[next]);
        }

        // Remplit partiellement le cône pour le rendre plus visible
        for (int i = 0; i < segments; i++)
        {
            Vector3 midPoint = (tip + basePoints[i]) / 2f;
            Gizmos.DrawLine(midPoint, baseCenter);
        }
    }

#if UNITY_EDITOR
    /// <summary>
    /// Retourne un style pour les labels avec la couleur appropriée
    /// </summary>
    private GUIStyle GetLabelStyle(Color color)
    {
        GUIStyle style = new GUIStyle();
        style.normal.textColor = color;
        style.fontSize = 14;
        style.fontStyle = FontStyle.Bold;
        return style;
    }
#endif
}