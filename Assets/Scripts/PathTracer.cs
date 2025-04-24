using UnityEngine;
using System.Collections.Generic;

public class PathTracer : MonoBehaviour
{
    public float markerSpacing = 0.5f;    // Distance between each trail point
    public Color trailColor = Color.blue;
    public float trailWidth = 0.1f;
    public int maxTrailPoints = 1000;     // Maximum number of points

    private Vector3 lastMarkerPosition;
    private LineRenderer lineRenderer;

    void Start()
    {
        lastMarkerPosition = transform.position;

        // Setup line renderer
        lineRenderer = gameObject.AddComponent<LineRenderer>();
        lineRenderer.startWidth = trailWidth;
        lineRenderer.endWidth = trailWidth;
        lineRenderer.material = new Material(Shader.Find("Sprites/Default"));
        lineRenderer.startColor = trailColor;
        lineRenderer.endColor = trailColor;
        lineRenderer.positionCount = 1;
        lineRenderer.SetPosition(0, transform.position);
    }

    void Update()
    {
        // Check if we've moved far enough to place a new trail point
        if (Vector3.Distance(transform.position, lastMarkerPosition) >= markerSpacing)
        {
            // Add position to line renderer
            lineRenderer.positionCount = lineRenderer.positionCount + 1;
            lineRenderer.SetPosition(lineRenderer.positionCount - 1, transform.position);

            lastMarkerPosition = transform.position;

            // Manage line renderer point count
            if (lineRenderer.positionCount > maxTrailPoints)
            {
                // Remove oldest point
                Vector3[] positions = new Vector3[lineRenderer.positionCount - 1];
                for (int i = 1; i < lineRenderer.positionCount; i++)
                {
                    positions[i - 1] = lineRenderer.GetPosition(i);
                }
                lineRenderer.positionCount = positions.Length;
                lineRenderer.SetPositions(positions);
            }
        }
    }

    // Call this if you want to clear the trail
    public void ClearTrail()
    {
        lineRenderer.positionCount = 1;
        lineRenderer.SetPosition(0, transform.position);
        lastMarkerPosition = transform.position;
    }
}