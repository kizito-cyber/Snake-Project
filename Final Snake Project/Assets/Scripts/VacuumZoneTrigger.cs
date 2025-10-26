using UnityEngine;
using Photon.Pun;
using ExitGames.Client.Photon;
using Photon.Realtime;

[RequireComponent(typeof(CircleCollider2D))]
public class VacuumZoneTrigger : MonoBehaviourPun
{
    [Header("How far out the vacuum should reach (in world units)")]
    [SerializeField] private float vacuumRadius = 1.3f;

    private SnakeController _snake;
    private CircleCollider2D _zone;

    private void Awake()
    {
        // parent SnakeController
        _snake = GetComponentInParent<SnakeController>();
        if (_snake == null)
        {
            Debug.LogError("VacuumZoneTrigger: no SnakeController found in parents!");
            enabled = false;
            return;
        }

        // our trigger collider
        _zone = GetComponent<CircleCollider2D>();
        _zone.isTrigger = true;
        _zone.radius = vacuumRadius;
    }

    private void OnValidate()
    {
        // keep editor changes in sync
        if (_zone != null)
            _zone.radius = vacuumRadius;
    }

    private void Update()
    {
        // if you ever want to grow the zone dynamically, it'll update
        if (_zone.radius != vacuumRadius)
            _zone.radius = vacuumRadius;
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        // only local snakes send events
        if (!_snake.photonView.IsMine)
            return;

        if (!other.CompareTag("Food"))
            return;

        var fb = other.GetComponent<FoodBehaviour>();
        if (fb == null || fb.isConsumed || fb.isAttracting)
            return;

        if (fb.foodId == 0)
        {
            Debug.LogWarning($"VacuumZone: food has id=0 at {other.transform.position}. Skipping attract (race?)");
            // Optionally still start local attract, but safer to skip until id assigned
            // fb.BeginAttraction(_snake.photonView.ViewID);
            return;
        }

        fb.BeginAttraction(_snake.photonView.ViewID);

        // 2) Broadcast the attraction to everyone else (DO NOT cache this event)
        object[] content = new object[] { fb.foodId, _snake.photonView.ViewID };
        var raiseOpts = new RaiseEventOptions
        {
            Receivers = ReceiverGroup.All,
            CachingOption = EventCaching.DoNotCache // IMPORTANT: don't cache
        };
        var sendOpts = new SendOptions { Reliability = true };

        PhotonNetwork.RaiseEvent(NetworkEvents.FoodAttract, content, raiseOpts, sendOpts);

        Debug.Log($"VacuumZone: raised FoodAttract for id={fb.foodId} eater={_snake.photonView.ViewID}");
    }
}
