using System.Collections;
using UnityEngine;
using UnityEngine.AI;
 
public class CarSpawningPad : MonoBehaviour
{
    [Header("Spawn Settings")]
    [SerializeField] private Transform spawnPoint;
    [SerializeField] private Transform despawnPoint;
 
    [Header("Models")]
    [SerializeField] private GameObject[] modelPrefabs = new GameObject[5];
 
    [Header("Weighted chances")]
    [Tooltip("If filled, must match modelPrefabs length. Higher = more likely.")]
    [SerializeField] private float[] spawnWeights;
 
    [Header("Timing")]
    [SerializeField] private float minSpawnInterval = 2f;
    [SerializeField] private float maxSpawnInterval = 4f;
    [SerializeField] private float reachThreshold = 0.5f;
    
    private void Start()
    {
        StartCoroutine(SpawnLoop());
    }
 
    private IEnumerator SpawnLoop()
    {
        while (true)
        {
            SpawnRandomModel();
            float waitTime = Random.Range(minSpawnInterval, maxSpawnInterval);
            yield return new WaitForSeconds(waitTime);
        }
    }
 
    private void OnValidate()
    {
        if (maxSpawnInterval < minSpawnInterval)
            maxSpawnInterval = minSpawnInterval;
    }
 
    private void SpawnRandomModel()
    {
        if (modelPrefabs == null || modelPrefabs.Length == 0 || spawnPoint == null || despawnPoint == null)
        {
            Debug.LogWarning("CarSpawningPad: missing prefabs or points.");
            return;
        }
 
        GameObject chosenPrefab = GetRandomModel();
        if (chosenPrefab == null) return;
 
        GameObject instance = Instantiate(chosenPrefab, spawnPoint.position, spawnPoint.rotation);
 
        NavMeshAgent agent = instance.GetComponent<NavMeshAgent>();
        if (agent == null)
        {
            Debug.LogError($"CarSpawningPad: {chosenPrefab.name} has no NavMeshAgent component!");
            Destroy(instance);
            return;
        }
 
        // Make sure it's actually placed on the navmesh
        if (!agent.isOnNavMesh)
        {
            if (NavMesh.SamplePosition(spawnPoint.position, out NavMeshHit hit, 5f, NavMesh.AllAreas))
            {
                agent.Warp(hit.position);
            }
        }
 
        agent.SetDestination(despawnPoint.position);
 
        StartCoroutine(DespawnWhenArrived(instance, agent));
    }
 
    private GameObject GetRandomModel()
    {
        // Weighted random if weights array is provided and valid
        if (spawnWeights != null && spawnWeights.Length == modelPrefabs.Length)
        {
            float total = 0f;
            foreach (float w in spawnWeights) total += w;
 
            float roll = Random.Range(0f, total);
            float cumulative = 0f;
 
            for (int i = 0; i < modelPrefabs.Length; i++)
            {
                cumulative += spawnWeights[i];
                if (roll <= cumulative)
                    return modelPrefabs[i];
            }
 
            return modelPrefabs[modelPrefabs.Length - 1];
        }
 
        // Equal chance fallback
        int index = Random.Range(0, modelPrefabs.Length);
        return modelPrefabs[index];
    }
 
    private IEnumerator DespawnWhenArrived(GameObject instance, NavMeshAgent agent)
    {
        // Wait until the agent has calculated a path
        while (agent.pathPending)
            yield return null;
 
        while (instance != null &&
               (agent.remainingDistance > reachThreshold || agent.pathStatus != NavMeshPathStatus.PathComplete))
        {
            yield return null;
        }
 
        if (instance != null)
            Destroy(instance);
    }
}