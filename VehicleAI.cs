using UnityEngine;
using UnityEngine.AI;
using System.Collections;

public class VehicleAI : MonoBehaviour
{
    [Header("🚗 Настройки")]
    [SerializeField] private float roadSpeed = 6f;
    [SerializeField] private float parkingSpeed = 2f;
    [SerializeField] private float rotationSpeed = 120f;
    [SerializeField] private float waitTimeBeforeLeave = 10f;
    
    private NavMeshAgent agent;
    private TrafficSystem trafficSystem;
    private TrafficSystem.ParkingSpot targetSpot;
    private bool shouldPark = false;
    private GameObject parkingCarPrefab;
    
    private Transform[] roadWaypoints;
    private int currentWaypointIndex = 0;
    
    // Состояния
    private enum VehicleState
    {
        OnRoad,
        MovingToParking,
        Parking,
        Parked,
        Leaving
    }
    private VehicleState currentState = VehicleState.OnRoad;

    private void Awake()
    {
        agent = GetComponent<NavMeshAgent>();
        agent.speed = roadSpeed;
        agent.angularSpeed = rotationSpeed;
    }

    public void Initialize(TrafficSystem system, bool willPark, GameObject carPrefab = null)
    {
        trafficSystem = system;
        shouldPark = willPark;
        parkingCarPrefab = carPrefab;
        roadWaypoints = system.GetRoadWaypoints();
    }

    public void FindParkingSpot()
    {
        if (!shouldPark) return;

        targetSpot = trafficSystem.FindNearestFreeSpot(transform.position);
        
        if (targetSpot != null)
        {
            currentState = VehicleState.MovingToParking;
            // Сначала едем к точке въезда на парковку
            agent.SetDestination(trafficSystem.GetParkingEntryPoint().position);
            StartCoroutine(CheckParkingDestination());
        }
        else
        {
            // Если нет мест - продолжаем движение по дороге
            FollowRoadWaypoints();
        }
    }

    public void FollowRoadWaypoints()
    {
        currentState = VehicleState.OnRoad;
        agent.speed = roadSpeed;
        currentWaypointIndex = 0;
        
        if (roadWaypoints != null && roadWaypoints.Length > 0)
        {
            agent.SetDestination(roadWaypoints[currentWaypointIndex].position);
            StartCoroutine(FollowWaypointsRoutine());
        }
        else
        {
            MoveToExit();
        }
    }

    private IEnumerator CheckParkingDestination()
    {
        while (currentState == VehicleState.MovingToParking || currentState == VehicleState.Parking)
        {
            if (!agent.pathPending && agent.remainingDistance <= agent.stoppingDistance)
            {
                if (currentState == VehicleState.MovingToParking)
                {
                    // Достигли въезда на парковку - начинаем парковку
                    StartParking();
                }
                else if (currentState == VehicleState.Parking)
                {
                    // Завершили парковку
                    CompleteParking();
                }
                yield break;
            }
            yield return new WaitForSeconds(0.1f);
        }
    }

    private void StartParking()
    {
        currentState = VehicleState.Parking;
        agent.speed = parkingSpeed;
        
        if (targetSpot != null && targetSpot.entryPoint != null)
        {
            // Двигаемся к точке парковки
            agent.SetDestination(targetSpot.entryPoint.position);
        }
    }

    private void CompleteParking()
    {
        currentState = VehicleState.Parked;
        trafficSystem.OccupySpot(targetSpot, gameObject);
        
        // Выравниваем машину по парковочному месту
        if (targetSpot.spotTransform != null)
        {
            transform.position = targetSpot.spotTransform.position;
            transform.rotation = targetSpot.spotTransform.rotation;
        }
        
        agent.isStopped = true;
        
        // Через время уезжаем
        StartCoroutine(WaitAndLeave());
    }

    private IEnumerator WaitAndLeave()
    {
        yield return new WaitForSeconds(waitTimeBeforeLeave);
        
        currentState = VehicleState.Leaving;
        trafficSystem.FreeSpot(targetSpot);
        agent.isStopped = false;
        agent.speed = roadSpeed;
        
        MoveToExit();
    }

    private IEnumerator FollowWaypointsRoutine()
    {
        while (currentState == VehicleState.OnRoad)
        {
            if (!agent.pathPending && agent.remainingDistance <= agent.stoppingDistance)
            {
                currentWaypointIndex++;
                
                if (currentWaypointIndex >= roadWaypoints.Length)
                {
                    MoveToExit();
                    yield break;
                }
                else
                {
                    agent.SetDestination(roadWaypoints[currentWaypointIndex].position);
                }
            }
            yield return new WaitForSeconds(0.1f);
        }
    }

    private void MoveToExit()
    {
        agent.SetDestination(trafficSystem.GetExitPoint().position);
        StartCoroutine(CheckExitReached());
    }

    private IEnumerator CheckExitReached()
    {
        while (true)
        {
            if (!agent.pathPending && agent.remainingDistance <= agent.stoppingDistance)
            {
                trafficSystem.ReturnVehicleToPool(gameObject);
                yield break;
            }
            yield return new WaitForSeconds(0.1f);
        }
    }

    // Визуализация в редакторе
    private void OnDrawGizmosSelected()
    {
        if (agent != null && agent.hasPath)
        {
            Gizmos.color = currentState == VehicleState.OnRoad ? Color.blue : Color.yellow;
            Gizmos.DrawLine(transform.position, agent.destination);
        }
    }
}