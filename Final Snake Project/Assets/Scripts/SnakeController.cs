using Photon.Pun;
using UnityEngine;
using System.Collections.Generic;
using UnityEngine.SocialPlatforms;
using System.Collections;


public class SnakeController : MonoBehaviourPun
{
    [Header("Movement Settings")]
    public float baseSpeed = 5f;
    public float boostMultiplier = 3f;
    public bool isBoosting;

    // new: how far from the head you must push before turning begins
    [SerializeField] private float deadZoneRadius = 0.05f;
    // new: maximum degrees per second you can turn
    [SerializeField] private float turnSpeed = 720f;

    [Header("Trail Settings")]
    [SerializeField] private float gap = 0.2f;           // Minimum distance between recorded points
    [SerializeField] private int maxPositions = 50;   // How many points to keep

    [SerializeField] private float baseGap = 0.15f;     // gap when snake is small
    [SerializeField] private float maxGap = 0.6f;      // gap when snake is huge


    [Header("Visual / Scoring")]
    [SerializeField] private SpriteRenderer snakeRenderer;
    [SerializeField] private GameObject spinningRim;

    private List<Vector3> positionsHistory = new List<Vector3>();

    [Header("Trail Renderers")]
    [SerializeField] private LineRenderer outlineLine;
    [SerializeField] private LineRenderer fillLine;

    [SerializeField] private Material outlineMaterial;
    [SerializeField] private Material fillMaterial;


    // Remotely received head positions
    private List<Vector3> remotePositions = new List<Vector3>();

    public int currentScore = 0;

    [SerializeField] private int initialFood = 16;

    private EdgeCollider2D _trailCollider;

    private Collider2D _headCollider;

    [SerializeField] private float colliderRadius = 0.12f;

    private List<CircleCollider2D> _circleColliders = new List<CircleCollider2D>();

    [SerializeField, Tooltip("Spawn one leftover every Nth trail point")]
    private int leftoverInterval = 4;  // try 2 (half), 3 (one third), etc.

    [SerializeField] private int maxInterpolationPoints = 20;

    private float _colliderTimer = 0f;
    private const float ColliderUpdateInterval = 0.1f;

    [Header("Head Outline")]
    [SerializeField] private Renderer headRenderer;  // your sprite�s MeshRenderer or SpriteRenderer
    [SerializeField] private string headOutlineProp = "_SolidOutline";

    private Vector3 _remotePos;
    private Quaternion _remoteRot;
    private bool _gotRemote;

    [Header("Growth / Width Settings")]
    [Tooltip("How many world?units to add to the trail width for each food eaten")]
    [SerializeField] private float widthPerFood = 0.05f;

    [Tooltip("Minimum body width (when score=0)")]
    [SerializeField] private float minBodyWidth = 0.5f;

    [Tooltip("Maximum body width (cap after tons of food, if you like)")]
    [SerializeField] private float maxBodyWidth = 1.5f;

    [Tooltip("Head?to?body width multiplier (e.g. 1.2 means head is always 20% larger than body)")]
    [SerializeField] private float headSizeMultiplier = 1.2f;

    // inside SnakeController (class members, e.g. right below currentScore)
    private float currentBodyWidth;

    private Vector3 _lastRemotePos;
    private Vector3 _remoteVelocity;
    [SerializeField] private float vacuumRadius = 1.5f;

    float realGap;
    [Header("Icon Stream")]
    [SerializeField] private LineRenderer iconLine;

    // cache last drawn points for smoothing
    private List<Vector3> _prevDrawPts = null;
    // how quickly the trail �snaps� to the new spline (higher = snappier, lower = smoother)
    [SerializeField] private float smoothSpeed = 10f;


    [Header("Head Offset")]
    [SerializeField, Tooltip("How far ahead of the transform to draw the head/trail start")]
    private float headOffset = 0.2f;

    [Header("Spawn Invulnerability")]
    [SerializeField, Tooltip("Seconds of invulnerability after spawn")]
    private float spawnInvulDuration = 1f;
    private bool _spawnInvulnerable = true;

    public float HeadOffset => headOffset;

