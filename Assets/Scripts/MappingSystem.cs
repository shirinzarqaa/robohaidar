using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic; // Untuk HashSet jika diperlukan nanti

public class MappingSystem : MonoBehaviour
{
    [Header("Map Settings")]
    public int mapSize = 100;
    public float mapScale = 0.2f;
    public Color initialColor = Color.white;    // Warna awal map (dianggap 'unknown')
    public Color obstacleColor = Color.black;   // Ganti ke hitam agar kontras?
    public Color scannedSafeColor = new Color(0.4f, 0.4f, 0.4f, 1f); // Abu-abu tua solid
    public Color robotColor = Color.red;

    [Header("UI References")]
    public RawImage minimapDisplay;

    [Header("Internal Sensor (Mapping Only)")] // Sensor internal mapping system
    public float mappingScanRadius = 3.0f;
    public int mappingScanRays = 12;
    public LayerMask mappingObstacleLayer; // Layer yg dideteksi oleh mapping system

    private Texture2D mapTexture;
    private Vector2 worldOriginOffset; // Untuk konversi world ke map (gantikan startPosition)
    // Optional: Cache warna pixel untuk pembacaan cepat
    private Color[] pixelCache;
    private bool cacheNeedsUpdate = true;

    void Start()
    {
        mapTexture = new Texture2D(mapSize, mapSize, TextureFormat.RGBA32, false);
        mapTexture.filterMode = FilterMode.Point; // Agar pixel jelas

        // Tentukan origin dunia yg akan dipetakan ke tengah map
        // Misalnya, gunakan posisi awal MappingSystem GameObject
        worldOriginOffset = new Vector2(transform.position.x, transform.position.z);

        // Isi dengan warna initial
        Color[] colors = new Color[mapSize * mapSize];
        for (int i = 0; i < colors.Length; i++)
        {
            colors[i] = initialColor;
        }
        mapTexture.SetPixels(colors);
        mapTexture.Apply();
        pixelCache = mapTexture.GetPixels(); // Inisialisasi cache
        cacheNeedsUpdate = false;

        if (minimapDisplay != null)
        {
            minimapDisplay.texture = mapTexture;
        }
    }

    void Update()
    {
        // Optional: Scan lingkungan dengan sensor internal MappingSystem
        // Ini bisa berjalan terpisah dari RobotController
        InternalEnvironmentScan();

        // Apply texture jika cache diupdate
        ApplyTextureChanges();
    }

    // Konversi koordinat dunia ke koordinat pixel map
    public Vector2Int WorldToMapCoords(Vector3 worldPos)
    {
        Vector2 relativePos = new Vector2(worldPos.x, worldPos.z) - worldOriginOffset;
        int mapX = Mathf.RoundToInt(mapSize / 2f + relativePos.x / mapScale);
        int mapY = Mathf.RoundToInt(mapSize / 2f + relativePos.y / mapScale);
        return new Vector2Int(mapX, mapY);
    }

    // Konversi koordinat pixel map ke koordinat dunia (tengah pixel)
    public Vector3 MapCoordsToWorldPos(Vector2Int mapCoords, float desiredY)
    {
        float worldX = (mapCoords.x - mapSize / 2f) * mapScale + worldOriginOffset.x;
        float worldZ = (mapCoords.y - mapSize / 2f) * mapScale + worldOriginOffset.y;
        return new Vector3(worldX, desiredY, worldZ);
    }

    // Fungsi untuk mendapatkan warna pixel (gunakan cache)
    public Color GetPixelColor(int x, int y)
    {
        if (!IsInMapBounds(x, y))
        {
            return obstacleColor; // Anggap luar map sebagai obstacle
        }
        if (cacheNeedsUpdate) // Jika cache belum diupdate oleh ApplyTextureChanges
        {
            pixelCache = mapTexture.GetPixels();
            cacheNeedsUpdate = false;
        }
        // Konversi koordinat 2D ke indeks 1D
        int index = y * mapSize + x;
        if (index >= 0 && index < pixelCache.Length) {
             return pixelCache[index];
        }
        return obstacleColor; // Fallback
    }

     // Fungsi untuk mendapatkan warna pixel dari world position
    public Color GetColorAtWorldPos(Vector3 worldPos) {
        Vector2Int mapCoords = WorldToMapCoords(worldPos);
        return GetPixelColor(mapCoords.x, mapCoords.y);
    }


