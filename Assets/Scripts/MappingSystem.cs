using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;

public class MappingSystem : MonoBehaviour
{
    [Header("Map Settings")]
    public int mapSize = 100;                       // Size of the map (texture dimensions)
    public float mapScale = 0.2f;                   // World units per pixel
    public Color initialColor = Color.white;        // Default color for unexplored areas
    public Color obstacleColor = Color.black;       // Color for detected obstacles
    public Color scannedSafeColor = new Color(0.4f, 0.4f, 0.4f, 1f); // Color for scanned safe areas
    public Color robotColor = Color.red;            // Color for the robot's position on the map

    [Header("UI References")]
    public RawImage minimapDisplay;                 // UI element to display the minimap

    [Header("Internal Sensor (Mapping Only)")]
    public float mappingScanRadius = 3.0f;          // Maximum scanning distance
    public int mappingScanRays = 12;                // Number of scan rays to shoot outward
    public LayerMask mappingObstacleLayer;          // Layer for obstacles to detect

    private Texture2D mapTexture;                   // The generated map texture
    private Vector2 worldOriginOffset;              // Offset to align world coordinates to map
    private Color[] pixelCache;                     // Cached pixel data for fast access
    private bool cacheNeedsUpdate = true;           

    void Start()
    {
        // Initialize the map texture
        mapTexture = new Texture2D(mapSize, mapSize, TextureFormat.RGBA32, false);
        mapTexture.filterMode = FilterMode.Point;
        worldOriginOffset = new Vector2(transform.position.x, transform.position.z);

        // Fill the map with the initial color
        Color[] colors = new Color[mapSize * mapSize];
        for (int i = 0; i < colors.Length; i++)
        {
            colors[i] = initialColor;
        }
        mapTexture.SetPixels(colors);
        mapTexture.Apply();

        pixelCache = mapTexture.GetPixels();
        cacheNeedsUpdate = false;

        if (minimapDisplay != null)
        {
            minimapDisplay.texture = mapTexture;
        }
    }

    void Update()
    {
        // Perform scanning and update texture each frame
        InternalEnvironmentScan();
        ApplyTextureChanges();
    }

    public Vector2Int WorldToMapCoords(Vector3 worldPos)
    {
        // Convert world position to map pixel coordinates
        Vector2 relativePos = new Vector2(worldPos.x, worldPos.z) - worldOriginOffset;
        int mapX = Mathf.RoundToInt(mapSize / 2f + relativePos.x / mapScale);
        int mapY = Mathf.RoundToInt(mapSize / 2f + relativePos.y / mapScale);
        return new Vector2Int(mapX, mapY);
    }

    public Vector3 MapCoordsToWorldPos(Vector2Int mapCoords, float desiredY)
    {
        // Convert map pixel coordinates back to world position
        float worldX = (mapCoords.x - mapSize / 2f) * mapScale + worldOriginOffset.x;
        float worldZ = (mapCoords.y - mapSize / 2f) * mapScale + worldOriginOffset.y;
        return new Vector3(worldX, desiredY, worldZ);
    }

    public Color GetPixelColor(int x, int y)
    {
        if (!IsInMapBounds(x, y))
            return obstacleColor;

        if (cacheNeedsUpdate)
        {
            pixelCache = mapTexture.GetPixels();
            cacheNeedsUpdate = false;
        }

        int index = y * mapSize + x;
        if (index >= 0 && index < pixelCache.Length)
            return pixelCache[index];

        return obstacleColor;
    }

    public Color GetColorAtWorldPos(Vector3 worldPos)
    {
        Vector2Int mapCoords = WorldToMapCoords(worldPos);
        return GetPixelColor(mapCoords.x, mapCoords.y);
    }

    private void SetPixelColor(int x, int y, Color color)
    {
        if (IsInMapBounds(x, y))
        {
            mapTexture.SetPixel(x, y, color);
            cacheNeedsUpdate = true;
        }
    }

