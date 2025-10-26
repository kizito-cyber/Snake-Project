using Photon.Pun;
using Photon.Realtime;
using System.Linq;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.SceneManagement;
using MilkShake;
using System.Collections.Generic;
using System.Collections;
using TMPro;
using UnityEngine.UI;
public class GameManager : MonoBehaviourPunCallbacks
{
    public static GameManager Instance;
    [SerializeField] private CameraFollow cameraHolder; // Assigned via the Inspector
    [SerializeField] private int initialFoodAmount = 1300;
  
    [SerializeField] private string dotFoodPrefabName = "Food_Dot";   // For blinking colored dot food
    [SerializeField] private string cloudFoodPrefabName = "Food_Cloud"; // For cloud-like food
    [SerializeField] private string leftoverPrefabName = "Leftover";
    [SerializeField] private int leftoverValue = 2; // or whatever your default i

    // Container for all food objects to keep hierarchy clean.
    [SerializeField] private Transform foodContainer;
    private int _nextFoodId = 1;
    public bool _localSnakeSpawned = false;
    public GameObject LosePanel;
    public GameObject loadingPanel;
    public Shaker myShaker;
    public ShakePreset myShakePreset;

    public float survivalTime = 0f;
    private bool isTrackingTime;
    [SerializeField] private TMPro.TextMeshProUGUI survivalTimeText;
    [SerializeField] private TMPro.TextMeshProUGUI scoreText;
    public float safeSpawnRadius = 18f;

    public PhotonView _pv;

    [SerializeField] private LeaderboardUI leaderboardUI;

    private int lastScore = 0;


    [Header("Prize UI")]
    [SerializeField] private TextMeshProUGUI prizeText;
    [SerializeField] private Button claimPrizeButton;

    [Header("Food Tracking")]
    [SerializeField, Tooltip("How many total food pieces can exist at once")]
    private int maxFoodCount = 1300;

    private float refillInterval = 3f;

    public bool isPlayerIn = false;

    private string _lastPrizeUrl;

    [Header("Debug / UI")]
    [SerializeField, Tooltip("Optional: assign a TextMeshProUGUI to display active food count")]
    private TextMeshProUGUI foodCountText;

    [SerializeField, Tooltip("Log food counts every N seconds for debugging")]
    private float foodCountLogInterval = 2f;

    // A simple persistent value so you can check across rounds
    private int lastKnownActiveFoodCount = 0;


    private Coroutine _refillCoroutine = null;
    private bool _masterInitialized = false; // to avoid re-initializing multiple times
    private void Awake()
    {
        Instance = this;
        _pv = GetComponent<PhotonView>();


    }
    void Start()
    {
        if (FoodPoolManager.Instance != null)
        {
            FoodPoolManager.Instance.OnActiveFoodCountChanged += HandleFoodCountChanged;
            // Initialize UI with current count immediately (if pool already exists)
            lastKnownActiveFoodCount = FoodPoolManager.Instance.ActiveFoodCount;
            UpdateFoodCountUI(lastKnownActiveFoodCount);
        }

        // start periodic logger
        StartCoroutine(FoodCountLogger());

    }
    private void Update()
    {

        if (isTrackingTime)
        {
            survivalTime += Time.deltaTime;
        }

    }
  
 
    // handler called whenever active food count changes
    private void HandleFoodCountChanged(int currentActive)
    {
        lastKnownActiveFoodCount = currentActive;
        Debug.Log($"[GM] ActiveFoodCount changed -> {currentActive} (Pooled {FoodPoolManager.Instance.PooledFoodCount})");
        UpdateFoodCountUI(currentActive);
    }

    private void UpdateFoodCountUI(int current)
    {
        if (foodCountText != null)
        {
            foodCountText.text = $"Food: {current} (pool {FoodPoolManager.Instance.PooledFoodCount})";
        }
    }

