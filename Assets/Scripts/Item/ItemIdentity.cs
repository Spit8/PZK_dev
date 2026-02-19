using Mirror;

public class ItemIdentity : NetworkBehaviour
{
    [SyncVar] public int itemId; // ID défini sur le prefab ou par le serveur
}