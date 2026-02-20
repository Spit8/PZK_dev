using UnityEngine;

public class PlayerPush : MonoBehaviour
{
    public float pushForce = 10f;
    public LayerMask pushLayers;

    private PlayerMovement playerMovement;

    void Start()
    {
        playerMovement = GetComponentInParent<PlayerMovement>();
    }

    void OnTriggerEnter(Collider other)
    {
        if ((pushLayers & (1 << other.gameObject.layer)) == 0) return;

        Rigidbody rb = other.attachedRigidbody;
        if (rb == null || rb.isKinematic) return;

        Vector3 pushDir = other.transform.position - transform.position;
        pushDir.y = 0f;
        pushDir.Normalize();

        rb.AddForce(pushDir * pushForce, ForceMode.Impulse); // Impulse au lieu de Force
    }
}