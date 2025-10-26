using UnityEngine;
using UnityEngine.U2D;  // for SpriteShapeController
using System.Collections.Generic;

[RequireComponent(typeof(SpriteShapeController))]
public class SpriteShapeSnake : MonoBehaviour
{
    public Transform head;              // assign your snake head here
    public int initialSegments = 5;     // start length
    public float segmentDistance = 0.4f;
    public int maxSegments = 100;       // max length cap

    SpriteShapeController ssc;
    List<Vector3> positions = new List<Vector3>();

    void Start()
    {
        ssc = GetComponent<SpriteShapeController>();
        // initialize with head’s starting pos
        for (int i = 0; i < initialSegments; i++)
            positions.Add(head.position);

        UpdateSpline();
    }

    void Update()
    {
        // record when head moves far enough
        if (Vector3.Distance(head.position, positions[0]) > segmentDistance)
        {
            positions.Insert(0, head.position);
            // cap history
            if (positions.Count > maxSegments) positions.RemoveAt(positions.Count - 1);
            UpdateSpline();
        }
    }

    void UpdateSpline()
    {
        var spline = ssc.spline;
        spline.Clear();  // reset all points
        // add each history pos as a control point
        for (int i = 0; i < positions.Count; i++)
        {
            Vector3 local = transform.InverseTransformPoint(positions[i]);
            spline.InsertPointAt(i, local);
            spline.SetTangentMode(i, ShapeTangentMode.Continuous);
        }
        // optional: close or cap ends?
        ssc.BakeCollider();  // if you want physics
    }

    /// <summary>
    /// Call this when you eat food to grow by N segments.
    /// </summary>
    public void Grow(int n = 1)
    {
        // extend history by repeating tail position
        for (int i = 0; i < n; i++)
            positions.Add(positions[positions.Count - 1]);
        UpdateSpline();
    }
}
