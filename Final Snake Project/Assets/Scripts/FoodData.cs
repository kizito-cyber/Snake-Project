using UnityEngine;

public enum FoodType
{
    Cloud,    // Appears occasionally and disappears if not eaten
    Dot,      // Blinking colored dots; respawn after consumption after a delay
    Leftover  // Spawned from dead snakes; disappears if not picked up
}

[CreateAssetMenu(fileName = "FoodData", menuName = "ScriptableObjects/FoodData", order = 1)]
public class FoodData : ScriptableObject
{
    public FoodType foodType;
    public Sprite foodSprite;

    [Header("Lifetime Settings")]
    [Tooltip("Time in seconds before this food automatically disappears (if <= 0, it won’t disappear automatically).")]
    public float lifetime;

    [Header("Respawn Settings")]
    [Tooltip("For food types that should reappear after being eaten (e.g., Dot), the delay in seconds.")]
    public float respawnDelay;

    [Header("Visual Effects")]
    [Tooltip("Optional: Should this food blink/pulse?")]
    public bool isBlinking;

    [Header("Variation Settings")]
    [Tooltip("Minimum scale for the food.")]
    public Vector2 minScale;
    [Tooltip("Maximum scale for the food.")]
    public Vector2 maxScale;
    [Tooltip("Randomization multiplier for respawn delay variation.")]
    public float respawnDelayVariation = 0.2f;
    [Tooltip("Randomization multiplier for lifetime variation.")]
    public float lifetimeVariation = 0.2f;

    [Header("Sponsored (optional)")]
    [Tooltip("Mark this food piece as a special Sponsored/Ad orb.")]
    public bool isSponsored = false;

    [Tooltip("The name/text that will appear on the orb when isSponsored = true.")]
    public string sponsorName = "Sponsor";

    [Tooltip("Main orb color for sponsored orbs.")]
    public Color sponsorColor = Color.white;

    [Tooltip("Text color to use for the sponsor name. If alpha == 0 this will auto-pick black/white for good contrast.")]
    public Color sponsorTextColor = new Color(0, 0, 0, 0); // alpha==0 => auto-choose



}