    // periodic logger — useful to see system over time and across rounds
    private IEnumerator FoodCountLogger()
    {
        var wait = new WaitForSeconds(foodCountLogInterval);
        while (true)
        {
            if (FoodPoolManager.Instance != null)
            {
                Debug.Log($"[GM Logger] Active={FoodPoolManager.Instance.ActiveFoodCount}  Pooled={FoodPoolManager.Instance.PooledFoodCount}  Total={FoodPoolManager.Instance.TotalFoodCount}  nextFoodId={_nextFoodId}");
            }
            yield return wait;
        }
    }
    IEnumerator UpdateRimRoutine()
    {
        Debug.Log("[GM] UpdateRimRoutine() started");
        WaitForSeconds wait = new WaitForSeconds(2f); // update every 0.25 seconds
        while (true)
        {
            UpdateHighestScoreRim();
            leaderboardUI.Refresh();
            yield return wait;
        }
    }

    // Spawns the local snake and sets the camera follow target.
    void SpawnLocalSnake()
    {
        _localSnakeSpawned = true;
        //Vector2 spawnPos = Random.insideUnitCircle * 3f;
        Vector2 spawnPos = GetSafeRandomPosition() * .7f;
        GameObject snake = PhotonNetwork.Instantiate("Snake", spawnPos, Quaternion.identity);

          Debug.Log($"[GM] Requested spawn at {spawnPos}. Instantiated object name:{snake.name}");

    // log actual immediately and next frame - some network components may overwrite on the same frame
    Debug.Log($"[GM] Actual transform.position immediately after Instantiate: {snake.transform.position}");

        cameraHolder.GetComponent<CameraFollow>().SetTarget(snake.transform);
        isTrackingTime = true;
    }





    public override void OnJoinedRoom()
    {
        
        if (!_localSnakeSpawned)
        {
            StartCoroutine(WaitOnSnakeJoin()); // Every player spawns their own snake
        }

        if (PhotonNetwork.IsMasterClient && !_masterInitialized)
        {
           
            Debug.Log("[GM] I am MasterClient, spawning initial food...");
            _masterInitialized = true;

            _nextFoodId = 1;
            StartCoroutine(RefillLoop());

        }
        LeftoverPoolManager.Instance.ClearPool();
        StartCoroutine(UpdateRimRoutine());
    }
    private void StartRefillLoop()
    {
        if (_refillCoroutine != null) StopCoroutine(_refillCoroutine);
        _refillCoroutine = StartCoroutine(RefillLoop());
    }
    private void StopRefillLoop()
    {
        if (_refillCoroutine != null) { StopCoroutine(_refillCoroutine); _refillCoroutine = null; }
    }

    [PunRPC]
    void RPC_ClearAllFood()
    {
        FoodPoolManager.Instance.ClearPoolAndActive();
        _nextFoodId = 1;
    }
    public override void OnMasterClientSwitched(Player newMaster)
    {
        Debug.Log($"OnMasterClientSwitched -> new master actor: {newMaster.ActorNumber}, isLocalMaster: {PhotonNetwork.IsMasterClient}");

        // Only the new master runs the reconciliation
        if (!PhotonNetwork.IsMasterClient)
        {
            // If we were master before, stop our refill; otherwise do nothing
            StopRefillLoop();
            _masterInitialized = false;
            return;
        }

        // We are now the master. Reconcile the world and only spawn missing pieces.
        // 1) compute highest food id already present so we won't reuse ids
        int maxId = 0;
        var allFoods = FindObjectsOfType<FoodBehaviour>();
        foreach (var fb in allFoods)
            maxId = Mathf.Max(maxId, fb.foodId);

        // ensure next id is higher than any seen
        _nextFoodId = Mathf.Max(_nextFoodId, maxId + 1);

        // 2) figure out how many we need to spawn
        int active = (FoodPoolManager.Instance != null) ? FoodPoolManager.Instance.ActiveFoodCount : 0;
        int toSpawn = Mathf.Max(0, maxFoodCount - active);

        Debug.Log($"New master: maxId={maxId} nextFoodId={_nextFoodId} active={active} will spawn missing={toSpawn}");

        // 3) spawn only the missing foods (these RPCs will be sent to all clients)
        for (int i = 0; i < toSpawn; i++)
            SpawnOneFood();

        // 4) start the refill loop (which will top up over time)
        StartRefillLoop();

        _masterInitialized = true;
    }