    public void MarkAreaAsScannedSafe(Vector3 worldCenter, float worldRadius)
    {
        // Mark an area around a world position as "scanned and safe"
        Vector2Int mapCenter = WorldToMapCoords(worldCenter);
        int pixelRadius = Mathf.Max(1, Mathf.RoundToInt(worldRadius / mapScale));

        for (int xOffset = -pixelRadius; xOffset <= pixelRadius; xOffset++)
        {
            for (int yOffset = -pixelRadius; yOffset <= pixelRadius; yOffset++)
            {
                if (xOffset * xOffset + yOffset * yOffset <= pixelRadius * pixelRadius)
                {
                    int currentX = mapCenter.x + xOffset;
                    int currentY = mapCenter.y + yOffset;

                    if (IsInMapBounds(currentX, currentY))
                    {
                        Color currentColor = GetPixelColor(currentX, currentY);
                        if (currentColor != obstacleColor && currentColor != scannedSafeColor)
                        {
                            mapTexture.SetPixel(currentX, currentY, scannedSafeColor);
                            cacheNeedsUpdate = true;
                        }
                    }
                }
            }
        }
    }

    private void ApplyTextureChanges()
    {
        if (cacheNeedsUpdate)
        {
            DrawRobotPosition();
            mapTexture.Apply(false);
            pixelCache = mapTexture.GetPixels();
            cacheNeedsUpdate = false;
        }
        else
        {
            DrawRobotPosition();
            mapTexture.Apply(false);
        }
    }

    private void DrawRobotPosition()
    {
        // Mark the robot's position on the map in red
        GameObject robotObject = GameObject.FindGameObjectWithTag("Player");
        if (robotObject != null)
        {
            Vector2Int robotMapPos = WorldToMapCoords(robotObject.transform.position);

            if (IsInMapBounds(robotMapPos.x, robotMapPos.y))
            {
                mapTexture.SetPixel(robotMapPos.x, robotMapPos.y, robotColor);
                if (IsInMapBounds(robotMapPos.x + 1, robotMapPos.y)) mapTexture.SetPixel(robotMapPos.x + 1, robotMapPos.y, robotColor);
                if (IsInMapBounds(robotMapPos.x - 1, robotMapPos.y)) mapTexture.SetPixel(robotMapPos.x - 1, robotMapPos.y, robotColor);
                if (IsInMapBounds(robotMapPos.x, robotMapPos.y + 1)) mapTexture.SetPixel(robotMapPos.x, robotMapPos.y + 1, robotColor);
                if (IsInMapBounds(robotMapPos.x, robotMapPos.y - 1)) mapTexture.SetPixel(robotMapPos.x, robotMapPos.y - 1, robotColor);
            }
        }
    }

    private void InternalEnvironmentScan()
    {
        // Perform a radial scan around the robot to detect obstacles
        GameObject robotObject = GameObject.FindGameObjectWithTag("Player");
        if (robotObject == null) return;
        Vector3 scanOrigin = robotObject.transform.position;

        bool changed = false;

        for (int i = 0; i < mappingScanRays; i++)
        {
            float angle = i * (360f / mappingScanRays);
            Vector3 direction = Quaternion.Euler(0, angle, 0) * robotObject.transform.forward;

            RaycastHit hit;
            if (Physics.Raycast(scanOrigin, direction, out hit, mappingScanRadius, mappingObstacleLayer))
            {
                Vector2Int hitMapPos = WorldToMapCoords(hit.point);
                SetPixelColor(hitMapPos.x, hitMapPos.y, obstacleColor);
                changed = true;
            }
        }

        if (changed)
            cacheNeedsUpdate = true;
    }

    public bool IsInMapBounds(int x, int y)
    {
        return x >= 0 && x < mapSize && y >= 0 && y < mapSize;
    }

    void LateUpdate()
    {
        ApplyTextureChanges();
    }
}