    // Fungsi untuk menandai pixel dengan warna tertentu (tandai cache perlu update)
    private void SetPixelColor(int x, int y, Color color)
    {
        if (IsInMapBounds(x, y))
        {
            // Langsung set ke texture, Apply akan dipanggil nanti
             mapTexture.SetPixel(x, y, color);
             cacheNeedsUpdate = true; // Tandai cache perlu diupdate sebelum dibaca lagi
            // Note: SetPixel berulang bisa lambat, SetPixels lebih baik jika banyak
        }
    }

    // ##### FUNGSI BARU: Dipanggil oleh RobotController #####
    public void MarkAreaAsScannedSafe(Vector3 worldCenter, float worldRadius)
    {
        Vector2Int mapCenter = WorldToMapCoords(worldCenter);
        int pixelRadius = Mathf.Max(1, Mathf.RoundToInt(worldRadius / mapScale)); // Radius dalam pixel, minimal 1

        // List untuk menampung perubahan pixel (lebih efisien)
        // List<KeyValuePair<Vector2Int, Color>> pixelsToSet = new List<KeyValuePair<Vector2Int, Color>>();

        for (int xOffset = -pixelRadius; xOffset <= pixelRadius; xOffset++)
        {
            for (int yOffset = -pixelRadius; yOffset <= pixelRadius; yOffset++)
            {
                // Cek dalam lingkaran
                if (xOffset * xOffset + yOffset * yOffset <= pixelRadius * pixelRadius)
                {
                    int currentX = mapCenter.x + xOffset;
                    int currentY = mapCenter.y + yOffset;

                    if (IsInMapBounds(currentX, currentY))
                    {
                        // Hanya ubah jika warnanya BUKAN obstacle
                        Color currentColor = GetPixelColor(currentX, currentY); // Baca dari cache
                        if (currentColor != obstacleColor)
                        {
                             // Tandai untuk diubah ke scannedSafeColor
                             // pixelsToSet.Add(new KeyValuePair<Vector2Int, Color>(new Vector2Int(currentX, currentY), scannedSafeColor));

                             // Versi sederhana (langsung set, kurang efisien jika radius besar)
                             if(currentColor != scannedSafeColor) { // Hindari set warna yg sama
                                 mapTexture.SetPixel(currentX, currentY, scannedSafeColor);
                                 cacheNeedsUpdate = true;
                             }
                        }
                    }
                }
            }
        }

        // Versi efisien:
        // ApplyPixels(pixelsToSet);

        // Tandai cache perlu update (sudah dilakukan di SetPixel sederhana)
        // cacheNeedsUpdate = true;
    }

    // Optional: Fungsi untuk apply batch pixel (lebih efisien)
    /*
    private void ApplyPixels(List<KeyValuePair<Vector2Int, Color>> pixels) {
        if (pixels.Count == 0) return;

        // Optimasi: Baca semua pixel dulu, ubah di array, lalu SetPixels
        Color[] currentPixels = mapTexture.GetPixels();
        foreach (var kvp in pixels) {
            int index = kvp.Key.y * mapSize + kvp.Key.x;
            if (index >= 0 && index < currentPixels.Length) {
                currentPixels[index] = kvp.Value;
            }
        }
        mapTexture.SetPixels(currentPixels);
        cacheNeedsUpdate = true;
    }
    */


    // Apply perubahan texture jika ada (bisa dipanggil di LateUpdate atau akhir Update)
    private void ApplyTextureChanges()
    {
        if (cacheNeedsUpdate) // Hanya apply jika ada perubahan tercatat
        {
             // Gambar posisi robot terakhir sebelum apply
             DrawRobotPosition(); // Gambar posisi robot di frame ini

             mapTexture.Apply(false); // false = don't update mipmaps (lebih cepat)
             pixelCache = mapTexture.GetPixels(); // Update cache setelah apply
             cacheNeedsUpdate = false; // Cache sudah sinkron
        } else {
            // Jika tidak ada perubahan texture, mungkin hanya posisi robot yg perlu diupdate?
            // Ini tricky, menggambar robot tanpa apply akan hilang.
            // Solusi: Selalu gambar robot dan selalu Apply(), atau cari cara lain update robot.
             DrawRobotPosition(); // Tetap gambar posisi robot
             mapTexture.Apply(false); // Tetap apply agar posisi robot terlihat
             // pixelCache tidak perlu diupdate jika hanya robot yg berubah
        }
    }

