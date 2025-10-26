using UnityEngine;
using System.Collections;
using DG.Tweening;
using Photon.Pun;
using ExitGames.Client.Photon;
using Photon.Realtime;
using TMPro;
public static class NetworkEvents
{
    public const byte FoodAttract = 1;
}

[RequireComponent(typeof(Collider2D))]
[RequireComponent(typeof(TrailRenderer))]
public class FoodBehaviour : MonoBehaviourPun, IOnEventCallback
{
    public FoodData foodData;
    [HideInInspector] public bool isConsumed = false;
    [HideInInspector] public int foodId;

    [Header("Blink Timing Bounds")]
    [SerializeField] private float minBlinkInterval = 0.4f;
    [SerializeField] private float maxBlinkInterval = 0.7f;

    [Header("Attraction Settings")]
    [SerializeField] private float attractSpeed = 10f;

    [Header("Sponsored label (optional)")]
    [Tooltip("Assign a TextMeshPro component (child) that will display sponsor text.")]
    public TextMeshPro labelTMP;
    [Tooltip("Local Z offset for the label if needed.")]
    public float labelZOffset = -0.01f;

    private Vector3 initialScale;
    private Coroutine blinkCoroutine;
    private Collider2D _collider;
    private TrailRenderer _trail;
    private Transform targetHead;
    public bool isAttracting = false;
    private Vector3 _initialTarget;      // initial head-front sent with the event
    private bool _hasInitialTarget = false;
    private int _eaterViewId = -1;

    [SerializeField, Tooltip("Child index on the head transform to attract to. -1 = use head root")]
    private int targetHeadChildIndex = -1; // set in inspector (e.g. 5)

    private Transform _targetHeadPoint = null; // cached point to move toward
    void Awake()
    {
        _collider = GetComponent<Collider2D>();
        _trail = GetComponent<TrailRenderer>();
        float uniform = Random.Range(foodData.minScale.x, foodData.maxScale.x);
        initialScale = Vector3.one * uniform;

        // try auto-find TMP if not set
        if (labelTMP == null)
            labelTMP = GetComponentInChildren<TextMeshPro>(true);
    }

    void OnEnable()
    {
        PhotonNetwork.AddCallbackTarget(this);

        isConsumed = false;
        isAttracting = false;
        targetHead = null;
        _collider.enabled = true;
        _trail.Clear();
        _trail.emitting = false;
        // random color
        var sr = GetComponent<SpriteRenderer>();
        if (sr != null)
        {
            if (foodData != null && foodData.isSponsored)
            {
                // Sponsored orb: use sponsor color and label
                sr.color = foodData.sponsorColor;

                // set trail gradient to use sponsor color (fading alpha)
                var grad = new Gradient();
                grad.colorKeys = new[] {
                    new GradientColorKey(foodData.sponsorColor, 0f),
                    new GradientColorKey(foodData.sponsorColor, 1f)
                };
                grad.alphaKeys = new[] {
                    new GradientAlphaKey(1f, 0f),
                    new GradientAlphaKey(0f, 1f)
                };
                _trail.colorGradient = grad;

                // configure label
                if (labelTMP != null)
                {
                    labelTMP.gameObject.SetActive(true);
                    labelTMP.text = foodData.sponsorName ?? "Sponsor";

                    // pick text color: use configured sponsorTextColor if alpha>0 else auto-choose contrast
                    if (foodData.sponsorTextColor.a > 0f)
                        labelTMP.color = foodData.sponsorTextColor;
                    else
                        labelTMP.color = GetContrastColor(foodData.sponsorColor);

                    // push label slightly forward if needed
                    labelTMP.transform.localPosition = new Vector3(labelTMP.transform.localPosition.x,
                                                                   labelTMP.transform.localPosition.y,
                                                                   labelZOffset);
                }
            }
            else
            {
                // Normal random color (existing behavior)
                Color pick = Color.HSVToRGB(Random.value, Random.Range(0.6f, 1f), Random.Range(0.7f, 1f));
                sr.color = pick;

                var grad = new Gradient();
                grad.colorKeys = new[] {
                    new GradientColorKey(pick, 0f),
                    new GradientColorKey(pick, 1f)
                };
                grad.alphaKeys = new[] {
                    new GradientAlphaKey(1f, 0f),
                    new GradientAlphaKey(0f, 1f)
                };
                _trail.colorGradient = grad;

                if (labelTMP != null)
                    labelTMP.gameObject.SetActive(false);
            }
        }

        transform.localScale = initialScale;

        if (!Application.isMobilePlatform)
        {
            transform.DOScale(initialScale * 1.1f, 1f).SetLoops(-1, LoopType.Yoyo).SetEase(Ease.InOutSine);
            if (foodData != null && foodData.isBlinking)
                blinkCoroutine = StartCoroutine(BlinkEffect());
        }
    }

