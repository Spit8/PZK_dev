using Mirror;
using UnityEngine;

public class PZKNetworkManager : NetworkManager
{
    public override void OnStartServer()
    {
        base.OnStartServer();
    }

    public override void OnServerAddPlayer(NetworkConnectionToClient connection)
    {
        base.OnServerAddPlayer(connection);
    }

    public override void OnServerDisconnect(NetworkConnectionToClient connection)
    {
        if (connection != null && connection.identity != null)
        {
            uint disconnectedNetId = connection.identity.netId;
            ReleaseItemsHeldBy(disconnectedNetId);
        }

        base.OnServerDisconnect(connection);
    }

    private void ReleaseItemsHeldBy(uint holderNetId)
    {
        if (!NetworkServer.active)
        {
            return;
        }

        PickupItem[] pickupItems = FindObjectsByType<PickupItem>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
        for (int i = 0; i < pickupItems.Length; i++)
        {
            PickupItem pickupItem = pickupItems[i];
            if (pickupItem != null)
            {
                pickupItem.ServerForceReleaseIfHeldBy(holderNetId);
            }
        }
    }
}