    /// <summary>
    /// Generates a smooth Catmull�Rom spline through your position list.
    /// You'll get more points (and therefore a smoother line) without adding real physics cost.
    /// </summary>
    private List<Vector3> GetSmoothedPoints(List<Vector3> pts, int subdivisions = 5)
    {
        var smooth = new List<Vector3>();
        if (pts.Count < 2)
        {
            smooth.AddRange(pts);
            return smooth;
        }

        // For each segment, pick P0,P1,P2,P3 (clamp edges)
        for (int i = 0; i < pts.Count - 1; i++)
        {
            Vector3 p0 = i == 0 ? pts[i] : pts[i - 1];
            Vector3 p1 = pts[i];
            Vector3 p2 = pts[i + 1];
            Vector3 p3 = (i + 2 < pts.Count) ? pts[i + 2] : pts[i + 1];

            for (int s = 0; s < subdivisions; s++)
            {
                float t = s / (float)subdivisions;
                // Catmull�Rom basis
                Vector3 A = 2f * p1;
                Vector3 B = p2 - p0;
                Vector3 C = 2f * p0 - 5f * p1 + 4f * p2 - p3;
                Vector3 D = -p0 + 3f * p1 - 3f * p2 + p3;

                Vector3 pos = 0.5f * (A + (B * t) + (C * t * t) + (D * t * t * t));
                smooth.Add(pos);
            }
        }
        // finally add last point
        smooth.Add(pts[pts.Count - 1]);
        return smooth;
    }


    void Awake()
    {

        _headCollider = GetComponent<Collider2D>();

        SetupLineRenderer(outlineLine, 0.8f, Color.white, "Outline");
        SetupLineRenderer(fillLine, 0.7f, Color.black, "Fill");
        //fillLine.sortingOrder = outlineLine.sortingOrder + 1;

        if (outlineMaterial != null) outlineLine.material = outlineMaterial;
        if (fillMaterial != null) fillLine.material = fillMaterial;


        var go = new GameObject($"{name}_TrailCollider");
        go.transform.SetParent(transform, worldPositionStays: false);
        _trailCollider = go.AddComponent<EdgeCollider2D>();
        _trailCollider.isTrigger = true;    // usually you want trigger so you can detect OnTriggerEnter2D
        _trailCollider.edgeRadius = .1f;
        _trailCollider.enabled = false;

        // IGNORE collisions between the head and *its own* trail
        if (_headCollider != null)
            Physics2D.IgnoreCollision(_headCollider, _trailCollider);
    }

    void Start()
    {
        StartCoroutine(SpawnInvulRoutine());
        positionsHistory.Add(transform.position);
        currentBodyWidth = minBodyWidth;
        UpdateWidths();  // so line renderers & head scale start at minBodyWidth#

        if (photonView.IsMine)
        {

            Color outlineCol = Random.ColorHSV(0f, 1f, 0.6f, 1f, 0.6f, 1f);

            // 1) outline trail
            photonView.RPC(nameof(RPC_SetOutlineColor), RpcTarget.AllBuffered,
                           outlineCol.r, outlineCol.g, outlineCol.b);

            photonView.RPC(nameof(RPC_SetHeadOutlineColor),
                        RpcTarget.AllBuffered,
                        outlineCol.r, outlineCol.g, outlineCol.b);

        }
        

        if (photonView.IsMine)
        {
            // force black fill


            photonView.RPC("RPC_SetInitialFood", RpcTarget.AllBuffered, initialFood);
        }
    }

    private IEnumerator SpawnInvulRoutine()
    {
        _spawnInvulnerable = true;
        yield return new WaitForSeconds(spawnInvulDuration);
        _spawnInvulnerable = false;
    }

    [PunRPC]
    void RPC_SetHeadOutlineColor(float r, float g, float b)
    {
        var mpb = new MaterialPropertyBlock();
        headRenderer.GetPropertyBlock(mpb);
        mpb.SetColor(headOutlineProp, new Color(r, g, b));
        headRenderer.SetPropertyBlock(mpb);
    }

