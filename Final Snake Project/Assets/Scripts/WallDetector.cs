using Photon.Pun;
using UnityEngine;

[RequireComponent(typeof(PhotonView))]
public class WallDetector : MonoBehaviourPun
{
    // Prevent handling the same death multiple times
    private bool _handled = false;

    void Awake()
    {
        int vacuum = LayerMask.NameToLayer("VacuumZone");
        int wall = LayerMask.NameToLayer("Wall");
        int head = LayerMask.NameToLayer("SnakeMain");

        // vacuum should NOT collide with wall or head
        Physics2D.IgnoreLayerCollision(vacuum, wall, true);
        Physics2D.IgnoreLayerCollision(vacuum, head, true);

        // head should collide with wall (leave true)
        Physics2D.IgnoreLayerCollision(head, wall, false);
    }


    private void OnTriggerEnter2D(Collider2D other)
    {
        // Only the owner of this snake should decide/announce its death.
        if (!photonView.IsMine) return;

        // Ignore if we've already handled death
        if (_handled) return;

        // Check tag of the collider. Set your wall's tag to "Wall".
        if (other.CompareTag("Wall"))
        {
            _handled = true;

            var snake = GetComponent<SnakeController>();
            if (snake == null)
            {
                Debug.LogWarning("WallDetector: no SnakeController found on same GameObject!");
                return;
            }

            // 1) Tell the MasterClient to resolve this player's death (authoritative)
            //    This calls RPC_MasterHandleDeath(int victimId) on the MasterClient.
            snake.photonView.RPC(nameof(snake.RPC_MasterHandleDeath),
                                 RpcTarget.MasterClient,
                                 snake.photonView.ViewID);

            // 2) Handle local death immediately for responsive UI/behavior
            snake.HandleLocalDeath("Hit the wall");

            // optional: disable head collider so it doesn't trigger other things while dying
            var headCol = GetComponent<Collider2D>();
            if (headCol != null) headCol.enabled = false;
        }
    }

    // Defensive: if you want to allow respawn or reuse disable the handled flag there.
    public void ResetHandled() => _handled = false;
}
