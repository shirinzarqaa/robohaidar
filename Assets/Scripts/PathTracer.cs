using UnityEngine;
using System.Collections.Generic;

public class PathTracer : MonoBehaviour
{
    public float markerSpacing = 0.5f;   
    public Color trailColor = Color.blue;
    public float trailWidth = 0.1f;
    public int maxTrailPoints = 1000;     
    private Vector3 lastMarkerPosition;
    private LineRenderer lineRenderer;

    void Start()
    {
        lastMarkerPosition = transform.position;
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
        if (Vector3.Distance(transform.position, lastMarkerPosition) >= markerSpacing)
        {
            lineRenderer.positionCount = lineRenderer.positionCount + 1;
            lineRenderer.SetPosition(lineRenderer.positionCount - 1, transform.position);

            lastMarkerPosition = transform.position;
            if (lineRenderer.positionCount > maxTrailPoints)
            {
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

    public void ClearTrail()
    {
        lineRenderer.positionCount = 1;
        lineRenderer.SetPosition(0, transform.position);
        lastMarkerPosition = transform.position;
    }
}