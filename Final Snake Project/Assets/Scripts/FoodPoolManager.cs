using System;
using System.Collections.Generic;
using UnityEngine;

public class FoodPoolManager : MonoBehaviour
{
    public static FoodPoolManager Instance;

    [SerializeField] private GameObject dotFoodPrefab;
    [SerializeField] private GameObject cloudFoodPrefab;
    [SerializeField] private Transform poolContainer;

    // Pools
    private Queue<GameObject> dotPool = new Queue<GameObject>();
    private Queue<GameObject> cloudPool = new Queue<GameObject>();

    // Tracks all *active* (not-pooled) foods:
    private HashSet<GameObject> activeFoods = new HashSet<GameObject>();

    public event Action<int> OnActiveFoodCountChanged;

    private void Awake()
    {
        Instance = this;
    }

    /// <summary>
    /// Pulls a food from the pool (or instantiates if empty), positions it,
    /// marks it active, and returns it.
    /// </summary>
    public GameObject GetFood(FoodType type, Vector2 position)
    {
        Queue<GameObject> pool = (type == FoodType.Dot) ? dotPool : cloudPool;
        GameObject prefab = (type == FoodType.Dot) ? dotFoodPrefab : cloudFoodPrefab;
        GameObject instance;

        if (pool.Count > 0)
        {
            instance = pool.Dequeue();
            instance.SetActive(true);
        }
        else
        {
            instance = Instantiate(prefab);
        }

        // Position & parent:
        instance.transform.position = position;
        if (poolContainer != null)
            instance.transform.SetParent(poolContainer, worldPositionStays: true);

        // Book-keep it as “live”:
        activeFoods.Add(instance);

        OnActiveFoodCountChanged?.Invoke(activeFoods.Count);
        return instance;
    }

    /// <summary>
    /// Looks up an active FoodBehaviour by its unique foodId.
    /// Returns null if not found (e.g. already pooled).
    /// </summary>
    public FoodBehaviour GetFoodById(int searchId)
    {
        foreach (var go in activeFoods)
        {
            var fb = go.GetComponent<FoodBehaviour>();
            if (fb != null && fb.foodId == searchId)
                return fb;
        }
        return null;
    }

    /// <summary>
    /// Returns a food to its pool (deactivates it), and marks it no longer active.
    /// </summary>
    public void ReturnFood(GameObject obj, FoodType type)
    {
        if (!activeFoods.Remove(obj))
            Debug.LogWarning("Tried to return a food that wasn't active: " + obj.name);

        obj.SetActive(false);

        var pool = (type == FoodType.Dot) ? dotPool : cloudPool;
        pool.Enqueue(obj);

        OnActiveFoodCountChanged?.Invoke(activeFoods.Count);
    }

    public void ClearPoolAndActive()
    {
        // Return all active to pool
        foreach (var obj in activeFoods)
        {
            obj.SetActive(false);
            var fb = obj.GetComponent<FoodBehaviour>();
            if (fb != null)
            {
                if (fb.foodData.foodType == FoodType.Dot)
                    dotPool.Enqueue(obj);
                else
                    cloudPool.Enqueue(obj);
            }
            else
                Debug.LogWarning("Missing FoodBehaviour on " + obj.name);
        }
        activeFoods.Clear();

        // Disable everything in the pools
        foreach (var obj in dotPool) obj.SetActive(false);
        foreach (var obj in cloudPool) obj.SetActive(false);

        Debug.Log($"Cleared pools. Dot={dotPool.Count}, Cloud={cloudPool.Count}");

        OnActiveFoodCountChanged?.Invoke(activeFoods.Count);
    }

    // Debug helpers:
    public int ActiveFoodCount => activeFoods.Count;
    public int PooledFoodCount => dotPool.Count + cloudPool.Count;
    public int TotalFoodCount => ActiveFoodCount + PooledFoodCount;
}