    IEnumerator WaitOnSnakeJoin()
    {
        yield return new WaitForSeconds(1.5f);

        SpawnLocalSnake();

     

    }
    public override void OnPlayerEnteredRoom(Player newPlayer)
    {
        if (!PhotonNetwork.IsMasterClient) return;

        // Gather all current live foods
        var all = FindObjectsOfType<FoodBehaviour>();
        int n = all.Length;
        var ids = new int[n];
        var names = new string[n];
        var poses = new Vector2[n];

        for (int i = 0; i < n; i++)
        {
            ids[i] = all[i].foodId;
            names[i] = all[i].foodData.foodType == FoodType.Dot ? dotFoodPrefabName : cloudFoodPrefabName;
            poses[i] = all[i].transform.position;
        }

        // Send to the newly-joined player only
        photonView.RPC(nameof(RPC_SpawnSnapshot), newPlayer, ids, names, poses);
    }



    [PunRPC]
    void RPC_SpawnSnapshot(int[] ids, string[] prefabNames, Vector2[] positions)
    {

        FoodPoolManager.Instance.ClearPoolAndActive();

        for (int i = 0; i < ids.Length; i++)
            RPC_SpawnFood(ids[i], prefabNames[i], positions[i]);
    }

    [PunRPC]
    public void RPC_ReportFoodEaten(int foodId, int eaterViewId, PhotonMessageInfo info)
    {
        // no-op: the refill loop will top up
        if (!PhotonNetwork.IsMasterClient) return;
        var fb = FoodPoolManager.Instance.GetFoodById(foodId);
        if (fb == null)
        {
            Debug.LogWarning($"[GM] Master: ReportFoodEaten - unknown foodId {foodId}");
            // still spawn a replacement to keep counts steady
            SpawnOneFood();
            return;
        }

        // Optionally validate further (distance checks, cheating protection, etc.)

        // 1) notify everyone to remove this food (they will return to pool locally)
        photonView.RPC(nameof(RPC_RemoveFood), RpcTarget.All, foodId);

        // 2) spawn immediate replacement (Master authoritative)
        SpawnOneFood();
    }

    [PunRPC]
    void RPC_RemoveFood(int foodId, PhotonMessageInfo info)
    {
        // find the orb in local active set
        var fb = FoodPoolManager.Instance.GetFoodById(foodId);
        if (fb == null)
        {
            // nothing to remove locally
            return;
        }

        // ensure attraction stopped and trail cleared
        fb.isAttracting = false;
        fb.isConsumed = true;
        var tr = fb.GetComponent<TrailRenderer>();
        if (tr != null) { tr.emitting = false; tr.Clear(); }

        // return the local object to pool
        FoodPoolManager.Instance.ReturnFood(fb.gameObject, fb.foodData.foodType);
    }
    IEnumerator RefillLoop()
    {
        var wait = new WaitForSeconds(refillInterval);
        while (true)
        {
            int active = FoodPoolManager.Instance.ActiveFoodCount;
            int toSpawn = maxFoodCount - active;
            for (int i = 0; i < toSpawn; i++)
                SpawnOneFood();
            yield return wait;
        }
    }

    void SpawnOneFood()
    {
        if (!PhotonNetwork.IsMasterClient)
            return; // only master spawns

        int id = _nextFoodId++;
        Vector2 pos = Random.insideUnitCircle * safeSpawnRadius;
        string prefab = (Random.value < 0.7f) ? cloudFoodPrefabName : dotFoodPrefabName;

        // instruct all clients to spawn this food locally
        photonView.RPC(nameof(RPC_SpawnFood), RpcTarget.All, id, prefab, pos);
    }

