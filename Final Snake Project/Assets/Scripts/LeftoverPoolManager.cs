using System.Collections.Generic;
using UnityEngine;

public class LeftoverPoolManager : MonoBehaviour
{
    public static LeftoverPoolManager Instance;

    [SerializeField] private GameObject leftoverPrefab;
    [SerializeField] private Transform poolContainer;

    private Queue<GameObject> pool = new Queue<GameObject>();
    private int nextId = 1;

    void Awake()
    {
        Instance = this;
    }

    /// <summary>
    /// Spawns (or re-uses) a leftover at worldPos, assigns and returns its instance ID.
    /// </summary>
    public int SpawnLeftover(Vector3 worldPos)
    {
        GameObject go;
        if (pool.Count > 0)
        {
            go = pool.Dequeue();
            go.SetActive(true);
        }
        else
        {
            go = Instantiate(leftoverPrefab);
            go.SetActive(true);
        }

        go.transform.position = worldPos;
        if (poolContainer) go.transform.SetParent(poolContainer);

        var lo = go.GetComponent<Leftover>();
        if (lo == null) lo = go.AddComponent<Leftover>();
        lo.leftoverId = nextId;

        return nextId++;
    }

    /// <summary>
    /// Returns the leftover with that ID to the pool.
    /// </summary>
    public void ReturnLeftover(int leftoverId)
    {
        // find the live object
        foreach (var lo in FindObjectsOfType<Leftover>())
        {
            if (lo.leftoverId == leftoverId)
            {
                lo.gameObject.SetActive(false);
                pool.Enqueue(lo.gameObject);
                Debug.Log("I have deactivate sha");
                return;
            }
        }
    }

    public void ClearPool()
    {
        // destroy queued objects
        while (pool.Count > 0)
        {
            Destroy(pool.Dequeue());
        }
    }
}