    // Gambar posisi robot (dipanggil sebelum ApplyTextureChanges)
    private void DrawRobotPosition() {
         // Dapatkan posisi robot dari GameObject RobotController (perlu referensi)
         // Misalnya, kita asumsikan ada referensi:
         GameObject robotObject = GameObject.FindGameObjectWithTag("Player"); // Cari robot (kurang efisien)
         if (robotObject != null) {
             Vector2Int robotMapPos = WorldToMapCoords(robotObject.transform.position);
              // Gambar titik kecil atau icon
              if(IsInMapBounds(robotMapPos.x, robotMapPos.y)) {
                    // Kembalikan pixel di bawah robot ke warna aslinya dulu (perlu cara menyimpan warna asli)
                    // Atau gambar saja di atasnya
                    mapTexture.SetPixel(robotMapPos.x, robotMapPos.y, robotColor);
                    // Mungkin gambar + kecil di sekitarnya
                    if(IsInMapBounds(robotMapPos.x+1, robotMapPos.y)) mapTexture.SetPixel(robotMapPos.x+1, robotMapPos.y, robotColor);
                    if(IsInMapBounds(robotMapPos.x-1, robotMapPos.y)) mapTexture.SetPixel(robotMapPos.x-1, robotMapPos.y, robotColor);
                    if(IsInMapBounds(robotMapPos.x, robotMapPos.y+1)) mapTexture.SetPixel(robotMapPos.x, robotMapPos.y+1, robotColor);
                    if(IsInMapBounds(robotMapPos.x, robotMapPos.y-1)) mapTexture.SetPixel(robotMapPos.x, robotMapPos.y-1, robotColor);
                    // cacheNeedsUpdate = true; // Tandai perlu apply (sudah ditangani di ApplyTextureChanges)
              }
         }
    }


    // Fungsi scan internal MappingSystem (untuk menggambar obstacle)
    private void InternalEnvironmentScan()
    {
        // Dapatkan posisi robot (atau posisi MappingSystem jika itu yg scan)
        GameObject robotObject = GameObject.FindGameObjectWithTag("Player");
        if (robotObject == null) return;
        Vector3 scanOrigin = robotObject.transform.position;
        Vector2Int mapOrigin = WorldToMapCoords(scanOrigin);

        bool changed = false;

        for (int i = 0; i < mappingScanRays; i++)
        {
            float angle = i * (360f / mappingScanRays);
            Vector3 direction = Quaternion.Euler(0, angle, 0) * robotObject.transform.forward; // Arah relatif thdp robot

            RaycastHit hit;
            Vector2Int endMapPos;
            Color lineColor;

            if (Physics.Raycast(scanOrigin, direction, out hit, mappingScanRadius, mappingObstacleLayer))
            {
                // Kena obstacle
                Vector2Int hitMapPos = WorldToMapCoords(hit.point);
                SetPixelColor(hitMapPos.x, hitMapPos.y, obstacleColor); // Tandai obstacle
                endMapPos = hitMapPos;
                lineColor = obstacleColor; // Gambar garis ke obstacle (opsional)
                 changed = true;
            }
            else
            {
                // Tidak kena (ruang kosong menurut scan ini)
                Vector3 endWorldPos = scanOrigin + direction * mappingScanRadius;
                endMapPos = WorldToMapCoords(endWorldPos);
                // Jangan ubah warna area kosong di sini, biarkan RobotController yg menandai scannedSafeColor
                // lineColor = initialColor; // Gambar garis ke ujung scan (opsional)
            }

            // Gambar garis di map (opsional, bisa membuat map ramai)
            // DrawLineOnMap(mapOrigin, endMapPos, lineColor);
            // if(lineColor != initialColor) changed = true;
        }
         if(changed) cacheNeedsUpdate = true;
    }

    // Gambar garis Bresenham (Helper, mungkin tidak dipakai jika tidak menggambar garis scan)
    /*
    void DrawLineOnMap(Vector2Int from, Vector2Int to, Color color) { ... }
    */

    bool IsInMapBounds(int x, int y)
    {
        return x >= 0 && x < mapSize && y >= 0 && y < mapSize;
    }

     // Panggil Apply di akhir frame agar semua perubahan tergambar
     void LateUpdate() {
         ApplyTextureChanges();
     }
}