    void OnDisable()
    {
        PhotonNetwork.RemoveCallbackTarget(this);

        isConsumed = false;
        isAttracting = false;
        targetHead = null;
        _collider.enabled = false;
        _trail.Clear();

        if (blinkCoroutine != null)
        {
            StopCoroutine(blinkCoroutine);
            blinkCoroutine = null;
        }
    }
    void Update()
    {
        if (isAttracting && _targetHeadPoint != null)
        {
            var snake = _targetHeadPoint.GetComponentInParent<SnakeController>() ?? _targetHeadPoint.GetComponent<SnakeController>();
            float headSpeed = 0f;
            if (snake != null) headSpeed = snake.baseSpeed * (snake.isBoosting ? snake.boostMultiplier : 1f);
            float followSpeed = Mathf.Max(attractSpeed, headSpeed * 1.5f);

            transform.position = Vector3.MoveTowards(
                transform.position,
                targetHead.position,
                followSpeed * Time.deltaTime
            );

            if (Vector3.Distance(transform.position, targetHead.position) < 0.1f)
                OnReachedHead();
        }
    }
    private IEnumerator BlinkEffect()
    {
        var sr = GetComponent<SpriteRenderer>();
        while (true)
        {
            sr.enabled = !sr.enabled;
            yield return new WaitForSeconds(Random.Range(minBlinkInterval, maxBlinkInterval));
        }
    }

    /// <summary>
    /// Called by SnakeController via RaiseEvent.
    /// </summary>
    public void BeginAttraction(int snakeHeadViewID)
    {
        if (isAttracting || isConsumed) return;

        var headPV = PhotonView.Find(snakeHeadViewID);
        if (headPV == null) return;

        targetHead = headPV.transform;

        // choose a child or the root safely
        if (targetHeadChildIndex >= 0 && targetHeadChildIndex < targetHead.childCount)
            _targetHeadPoint = targetHead.GetChild(targetHeadChildIndex);
        else
            _targetHeadPoint = targetHead;

        isAttracting = true;
        _collider.enabled = false;
        _trail.Clear();
        _trail.emitting = true;
    }

    private void OnReachedHead()
    {
        if (isConsumed) return;
        isConsumed = true;
        isAttracting = false;
        if (_trail != null)
        {
            _trail.emitting = false;
            _trail.Clear();
        }


        var snake = targetHead.GetComponent<SnakeController>();
        if (snake != null && snake.photonView.IsMine)
        {
            // 1) award locally (owner does UI/score)
            snake.OnOrbArrived(this);

            // 2) notify Master that foodId was eaten (authoritative removal)
            if (GameManager.Instance != null && GameManager.Instance._pv != null)
            {
                GameManager.Instance._pv.RPC(
                    nameof(GameManager.RPC_ReportFoodEaten),
                    RpcTarget.MasterClient,
                    foodId,
                    snake.photonView.ViewID
                );
            }
        }
        else
        {
            // If we're not the owner, just visually clear — Master will later instruct removal
            // keep this object inactive until Master removes it via RPC
        }

    }

    /// <summary>
    /// Listens for our FoodAttract event and kicks off attraction.
    /// </summary>
    public void OnEvent(EventData photonEvent)
    {
        if (photonEvent.Code != NetworkEvents.FoodAttract) return;

        object[] data = (object[])photonEvent.CustomData;
        int evtFoodId = (int)data[0];
        int eaterViewID = (int)data[1];

        if (evtFoodId != foodId) return;

        BeginAttraction(eaterViewID);
    }

    /// <summary>
    /// Called by SnakeController when a collision with a food occurs.
    /// Raises the network event so everyone begins attraction.
    /// </summary>
    public void NotifyBeginAttraction(int eaterViewID)
    {
        if (isConsumed) return;

        var content = new object[] { foodId, eaterViewID };
        var raiseOpts = new RaiseEventOptions { Receivers = ReceiverGroup.All }; // no caching
        PhotonNetwork.RaiseEvent(NetworkEvents.FoodAttract, content, raiseOpts, SendOptions.SendReliable);
    }

    /// <summary>
    /// If the MasterClient directly tells this orb it's consumed.
    /// </summary>
    public void OnConsumed()
    {
        if (isConsumed) return;
        isConsumed = true;
        _collider.enabled = false;
      
    }
    private Color GetContrastColor(Color c)
    {
        // relative luminance approximation
        float l = 0.299f * c.r + 0.587f * c.g + 0.114f * c.b;
        return (l > 0.5f) ? Color.black : Color.white;
    }
}
