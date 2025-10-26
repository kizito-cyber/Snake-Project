using UnityEngine;
using Photon.Pun;

/// <summary>
/// Attach this to your “CameraHolder” GameObject (which has NO Camera component).
/// Drag the child MainCamera into the inspector’s “Child Camera” slot.
/// </summary>
public class CameraFollow : MonoBehaviour
{
    [Header("Follow Settings")]
    [SerializeField] private float smoothSpeed = 5f;
    [SerializeField] private Vector3 offset = new Vector3(0f, 0f, -10f);

    [Header("Zoom Settings")]
    [Tooltip("Min orthographic size when the snake is at its smallest.")]
    [SerializeField] private float minZoom = 5f;
    [Tooltip("Max orthographic size when the snake is at or above maxZoomScore.")]
    [SerializeField] private float maxZoom = 15f;
    [Tooltip("Snake score at which camera is fully zoomed out to maxZoom.")]
    [SerializeField] private int maxZoomScore = 200;

    [Header("Child Camera Reference")]
    [Tooltip("Drag your child MainCamera here (the one with Shaker + Camera component).")]
    [SerializeField] private Camera childCamera;

    private Transform followTarget;
    private SnakeController snakeController;

    void Awake()
    {
        if (childCamera == null)
        {
            Debug.LogWarning("[CameraFollow] Child Camera reference is missing. Drag your MainCamera into 'Child Camera'.");
        }
        else if (!childCamera.orthographic)
        {
            Debug.LogWarning("[CameraFollow] Child Camera should be Orthographic for zooming to work.");
        }

        // Make sure we start at the minimum zoom
        if (childCamera != null)
            childCamera.orthographicSize = minZoom;
    }

    void LateUpdate()
    {
        if (followTarget == null) return;

        // 1) Smoothly move the holder so that its world position follows the target
        Vector3 desiredPos = new Vector3(followTarget.position.x, followTarget.position.y, offset.z);
        transform.position = Vector3.Lerp(transform.position, desiredPos, Time.deltaTime * smoothSpeed);

        // 2) If we haven’t cached the SnakeController yet, grab it now
        if (snakeController == null && followTarget != null)
        {
            snakeController = followTarget.GetComponent<SnakeController>();
        }

        // 3) If we have a SnakeController, calculate zoom factor
        if (snakeController != null && childCamera != null)
        {
            float t = Mathf.Clamp01((float)snakeController.currentScore / maxZoomScore);
            float desiredSize = Mathf.Lerp(minZoom, maxZoom, t);
            childCamera.orthographicSize = Mathf.Lerp(childCamera.orthographicSize,
                                                      desiredSize,
                                                      Time.deltaTime * smoothSpeed);
        }
    }

    /// <summary>
    /// Call this from GameManager (or wherever you spawn your local snake) immediately after instantiation.
    /// </summary>
    public void SetTarget(Transform newTarget)
    {
        followTarget = newTarget;
        snakeController = null;           // force re-fetch in next LateUpdate
        if (childCamera != null)
            childCamera.orthographicSize = minZoom;  // snap camera to minZoom immediately
    }
}