    [PunRPC]
    void RPC_SpawnFood(int foodId, string prefabName, Vector2 position)
    {
        var existing = FoodPoolManager.Instance.GetFoodById(foodId);
        if (existing != null)
        {
            Debug.Log($"RPC_SpawnFood: foodId {foodId} already exists locally — skipping spawn.");
            return;
        }

        var type = prefabName.Contains("Dot") ? FoodType.Dot : FoodType.Cloud;
        var go = FoodPoolManager.Instance.GetFood(type, position);
        var fb = go.GetComponent<FoodBehaviour>();
        if (fb != null) fb.foodId = foodId;
    }



    void OnDestroy()
    {
#if UNITY_EDITOR
        // This will run only in the editor.
        GameObject[] leftovers = GameObject.FindGameObjectsWithTag("Leftover");
        foreach (GameObject leftover in leftovers)
        {
            DestroyImmediate(leftover);
        }
#endif

        if (FoodPoolManager.Instance != null)
            FoodPoolManager.Instance.OnActiveFoodCountChanged -= HandleFoodCountChanged;
    }




    public void RequestSpawnLeftovers(Vector3[] worldPositions, int ownerActor, int ownerScore)
    {
        // Owner asks Master to spawn them (so master is authoritative)
        _pv.RPC(nameof(RPC_SpawnLeftovers), RpcTarget.MasterClient, worldPositions, ownerActor, ownerScore);
    }

    [PunRPC]
    private void RPC_SpawnLeftovers(Vector3[] worldPositions, int ownerActor, int ownerScore, PhotonMessageInfo info)
    {
        if (!PhotonNetwork.IsMasterClient)
        {
            // only Master should run the authoritative spawn logic
            return;
        }

        if (worldPositions == null || worldPositions.Length == 0)
            return;

        // Decide what fraction of the dead snake's score gets returned as leftovers:
        float transferFraction = 0.6f; // 60% of score becomes leftovers (tweak as desired)
        int totalToDistribute = Mathf.Max(1, Mathf.FloorToInt(ownerScore * transferFraction));

        int n = worldPositions.Length;
        int baseVal = totalToDistribute / n;
        int remainder = totalToDistribute % n;

        // For each position, spawn a leftover (Master tells everyone to actually create the leftover)
        for (int i = 0; i < n; i++)
        {
            // value distribution: distribute the remainder to the first few pieces
            int pieceValue = baseVal + (i < remainder ? 1 : 0);
            // Ensure at least 1
            pieceValue = Mathf.Max(1, pieceValue);

            // spawn on all clients (Master instructs everyone to create the visual/local leftover)
            photonView.RPC(nameof(RPC_SpawnLeftoverSingle), RpcTarget.All, worldPositions[i], ownerActor, pieceValue);
        }
    }

    [PunRPC]
    private void RPC_SpawnLeftoverSingle(Vector3 worldPos, int ownerActor, int pieceValue, PhotonMessageInfo info)
    {
        // Use your Leftover pool manager to spawn the object and get its id
        int id = LeftoverPoolManager.Instance.SpawnLeftover(worldPos);

        // find the spawned leftover and set its owner/value
        var lo = FindObjectsOfType<Leftover>().FirstOrDefault(l => l.leftoverId == id);
        if (lo != null)
        {
            lo.ownerActor = ownerActor;
            lo.value = pieceValue;
        }
    }
    public void RequestDestroyLeftover(int id)
    {
        photonView.RPC(nameof(RPC_DestroyLeftover), RpcTarget.All, id);
    }

    [PunRPC]
    private void RPC_DestroyLeftover(int leftoverId, PhotonMessageInfo info)
    {
        LeftoverPoolManager.Instance.ReturnLeftover(leftoverId);
    }

    public Vector2 GetSafeRandomPosition()
    {
        // This returns a point inside a circle of radius safeSpawnRadius.
        return Random.insideUnitCircle * safeSpawnRadius;
    }

