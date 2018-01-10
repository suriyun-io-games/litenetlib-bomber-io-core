using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Networking;

public class GameplayManager : NetworkBehaviour
{
    public const float REAL_MOVE_SPEED_RATE = 0.1f;
    public static GameplayManager Singleton { get; private set; }
    [Header("Character stats")]
    public int minMoveSpeed = 30;
    public int addMoveSpeedPerPowerUp = 5;
    [Header("Powerups")]
    public int maxBombRangePowerUp = 9;
    public int maxBombAmountPowerUp = 9;
    public int maxHeartPowerUp = 9;
    public int maxMoveSpeedPowerUp = 9;
    [Header("UI")]
    public UIGameplay uiGameplay;
    [Header("Game rules")]
    public int killScore = 15;
    public int suicideScore = -20;
    public int watchAdsRespawnAvailable = 2;
    public float updateScoreDuration = 1f;
    public float brickRespawnDuration = 30f;
    public float respawnDuration = 5f;
    public float invincibleDuration = 1.5f;
    public Transform[] characterSpawnPositions;
    public PowerUpSpawnData[] powerUps;
    public int noDropPowerUpWeight = 1;
    public readonly Dictionary<PowerUpEntity, int> powerUpDropWeights = new Dictionary<PowerUpEntity, int>();

    private void Awake()
    {
        if (Singleton != null)
        {
            Destroy(gameObject);
            return;
        }
        Singleton = this;

        powerUpDropWeights.Clear();
        foreach (var powerUp in powerUps)
        {
            var powerUpPrefab = powerUp.powerUpPrefab;
            if (powerUpPrefab != null && !ClientScene.prefabs.ContainsValue(powerUpPrefab.gameObject))
                ClientScene.RegisterPrefab(powerUpPrefab.gameObject);
            if (powerUpPrefab != null && !powerUpDropWeights.ContainsKey(powerUpPrefab))
                powerUpDropWeights.Add(powerUpPrefab, powerUp.randomWeight);
        }
    }

    public void SpawnPowerUp(Vector3 position)
    {
        if (!isServer)
            return;
        
        var randomizer = WeightedRandomizer.From(powerUpDropWeights);
        randomizer.noResultWeight = noDropPowerUpWeight;
        var powerUpPrefab = randomizer.TakeOne();
        SpawnPowerUp(powerUpPrefab, position);
    }

    public void SpawnPowerUp(PowerUpEntity powerUpPrefab, Vector3 position)
    {
        if (powerUpPrefab != null)
        {
            var powerUpEntity = Instantiate(powerUpPrefab, position, Quaternion.identity);
            NetworkServer.Spawn(powerUpEntity.gameObject);
        }
    }

    public Vector3 GetCharacterSpawnPosition(CharacterEntity character)
    {
        if (characterSpawnPositions == null || characterSpawnPositions.Length == 0)
            return Vector3.zero;
        return characterSpawnPositions[Random.Range(0, characterSpawnPositions.Length)].position;
    }
}
