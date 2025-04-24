using UnityEngine;
using UnityEngine.UI;

public class MappingSystem : MonoBehaviour
{
    [Header("Map Settings")]
    public int mapSize = 100;         // Size of the map in pixels
    public float mapScale = 0.2f;     // How many world units per pixel
    public Color emptyColor = Color.white;
    public Color obstacleColor = Color.gray;
    public Color robotColor = Color.red;

    [Header("UI References")]
    public RawImage minimapDisplay;   // UI RawImage to display the map

    [Header("Sensor Settings")]
    public float scanRadius = 3.0f;   // How far to check for obstacles
    public int scanRays = 12;         // Number of rays to cast around the robot

    private Texture2D mapTexture;
    private Vector2 startPosition;
    private Vector2 currentMapPos;

    void Start()
    {
        // Create the map texture
        mapTexture = new Texture2D(mapSize, mapSize, TextureFormat.RGBA32, false);

        // Fill with empty color initially
        Color[] colors = new Color[mapSize * mapSize];
        for (int i = 0; i < colors.Length; i++)
        {
            colors[i] = emptyColor;
        }
        mapTexture.SetPixels(colors);
        mapTexture.Apply();

        // Assign to UI if available
        if (minimapDisplay != null)
        {
            minimapDisplay.texture = mapTexture;
        }

        // Store starting position
        startPosition = new Vector2(transform.position.x, transform.position.z);
        currentMapPos = new Vector2(mapSize / 2, mapSize / 2); // Start at center of map
    }

    void Update()
    {
        // Calculate current position on map
        Vector2 worldPos = new Vector2(transform.position.x, transform.position.z);
        Vector2 relativePos = worldPos - startPosition;
        currentMapPos = new Vector2(mapSize / 2, mapSize / 2) + relativePos / mapScale;

        // Scan environment and update map
        ScanAndUpdateMap();

        // Mark robot position
        int robotX = Mathf.RoundToInt(currentMapPos.x);
        int robotY = Mathf.RoundToInt(currentMapPos.y);
        if (IsInMapBounds(robotX, robotY))
        {
            mapTexture.SetPixel(robotX, robotY, robotColor);
        }

        // Apply changes to texture
        mapTexture.Apply();
    }

    void ScanAndUpdateMap()
    {
        // Cast rays in all directions around the robot
        for (int i = 0; i < scanRays; i++)
        {
            float angle = i * (360f / scanRays);
            Vector3 direction = Quaternion.Euler(0, angle, 0) * transform.forward;

            RaycastHit hit;
            if (Physics.Raycast(transform.position, direction, out hit, scanRadius))
            {
                // Hit something - mark as obstacle
                Vector3 hitPos = hit.point;
                Vector2 hitWorldPos = new Vector2(hitPos.x, hitPos.z);
                Vector2 hitRelativePos = hitWorldPos - startPosition;
                Vector2 hitMapPos = new Vector2(mapSize / 2, mapSize / 2) + hitRelativePos / mapScale;

                int mapX = Mathf.RoundToInt(hitMapPos.x);
                int mapY = Mathf.RoundToInt(hitMapPos.y);

                if (IsInMapBounds(mapX, mapY))
                {
                    mapTexture.SetPixel(mapX, mapY, obstacleColor);
                }

                // Draw line from robot to obstacle on map
                DrawLineOnMap(currentMapPos, hitMapPos, obstacleColor);
            }
            else
            {
                // No hit - mark the maximum distance as empty
                Vector3 endPos = transform.position + direction * scanRadius;
                Vector2 endWorldPos = new Vector2(endPos.x, endPos.z);
                Vector2 endRelativePos = endWorldPos - startPosition;
                Vector2 endMapPos = new Vector2(mapSize / 2, mapSize / 2) + endRelativePos / mapScale;

                // Draw line from robot to empty space
                DrawLineOnMap(currentMapPos, endMapPos, emptyColor);
            }
        }
    }

    // Helper to draw a line on the map
    void DrawLineOnMap(Vector2 from, Vector2 to, Color color)
    {
        int x0 = Mathf.RoundToInt(from.x);
        int y0 = Mathf.RoundToInt(from.y);
        int x1 = Mathf.RoundToInt(to.x);
        int y1 = Mathf.RoundToInt(to.y);

        // Bresenham's line algorithm
        int dx = Mathf.Abs(x1 - x0);
        int dy = -Mathf.Abs(y1 - y0);
        int sx = x0 < x1 ? 1 : -1;
        int sy = y0 < y1 ? 1 : -1;
        int err = dx + dy;

        while (true)
        {
            if (IsInMapBounds(x0, y0))
            {
                mapTexture.SetPixel(x0, y0, color);
            }

            if (x0 == x1 && y0 == y1) break;
            int e2 = 2 * err;
            if (e2 >= dy)
            {
                if (x0 == x1) break;
                err += dy;
                x0 += sx;
            }
            if (e2 <= dx)
            {
                if (y0 == y1) break;
                err += dx;
                y0 += sy;
            }
        }
    }

    bool IsInMapBounds(int x, int y)
    {
        return x >= 0 && x < mapSize && y >= 0 && y < mapSize;
    }
}