    [PunRPC]
    void RPC_SetOutlineColor(float r, float g, float b)
    {
        var col = new Color(r, g, b);
        outlineLine.startColor = outlineLine.endColor = col;

        if (iconLine != null && iconLine.material.HasProperty("_OutlineColor"))
        {
            iconLine.material.SetColor("_OutlineColor", col);
        }
    }

    // Called by FoodBehaviour
    public void OnOrbArrived(FoodBehaviour orb)
    {
        orb.OnConsumed();

        // increment score & width on the owner
        currentScore += 1;
        currentBodyWidth += widthPerFood;
        UpdateWidths();

        // notify others of your growth
        photonView.RPC(nameof(RPC_Grow), RpcTarget.OthersBuffered, 1);
    }


    void Update()
    {


        if (photonView.IsMine)
        {
            HandleMovement();

        }


        /* else if (_gotRemote)
         {
             transform.position = Vector3.Lerp(transform.position, _remotePos, 20f * Time.deltaTime);
             transform.rotation = Quaternion.Slerp(transform.rotation, _remoteRot, 20f * Time.deltaTime);
         }*/

        RecordPositions();
        UpdateLineRenderers();
    }

    [PunRPC]
    void RPC_SetInitialFood(int count)
    {
        currentScore = count;

        // make sure we have at least (count+1) positions so the renderer
        // can draw head + count segments immediately
        positionsHistory.Clear();
        for (int i = 0; i <= currentScore; i++)
        {
            positionsHistory.Add(transform.position);
        }

        currentBodyWidth = minBodyWidth + (widthPerFood * count);
        currentBodyWidth = Mathf.Min(currentBodyWidth, maxBodyWidth);
        UpdateWidths();
    }
    void SetupLineRenderer(LineRenderer lr, float width, Color col, string name)
    {
        lr.useWorldSpace = true;
        lr.loop = false;
        lr.startWidth = lr.endWidth = width;
        lr.startColor = lr.endColor = col;
        lr.numCapVertices = 16;
        lr.numCornerVertices = 16;
        lr.positionCount = 0;
        lr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        lr.receiveShadows = false;
    }



    private Vector2 _lastTouchPos;

    void HandleMovement()
    {
        float turnInput = 0f;

        if (Application.isMobilePlatform)
        {
            // � MOBILE TOUCH MODE �
            if (Input.touchCount > 0)
            {
                Touch t = Input.GetTouch(0);
                if (t.phase == TouchPhase.Began)
                {
                    _lastTouchPos = t.position;
                }
                else if (t.phase == TouchPhase.Moved)
                {
                    Vector2 delta = t.position - _lastTouchPos;
                    _lastTouchPos = t.position;
                    if (delta.sqrMagnitude > 25f)
                    {
                        float desired = Mathf.Atan2(delta.y, delta.x) * Mathf.Rad2Deg - 90f;
                        float current = transform.eulerAngles.z;
                        float diff = Mathf.DeltaAngle(current, desired);
                        turnInput = Mathf.Clamp(diff / 180f, -1f, 1f);
                    }
                }
            }
            else
            {
                // No finger: keep current heading
                turnInput = 0f;
            }
        }
        else
        {
            // � DESKTOP MOUSE STEERING �
            Vector2 pointerWorld = Camera.main.ScreenToWorldPoint(Input.mousePosition);
            Vector2 toPointer = pointerWorld - (Vector2)transform.position;
            if (toPointer.magnitude > deadZoneRadius)
            {
                float desired = Mathf.Atan2(toPointer.y, toPointer.x) * Mathf.Rad2Deg - 90f;
                float current = transform.eulerAngles.z;
                float diff = Mathf.DeltaAngle(current, desired);
                turnInput = Mathf.Clamp(diff / 180f, -1f, 1f);
            }
        }

        // 1) Apply turning
        float maxTurn = turnSpeed * Time.deltaTime;
        transform.Rotate(0f, 0f, turnInput * maxTurn);

        // 2) Boost detection
        isBoosting = Application.isMobilePlatform
           ? Input.touchCount >= 2
           : (Input.GetMouseButton(0) || Input.GetKey(KeyCode.Space));

      
          // compute final move
            float speed = baseSpeed * (isBoosting ? boostMultiplier : 1f);
            Vector2 start = transform.position;
            Vector2 dir = transform.up;
            float distance = speed * Time.deltaTime;

            // make sure we hit triggers
            bool prevQuery = Physics2D.queriesHitTriggers;
            Physics2D.queriesHitTriggers = true;

            // adjust layers to your setup; include other snakes' heads/trails
            int mask = LayerMask.GetMask("SnakeHead", "SnakeTrail");

            RaycastHit2D hit = Physics2D.CircleCast(start, colliderRadius, dir, distance, mask);
            if (hit && hit.collider != _trailCollider) // ignore own trail
            {
                if (!_spawnInvulnerable)
                {
                    HandleLocalDeath("Collided (sweep)");
                    photonView.RPC(nameof(RPC_MasterHandleDeath), RpcTarget.MasterClient, photonView.ViewID);
                    Physics2D.queriesHitTriggers = prevQuery;
                    return;
                }
            }

            Physics2D.queriesHitTriggers = prevQuery;

            // move only if sweep is clear
            transform.Translate(Vector3.up * distance, Space.Self);
                
    }




