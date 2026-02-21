using Mirror;
using UnityEngine;


using UnityEngine;

[RequireComponent(typeof(AudioSource))]
public class FallingImpact : MonoBehaviour
{
    public AudioClip groundImpactClip;
    public float minImpactVelocity = 2f;
    private AudioSource audioSource;

    void Awake()
    {
        audioSource = GetComponent<AudioSource>();
    }

    void OnCollisionEnter(Collision collision)
    {
        if (!collision.gameObject.CompareTag("Ground"))
            return;

        float impactForce = collision.relativeVelocity.magnitude;
        if (impactForce < minImpactVelocity)
            return;

        audioSource.PlayOneShot(groundImpactClip);
    }
}

/*
[RequireComponent(typeof(AudioSource))]
public class FallingImpact : NetworkBehaviour
{
    [Header("Impact Sound")]
    public AudioClip groundImpactClip;

    [Header("Audio Settings")]
    public float hearingRadius = 30f;
    public float minImpactVelocity = 2f;

    private AudioSource audioSource;

    void Awake()
    {
        audioSource = GetComponent<AudioSource>();
    }

    [ServerCallback]
    void OnCollisionEnter(Collision collision)
    {
        if (!collision.gameObject.CompareTag("Ground"))
            return;

        float impactForce = collision.relativeVelocity.magnitude;

        if (impactForce < minImpactVelocity)
            return;

        NotifyNearbyPlayers();
    }

    [Server]
    void NotifyNearbyPlayers()
    {
        Vector3 soundPosition = transform.position;

        foreach (NetworkConnectionToClient conn in NetworkServer.connections.Values)
        {
            if (conn.identity == null)
                continue;

            float distance = Vector3.Distance(
                conn.identity.transform.position,
                soundPosition
            );

            if (distance <= hearingRadius)
            {
                TargetPlayImpact(conn);
            }
        }
    }

    [TargetRpc]
    void TargetPlayImpact(NetworkConnection target)
    {
        Debug.Log("Joue le son !");
        if (groundImpactClip == null)
            return;

        audioSource.pitch = Random.Range(0.95f, 1.05f);
        audioSource.PlayOneShot(groundImpactClip);
    }
}*/