using UnityEngine;

/// <summary>
/// Attach this to your Spinner Image to rotate it continuously.
/// </summary>
public class LoadingSpinner : MonoBehaviour
{
    [Tooltip("Degrees per second to spin.")]
    public float spinSpeed = 180f;

    void Update()
    {
        // rotate around Z
        transform.Rotate(0f, 0f, spinSpeed * Time.deltaTime);
    }
}