    void RecordPositions()
    {
        // 1) Compute a �gap� that depends on currentScore:
        //    as the snake grows, gap slides from baseGap?maxGap, ensuring fewer points.
        float t = Mathf.Clamp01(currentScore / 200f);
        realGap = Mathf.Lerp(baseGap, maxGap, t);
        realGap = Mathf.Min(realGap, 0.4f);


        Vector3 headPos = transform.position;
        if (positionsHistory.Count == 0)
        {
            positionsHistory.Add(headPos);
        }
        else
        {
            Vector3 lastPos = positionsHistory[0];
            float dist = Vector3.Distance(headPos, lastPos);
            if (dist > realGap)
            {
                Vector3 dir = (headPos - lastPos).normalized;
                int steps = Mathf.FloorToInt(dist / realGap);
                steps = Mathf.Min(steps, maxInterpolationPoints);

                for (int i = 1; i <= steps; i++)
                {
                    Vector3 point = lastPos + dir * realGap * i;
                    positionsHistory.Insert(0, point);
                }
            }
        }

        // 2) Trim to a �nonlinear� keepCount so the trail grows more slowly at high scores.
        int keepCount;
        if (currentScore <= 200)
        {
            // 1 segment per 2 points (?0.5�)
            keepCount = 1 + (int)(currentScore * 0.5f);
        }
        else if (currentScore <= 500)
        {
            // Between 200 and 500: 1 segment per ~6.7 points (0.15�)
            // At score=200 ? keepCount = 1 + (200*0.5) = 101
            // Up to score=500 ? keepCount = 101 + ((500-200)*0.15) = 101 + 45 = 146
            keepCount = 1 + (int)(200 * 0.5f)
                        + (int)((currentScore - 200) * 0.15f);
        }
        else if (currentScore <= 1000)
        {
            // Between 500 and 1000: 1 segment per 20 points (0.05�)
            // At score=500 ? keepCount = 1 + (200*0.5) + (300*0.15) = 1 + 100 + 45 = 146
            // Then add (currentScore - 500)*0.05
            keepCount = 1
                        + (int)(200 * 0.5f)
                        + (int)(300 * 0.15f)
                        + (int)((currentScore - 500) * 0.05f);
        }
        else
        {
            // Beyond 1000: 1 segment per 50 points (0.02�)
            // At score=1000 ? keepCount = 1 + (200*0.5) + (300*0.15) + (500*0.05) = 1 + 100 + 45 + 25 = 171
            keepCount = 1
                        + (int)(200 * 0.5f)
                        + (int)(300 * 0.15f)
                        + (int)(500 * 0.05f)
                        + (int)((currentScore - 1000) * 0.07f);
        }

        // Always enforce at least 1 segment
        keepCount = Mathf.Max(keepCount, 1);

        if (positionsHistory.Count > keepCount)
        {
            positionsHistory.RemoveRange(keepCount, positionsHistory.Count - keepCount);
        }
    }