    void UpdateHighestScoreRim()
    {
        // Find all SnakeController instances in the scene.
        SnakeController[] allSnakes = FindObjectsOfType<SnakeController>();
        if (allSnakes.Length == 0)
        {
            Debug.LogWarning("UpdateHighestScoreRim: No snakes found.");
            return;
        }

        int highestScore = -1;
        int winningViewID = -1;
        foreach (var snake in allSnakes)
        {
           // Debug.Log($"Snake {snake.photonView.ViewID} currentScore: {snake.currentScore}");
            if (snake.currentScore > highestScore)
            {
                highestScore = snake.currentScore;
                winningViewID = snake.photonView.ViewID;
            }
        }

        // Only assign a winningViewID if the highest score meets the threshold.
        if (highestScore < 7)
        {
            winningViewID = -1;
        }
        Debug.Log("UpdateHighestScoreRim: WinningViewID = " + winningViewID);
        // Now, send an RPC call on each snake's PhotonView to update the rim.
        foreach (var snake in allSnakes)
        {

            snake.photonView.RPC("RPC_UpdateSpinningRim", RpcTarget.All, winningViewID);
        }
    }

    public void RecordFinalScore(int score)
    {
        lastScore = score;
    }

    public void GameOver()
    {
    
        isTrackingTime = false;


        PhotonNetwork.RemoveRPCs(PhotonNetwork.LocalPlayer);

        myShaker.Shake(myShakePreset);
        StartCoroutine(GameOverWait());

    }
    IEnumerator GameOverWait()
    {
        yield return new WaitForSeconds(.4f);

        int seconds = Mathf.RoundToInt(survivalTime);
        survivalTimeText.text = $"You survived for {seconds} seconds!";
        scoreText.text = $"Score: {lastScore}";
       // FoodPoolManager.Instance.ClearPoolAndActive();
        LosePanel.SetActive(true);


        if (string.IsNullOrEmpty(DoorzyApi.CurrentUserId))
        {
            prizeText.text = "Log in to submit your score & claim prizes.";
            claimPrizeButton.gameObject.SetActive(false);
            yield break;
        }


        StartCoroutine(DoorzyApi.SubmitResult(survivalTime, lastScore, (ok, resp) => {
            if (!ok)
            {
                prizeText.text = "Error submitting score. Please try again.";
                claimPrizeButton.gameObject.SetActive(false);
            }
            else
            {
                // 2) HTTP succeeded, but server said no prize
                if (!resp.ok)
                {
                    prizeText.text = "You have not survived long enough to win a prize.";
                    claimPrizeButton.gameObject.SetActive(false);
                }
                else
                {
                    // 3) HTTP succeeded and server granted a prize
                    prizeText.text = resp.message ?? "Congratulations You have won a prize!";
                    if (!string.IsNullOrEmpty(resp.redirect_url))
                    {
                        claimPrizeButton.gameObject.SetActive(true);
                        _lastPrizeUrl = resp.redirect_url;
                    }
                    else
                    {
                        claimPrizeButton.gameObject.SetActive(false);
                    }
                }
            }
        }));
    }

    public void MainMenu()
    {
        // 1) Show the spinner panel immediately
        loadingPanel.SetActive(true);

        // 2) Disconnect from Photon
        PhotonNetwork.Disconnect();

        // 3) Start the timed load coroutine
        StartCoroutine(LoadMainMenuWithDelay());
    }

    private IEnumerator LoadMainMenuWithDelay()
    {
        // Give the spinner 2 seconds to spin
        yield return new WaitForSeconds(1f);

        // Now begin loading asynchronously
        AsyncOperation op = SceneManager.LoadSceneAsync("MainMenu");
        op.allowSceneActivation = true;

        // Wait until the new scene is fully loaded
        while (!op.isDone)
            yield return null;
    }

    public void OnClaimPrizeClicked()
    {
        if (!string.IsNullOrEmpty(_lastPrizeUrl))
            Application.OpenURL(_lastPrizeUrl);
    }


    void OnApplicationQuit()
    {
        PhotonNetwork.LeaveRoom();
    }

}
