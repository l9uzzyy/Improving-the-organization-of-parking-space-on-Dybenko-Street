using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;
using TMPro;

public class TrafficSystem : MonoBehaviour
{
    [System.Serializable]
    public class ParkingSpot
    {
        public Transform spotTransform;
        public Transform entryPoint; // Точка въезда на место
        public bool isOccupied;
        public GameObject occupiedCar;
        public Renderer indicatorRenderer;
    }

    [Header("⚙️ Настройки движения")]
    [SerializeField] private int maxVehicles = 20;
    [SerializeField] private float spawnInterval = 2f;
    [SerializeField] private float carToParkProbability = 0.3f; // 30% машин паркуются
    
    [Header("🔗 Префабы")]
    [SerializeField] private GameObject[] vehiclePrefabs;
    [SerializeField] private GameObject[] parkingCarPrefabs; // Только легковые для парковки
    
    [Header("📍 Маршруты")]
    [SerializeField] private Transform spawnPoint;
    [SerializeField] private Transform[] roadWaypoints;
    [SerializeField] private Transform exitPoint;
    
    [Header("🅿️ Парковка")]
    [SerializeField] private List<ParkingSpot> parkingSpots = new List<ParkingSpot>();
    [SerializeField] private Transform parkingEntryPoint; // Точка въезда на парковку

    [Header("📊 UI")]
    [SerializeField] private TextMeshProUGUI statsText;

    private List<GameObject> activeVehicles = new List<GameObject>();
    private Queue<GameObject> vehiclePool = new Queue<GameObject>();
    private int totalVehiclesSpawned = 0;
    private int parkedCarsCount = 0;

    private void Start()
    {
        InitializeParkingSpots();
        InitializeVehiclePool();
        StartCoroutine(VehicleSpawner());
        UpdateStatsUI();
    }

    private void InitializeParkingSpots()
    {
        GameObject[] spotObjects = GameObject.FindGameObjectsWithTag("ParkingSpot");
        
        foreach (GameObject spotObj in spotObjects)
        {
            Transform entryPoint = spotObj.transform.Find("EntryPoint");
            
            ParkingSpot newSpot = new ParkingSpot
            {
                spotTransform = spotObj.transform,
                entryPoint = entryPoint != null ? entryPoint : spotObj.transform,
                isOccupied = false,
                indicatorRenderer = spotObj.GetComponent<Renderer>()
            };

            if (newSpot.indicatorRenderer != null)
            {
                newSpot.indicatorRenderer.material.color = Color.green;
            }

            parkingSpots.Add(newSpot);
        }
        Debug.Log($"✅ Парковочных мест: {parkingSpots.Count}");
    }

    private void InitializeVehiclePool()
    {
        for (int i = 0; i < maxVehicles; i++)
        {
            GameObject vehicle = Instantiate(vehiclePrefabs[Random.Range(0, vehiclePrefabs.Length)]);
            vehicle.SetActive(false);
            vehiclePool.Enqueue(vehicle);
        }
    }

    private IEnumerator VehicleSpawner()
    {
        while (true)
        {
            yield return new WaitForSeconds(spawnInterval);
            
            if (vehiclePool.Count > 0 && activeVehicles.Count < maxVehicles)
            {
                SpawnVehicle();
            }
        }
    }

    private void SpawnVehicle()
    {
        GameObject vehicle = vehiclePool.Dequeue();
        vehicle.SetActive(true);
        vehicle.transform.position = spawnPoint.position;

        VehicleAI vehicleAI = vehicle.GetComponent<VehicleAI>();
        if (vehicleAI != null)
        {
            // Рандомно определяем, будет ли машина парковаться
            bool willPark = false;
            GameObject carPrefab = null;
            
            // Проверяем, может ли эта машина парковаться (только легковые)
            if (IsParkingCar(vehicle))
            {
                willPark = Random.Range(0f, 1f) < carToParkProbability && HasFreeSpots();
                
                if (willPark)
                {
                    carPrefab = parkingCarPrefabs[Random.Range(0, parkingCarPrefabs.Length)];
                }
            }

            vehicleAI.Initialize(this, willPark, carPrefab);
            
            if (willPark)
            {
                vehicleAI.FindParkingSpot();
            }
            else
            {
                vehicleAI.FollowRoadWaypoints();
            }
        }

        activeVehicles.Add(vehicle);
        totalVehiclesSpawned++;
        UpdateStatsUI();
    }

    private bool IsParkingCar(GameObject vehicle)
    {
        // Проверяем тег или компонент чтобы определить, может ли машина парковаться
        return vehicle.CompareTag("Car") || vehicle.GetComponent<ParkingCapability>() != null;
    }

    public ParkingSpot FindNearestFreeSpot(Vector3 position)
    {
        foreach (ParkingSpot spot in parkingSpots)
        {
            if (!spot.isOccupied)
            {
                return spot;
            }
        }
        return null;
    }

    public void OccupySpot(ParkingSpot spot, GameObject car)
    {
        spot.isOccupied = true;
        spot.occupiedCar = car;
        
        if (spot.indicatorRenderer != null)
        {
            spot.indicatorRenderer.material.color = Color.red;
        }

        parkedCarsCount++;
        UpdateStatsUI();
    }

    public void FreeSpot(ParkingSpot spot)
    {
        spot.isOccupied = false;
        spot.occupiedCar = null;
        
        if (spot.indicatorRenderer != null)
        {
            spot.indicatorRenderer.material.color = Color.green;
        }
        
        parkedCarsCount--;
    }

    public void ReturnVehicleToPool(GameObject vehicle)
    {
        vehicle.SetActive(false);
        activeVehicles.Remove(vehicle);
        vehiclePool.Enqueue(vehicle);
    }

    public Transform GetParkingEntryPoint() => parkingEntryPoint;
    public Transform[] GetRoadWaypoints() => roadWaypoints;
    public Transform GetExitPoint() => exitPoint;

    private bool HasFreeSpots()
    {
        foreach (ParkingSpot spot in parkingSpots)
        {
            if (!spot.isOccupied) return true;
        }
        return false;
    }

    private void UpdateStatsUI()
    {
        if (statsText != null)
        {
            statsText.text = $"🚗 ТРАФИК: {totalVehiclesSpawned}\n" +
                           $"🅿️ ПРИПАРКОВАНО: {parkedCarsCount}\n" +
                           $"🎯 СВОБОДНО: {parkingSpots.Count - parkedCarsCount}";
        }
    }
}