    void UpdateLineRenderers()
    {
        // 1) get a slightly coarser spline (2 subdivisions instead of 4)
        var rawPts = GetSmoothedPoints(positionsHistory, subdivisions: 2);
        int count = rawPts.Count;

        // 1a) Force the very first point to match the head exactly
        rawPts[0] = transform.position + (transform.up * headOffset);

        // ��� Keep _prevDrawPts in sync with rawPts.Count ���
        if (_prevDrawPts != null)
        {
            // pad if new count is larger
            if (_prevDrawPts.Count < count)
            {
                var last = _prevDrawPts[_prevDrawPts.Count - 1];
                while (_prevDrawPts.Count < count)
                    _prevDrawPts.Add(last);
            }
            // truncate if new count is smaller
            else if (_prevDrawPts.Count > count)
            {
                _prevDrawPts.RemoveRange(count, _prevDrawPts.Count - count);
            }
        }

        // 2) if we have previous pts, blend to smooth jitter (skip index 0)
        if (_prevDrawPts != null)
        {
            float alpha = Mathf.Clamp01(Time.deltaTime * smoothSpeed);
            for (int i = 1; i < count; i++)
                rawPts[i] = Vector3.Lerp(_prevDrawPts[i], rawPts[i], alpha);
        }

        // 3) stash for next frame
        _prevDrawPts = new List<Vector3>(rawPts);

        // 4) outline & fill
        outlineLine.positionCount = fillLine.positionCount = count;
        for (int i = 0; i < count; i++)
        {
            outlineLine.SetPosition(i, rawPts[i]);
            fillLine.SetPosition(i, rawPts[i]);
        }

        // 5) collider
        int histCount = positionsHistory.Count;
        int colCount = Mathf.Min(histCount, count);

        if (colCount > 1)
        {
            var pts2D = new Vector2[colCount];
            for (int i = 0; i < colCount; i++)
            {
                pts2D[i] = transform.InverseTransformPoint(positionsHistory[i]);
            }
            _trailCollider.points = pts2D;
            _trailCollider.enabled = true;
        }
        else
        {
            _trailCollider.enabled = false;
        }

        // 6) iconLine
        iconLine.positionCount = count;
        for (int i = 0; i < count; i++)
            iconLine.SetPosition(i, rawPts[i]);

        // 7) sizing & tiling (one dot per diameter)
        float dotDiameter = currentBodyWidth * 0.3f;
        iconLine.startWidth = iconLine.endWidth = dotDiameter;
        float iconsPerUnit = (dotDiameter > 0f) ? (1f / dotDiameter) : 1f;
        iconLine.material.SetFloat("_IconsPerU", iconsPerUnit);
        iconLine.textureMode = LineTextureMode.Tile;
    }




    public void Grow(int points = 1)
    {
        if (!photonView.IsMine) return;

        photonView.RPC("RPC_Grow", RpcTarget.AllBuffered, points);
    }


    [PunRPC]
    void RPC_Grow(int points)
    {
        currentScore += points;

        // If that was �others� picking up your food, we also need to widen
        // their copy of your snake�s body/ head scale exactly the same:
        currentBodyWidth += widthPerFood * points;
        UpdateWidths();

        // Pad positionsHistory as before for the trail:
        int want = currentScore + 1;
        while (positionsHistory.Count < want)
            positionsHistory.Add(positionsHistory[positionsHistory.Count - 1]);
    }


    /// <summary>
    /// Call this anytime you want to change the snake's width (e.g. after Grow or hitting a leftover).
    /// It updates both trail width and head scale so they remain proportional.
    /// </summary>
    private void UpdateWidths()
    {
        // clamp body width so it never exceeds maxBodyWidth:
        currentBodyWidth = Mathf.Min(currentBodyWidth, maxBodyWidth);

        // 1) Update the LineRenderer widths (body):
        outlineLine.startWidth = outlineLine.endWidth = currentBodyWidth;
        fillLine.startWidth = fillLine.endWidth = currentBodyWidth * 0.85f;
        // (You can pick 0.85f so the �fill� trail is slightly thinner if you like.)

        // 2) Update the head sprite�s scale so it�s always a bit bigger than the body:
        float headScale = currentBodyWidth * headSizeMultiplier;
        snakeRenderer.transform.localScale = Vector3.one * headScale;
    }



