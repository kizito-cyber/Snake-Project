using System.Collections.Generic;
using TMPro;
using UnityEngine;

/// <summary>
/// Places small advertising labels on hex centers (pointy-top hex layout).
/// Uses a simple pool and distance culling so it stays performant.
/// </summary>
public class HexAdPlacer : MonoBehaviour
{
    [Header("Prefab / Camera")]
    public GameObject adLabelPrefab;         // world-space TextMeshPro prefab
    public Camera mainCamera;

    [Header("Hex Grid")]
    [Tooltip("Hex size (radius). Tune so hex spacing matches your background.")]
    public float hexSize = 1f;
    [Tooltip("How many hex steps from center (hex radius). Larger -> more labels.")]
    public int hexRadius = 10;

    [Header("Ad Settings")]
    public List<string> adStrings = new List<string>()
    {
        "Your Ad Here",
        "Sponsor 1",
        "Sponsor 2",
        "Try Our App!"
    };
    [Tooltip("How far from camera (world units) labels remain active.")]
    public float showDistance = 25f;
    [Tooltip("If true, labels will rotate to face camera.")]
    public bool billboard = true;
    [Tooltip("Spacing jitter to avoid exact repetition (0 = no jitter)")]
    public float jitter = 0.05f;

    // pooling
    private List<GameObject> _pool = new List<GameObject>();
    private List<Vector3> _positions = new List<Vector3>();

    private void Reset()
    {
        // auto assign camera
        if (mainCamera == null) mainCamera = Camera.main;
    }

    void Start()
    {
        if (mainCamera == null) mainCamera = Camera.main;
        if (adLabelPrefab == null)
        {
            Debug.LogError("HexAdPlacer: assign adLabelPrefab.");
            enabled = false;
            return;
        }

        BuildHexCenters();
        EnsurePoolSize(_positions.Count);

        // Fetch vendor/ad names from server and then assign labels.
        StartCoroutine(DoorzyApi.GetVendorNames((ok, vendorList) =>
        {
            if (ok && vendorList != null && vendorList.Count > 0)
            {
                adStrings = vendorList; // replace static list with API-provided names
                Debug.Log($"HexAdPlacer: loaded {vendorList.Count} vendor names from API.");
            }
            else
            {
                Debug.Log("HexAdPlacer: using fallback adStrings");
            }

            AssignLabels(); // now populate the pool with the chosen adStrings
        }));
    }


    void Update()
    {
        // cull/pool logic: enable only labels within showDistance (cheap distance test)
        var camPos = mainCamera.transform.position;
        for (int i = 0; i < _positions.Count; i++)
        {
            var go = _pool[i];
            if (go == null) continue;

            float d = Vector3.Distance(camPos, _positions[i]);
            bool shouldShow = d <= showDistance;

            if (go.activeSelf != shouldShow)
                go.SetActive(shouldShow);

            if (shouldShow && billboard)
            {
                // keep label facing camera (2D top-down): align Z rotation so text faces camera
                // for orthographic top-down camera, just match camera rotation on X/Y or set forward
                go.transform.rotation = Quaternion.LookRotation(Vector3.forward, mainCamera.transform.up);
                // Alternatively: go.transform.rotation = Quaternion.LookRotation(go.transform.position - mainCamera.transform.position);
            }
        }
    }

    // compute hex centers using pointy-top axial coordinates
    private void BuildHexCenters()
    {
        _positions.Clear();

        // pointy-top formula:
        // x = size * sqrt(3) * (q + r/2)
        // y = size * 3/2 * r
        float s = hexSize;
        float w = Mathf.Sqrt(3f) * s;
        for (int q = -hexRadius; q <= hexRadius; q++)
        {
            int r1 = Mathf.Max(-hexRadius, -q - hexRadius);
            int r2 = Mathf.Min(hexRadius, -q + hexRadius);
            for (int r = r1; r <= r2; r++)
            {
                float x = w * (q + r / 2f);
                float y = s * 1.5f * r;
                var pos = new Vector3(x, y, 0f);

                // optional small jitter so labels don't perfectly overlap grid
                if (jitter > 0f)
                    pos += new Vector3(Random.Range(-jitter, jitter), Random.Range(-jitter, jitter), 0f);

                _positions.Add(pos);
            }
        }
    }

    private void EnsurePoolSize(int want)
    {
        // create or destroy pool entries to match count
        while (_pool.Count < want)
        {
            var go = Instantiate(adLabelPrefab, transform);
            go.SetActive(false);
            _pool.Add(go);
        }

        // (we intentionally don't destroy if too many — you can add trimming if you want)
    }

    private void AssignLabels()
    {
        for (int i = 0; i < _positions.Count; i++)
        {
            var go = _pool[i];
            go.transform.position = _positions[i];
            go.transform.SetParent(transform, worldPositionStays: true);

            // choose ad text (round-robin)
            string text = adStrings.Count > 0 ? adStrings[i % adStrings.Count] : $"Ad {i}";
            var tmp = go.GetComponent<TextMeshPro>();
            if (tmp == null) tmp = go.GetComponentInChildren<TextMeshPro>();
            if (tmp != null)
            {
                tmp.text = text;
                tmp.alignment = TextAlignmentOptions.Center;
            }

            // optionally scale label according to camera/zoom (for orthographic):
            // float scale = 1f;
            // go.transform.localScale = Vector3.one * scale;

            // leave it inactive; Update() will enable nearby ones
            go.SetActive(false);
        }
    }
}
