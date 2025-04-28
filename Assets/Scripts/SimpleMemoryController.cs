using UnityEngine;
using System.Collections.Generic;
using System.Linq; // Diperlukan untuk LastOrDefault

public class SimpleMemoryController : MonoBehaviour
{
    public int memorySize = 10; // Berapa banyak posisi terakhir yang diingat
    public float minDistanceBetweenPoints = 1.0f; // Jarak minimum antar titik memori
    public float checkAvoidDistance = 2.0f; // Jarak ke depan untuk memeriksa apakah sudah dikunjungi

    private List<Vector3> visitedPositions = new List<Vector3>();
    private RobotController movementController; // Ganti dengan nama script controller gerak Anda

    void Start()
    {
        movementController = GetComponent<RobotController>(); // Dapatkan referensi ke controller gerak
        if (movementController == null)
        {
            Debug.LogError("RobotController tidak ditemukan!");
        }
        // Catat posisi awal
        RecordPosition(transform.position);
    }

    void Update()
    {
        // --- 1. Mencatat Posisi ---
        Vector3 currentPosition = transform.position;
        Vector3 lastRecordedPosition = visitedPositions.LastOrDefault(); // Dapatkan posisi terakhir yang dicatat

        // Hanya catat jika jaraknya cukup jauh dari titik terakhir
        if (Vector3.Distance(currentPosition, lastRecordedPosition) > minDistanceBetweenPoints)
        {
            RecordPosition(currentPosition);
        }

        // --- 2. Logika Menghindari Kembali (Contoh Sederhana) ---
        // Ini adalah bagian KUNCI. Anda perlu memodifikasi logika
        // pengambilan keputusan di controller gerak Anda.

        // Contoh: Jika robot Anda biasanya berbelok secara acak atau berdasarkan sensor
        // Sekarang, sebelum memutuskan berbelok, periksa apakah arah tujuan
        // mengarah ke area yang baru saja dikunjungi.

        // Anda perlu fungsi di RobotController Anda seperti:
        // bool ShouldTurn() atau Vector3 GetBestDirection()
        // Di dalam fungsi itulah Anda akan menggunakan memori ini.
    }

    // Fungsi untuk mencatat posisi
    void RecordPosition(Vector3 position)
    {
        visitedPositions.Add(position);

        // Jaga agar ukuran memori tidak terlalu besar
        if (visitedPositions.Count > memorySize)
        {
            visitedPositions.RemoveAt(0); // Hapus posisi tertua
        }
        // Debugging: Visualisasikan jejak (opsional)
        // Debug.DrawLine(visitedPositions[visitedPositions.Count - 2], position, Color.cyan, 10f); // Uncomment jika punya >1 titik
    }

    // Fungsi yang bisa dipanggil oleh Controller Gerak Anda
    public bool IsPositionRecentlyVisited(Vector3 targetPosition, float radius)
    {
        // Periksa apakah targetPosition dekat dengan salah satu posisi dalam memori
        foreach (Vector3 visitedPos in visitedPositions)
        {
            if (Vector3.Distance(targetPosition, visitedPos) < radius)
            {
                // Debug.Log("Posisi target " + targetPosition + " dekat dengan memori " + visitedPos);
                return true; // Ya, posisi ini baru saja dikunjungi
            }
        }
        return false; // Tidak, posisi ini aman (tidak ada dalam memori terdekat)
    }

    // --- Contoh Integrasi dengan Controller Gerak (Harus Dimodifikasi) ---
    // Misalkan di RobotController Anda ada fungsi untuk memilih arah:

    /*
    void DecideNextMove()
    {
        SimpleMemoryController memory = GetComponent<SimpleMemoryController>();

        // Opsi arah potensial (contoh: lurus, belok kanan, belok kiri)
        Vector3 forwardPoint = transform.position + transform.forward * memory.checkAvoidDistance;
        Vector3 rightPoint = transform.position + transform.right * memory.checkAvoidDistance;
        Vector3 leftPoint = transform.position + (-transform.right) * memory.checkAvoidDistance;

        bool forwardVisited = memory.IsPositionRecentlyVisited(forwardPoint, memory.minDistanceBetweenPoints);
        bool rightVisited = memory.IsPositionRecentlyVisited(rightPoint, memory.minDistanceBetweenPoints);
        bool leftVisited = memory.IsPositionRecentlyVisited(leftPoint, memory.minDistanceBetweenPoints);

        // Logika Prioritas (Sangat Sederhana):
        if (!forwardVisited)
        {
            // Prioritas: Maju jika belum dikunjungi
            MoveForward();
        }
        else if (!rightVisited)
        {
            // Jika maju sudah, coba belok kanan jika belum dikunjungi
            TurnRight();
            MoveForward(); // Mungkin perlu bergerak maju setelah belok
        }
        else if (!leftVisited)
        {
            // Jika maju dan kanan sudah, coba belok kiri jika belum dikunjungi
            TurnLeft();
            MoveForward();
        }
        else
        {
            // Semua arah terdekat sudah dikunjungi!
            // Pilihan:
            // 1. Berbalik arah (paling simpel)
            // 2. Berhenti sejenak
            // 3. Pilih arah acak (meskipun sudah dikunjungi)
            TurnAround(); // Misalnya
            MoveForward();
        }
    }
    */


    // --- Visualisasi Memori (Untuk Debugging di Editor) ---
    void OnDrawGizmos()
    {
        if (visitedPositions.Count > 0)
        {
            Gizmos.color = Color.yellow;
            for (int i = 0; i < visitedPositions.Count - 1; i++)
            {
                Gizmos.DrawLine(visitedPositions[i], visitedPositions[i + 1]);
            }
            Gizmos.color = Color.red;
            Gizmos.DrawSphere(visitedPositions.LastOrDefault(), 0.2f); // Tandai titik terakhir

            // Visualisasi titik cek
            Gizmos.color = Color.blue;
            Vector3 checkPoint = transform.position + transform.forward * checkAvoidDistance;
             if(IsPositionRecentlyVisited(checkPoint, minDistanceBetweenPoints)) Gizmos.color = Color.magenta;
            Gizmos.DrawWireSphere(checkPoint, 0.3f);
        }
    }
}