    // --- Collision logic remains unchanged (call Grow, IncreaseSnakeSize, Die, etc.) ---

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (!photonView.IsMine) return;
        Debug.Log($"[Snake {photonView.ViewID}] OnTriggerEnter2D with {other.name} ({other.tag})");

        if (_spawnInvulnerable)
            return;

        bool hitHead = other.CompareTag("SnakeHead");
        bool hitTrail = other.gameObject.name.EndsWith("_TrailCollider");

        if (hitHead || hitTrail)
        {
            if (hitTrail)
            {
                // Skip if this collider belongs to your own trail object:
                if (other == _trailCollider)
                    return;
            }

            // HEAD vs HEAD: only die if *you* actually ran into their head 
            if (hitHead)
            {
                // direction from *you* to the other head
                Vector2 toOther = ((Vector2)other.transform.position - (Vector2)transform.position).normalized;
                // dot > 0 => the other head is in front of you, so *you* are running into them ? you die
                if (Vector2.Dot(transform.up, toOther) <= 0f)
                {
                    // other head was behind you ? they ran into you, so *they* should die instead
                    return;
                }
            }


            // either we hit their trail, or we ran head-on into their front
            HandleLocalDeath("Collided with another snake");
            photonView.RPC(
                nameof(RPC_MasterHandleDeath),
                RpcTarget.MasterClient,
                photonView.ViewID
            );

            return;
        }



     


        if (other.CompareTag("Leftover"))
        {
            var lo = other.GetComponent<Leftover>();
            if (lo == null) return;

            if (lo.ownerActor == PhotonNetwork.LocalPlayer.ActorNumber)
                return;

            Grow(lo.value);
            // instead of IncreaseSnakeSize(1.00002f):
            currentBodyWidth += widthPerFood * lo.value;
            UpdateWidths();


            GameManager.Instance.RequestDestroyLeftover(lo.leftoverId);
            return;
        }

        // ... rest of head/body collisions ...
    }

    [PunRPC]
    public void RPC_MasterHandleDeath(int victimId, PhotonMessageInfo info)
    {
        if (!PhotonNetwork.IsMasterClient) return;
        // Tell the *owner* to actually destroy:
        PhotonView victim = PhotonView.Find(victimId);
        if (victim != null)
        {
            victim.RPC(nameof(RPC_RequestOwnerDestroy), victim.Owner);
        }
    }

    // 3) Owner runs this and is then allowed to destroy itself:
    [PunRPC]
    void RPC_RequestOwnerDestroy(PhotonMessageInfo info)
    {
        if (!photonView.IsMine) return;
        PhotonNetwork.Destroy(gameObject);
    }

    public void HandleLocalDeath(string reason)
    {
        Debug.Log($"(LOCAL) Snake {PhotonNetwork.NickName} died: {reason}");

        // collect spawn positions (same sampling you had)
        var spawnPositions = new List<Vector3>();
        int spawnCount = Mathf.Clamp(currentScore + 1, 1, positionsHistory.Count);
        for (int i = 0; i < spawnCount; i += leftoverInterval)
            spawnPositions.Add(positionsHistory[i]);

        // Request the GameManager / Master to spawn leftovers and encode how big this snake was
        if (GameManager.Instance != null)
        {
            // pass owner actor so leftovers know who died, and pass the victim's score
            GameManager.Instance.RequestSpawnLeftovers(spawnPositions.ToArray(), PhotonNetwork.LocalPlayer.ActorNumber, currentScore);
        }

        GameManager.Instance.RecordFinalScore(currentScore);

        // show Game Over UI locally
        GameManager.Instance.GameOver();

        // disable this component so you stop moving / colliding
        enabled = false;
        _headCollider.enabled = false;
        outlineLine.enabled = false;
        fillLine.enabled = false;
    }




    [PunRPC]
    public void RPC_UpdateSpinningRim(int winningViewID)
    {
        // if this snake's PhotonView ID matches the winner, show rim, else hide it
        bool isWinner = (photonView.ViewID == winningViewID);
        if (spinningRim != null)
            spinningRim.SetActive(isWinner);
    }


}