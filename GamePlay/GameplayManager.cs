using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Networking;

public class GameplayManager : NetworkBehaviour
{
    public const float REAL_MOVE_SPEED_RATE = 0.1f;
    public const int RANKING_AMOUNT = 5;
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
    public int botCount = 10;
    public int watchAdsRespawnAvailable = 2;
    public float updateScoreDuration = 1f;
    public float brickRespawnDuration = 30f;
    public float respawnDuration = 5f;
    public float invincibleDuration = 1.5f;
    public Transform[] characterSpawnPositions;
    public PowerUpSpawnData[] powerUps;
    public int noDropPowerUpWeight = 1;
    public readonly List<CharacterEntity> characters = new List<CharacterEntity>();
    public readonly Dictionary<PowerUpEntity, int> powerUpDropWeights = new Dictionary<PowerUpEntity, int>();
    private UserRanking[] userRankings = new UserRanking[RANKING_AMOUNT];
    // Private
    private float lastUpdateScoreTime;

    private void Awake()
    {
        if (Singleton != null)
        {
            Destroy(gameObject);
            return;
        }
        Singleton = this;
        lastUpdateScoreTime = Time.unscaledTime;

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

    public override void OnStartServer()
    {
        var gameInstance = GameInstance.Singleton;
        var botList = gameInstance.bots;
        var characterKeys = GameInstance.Characters.Keys;
        for (var i = 0; i < botCount; ++i)
        {
            var bot = botList[Random.Range(0, botList.Length)];
            var botEntity = Instantiate(gameInstance.botPrefab);
            botEntity.playerName = bot.name;
            botEntity.selectHead = bot.GetSelectHead();
            botEntity.selectBomb = bot.GetSelectBomb();
            botEntity.selectCharacter = bot.GetSelectCharacter();
            NetworkServer.Spawn(botEntity.gameObject);
            Singleton.characters.Add(botEntity);
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

    private void Update()
    {
        if (Time.unscaledTime - lastUpdateScoreTime >= updateScoreDuration)
        {
            if (isServer)
                UpdateScores();
            lastUpdateScoreTime = Time.unscaledTime;
        }
    }

    private void UpdateScores()
    {
        characters.Sort();
        userRankings = new UserRanking[RANKING_AMOUNT];
        for (var i = 0; i < RANKING_AMOUNT; ++i)
        {
            if (i >= characters.Count)
                break;
            var character = characters[i];
            var ranking = new UserRanking();
            ranking.netId = character.netId;
            ranking.playerName = character.playerName;
            ranking.score = character.Score;
            ranking.killCount = character.killCount;
            userRankings[i] = ranking;
        }
        RpcUpdateRankings(userRankings);
    }

    public Vector3 GetCharacterSpawnPosition()
    {
        if (characterSpawnPositions == null || characterSpawnPositions.Length == 0)
            return Vector3.zero;
        return characterSpawnPositions[Random.Range(0, characterSpawnPositions.Length)].position;
    }
    
    public void UpdateRank(NetworkInstanceId netId)
    {
        var target = NetworkServer.FindLocalObject(netId);
        if (target == null)
            return;
        var character = target.GetComponent<CharacterEntity>();
        if (character == null)
            return;
        var ranking = new UserRanking();
        ranking.netId = character.netId;
        ranking.playerName = character.playerName;
        ranking.score = character.Score;
        ranking.killCount = character.killCount;
        if (character.connectionToClient != null)
            TargetUpdateLocalRank(character.connectionToClient, characters.IndexOf(character) + 1, ranking);
    }

    [ClientRpc]
    private void RpcUpdateRankings(UserRanking[] userRankings)
    {
        if (uiGameplay != null)
            uiGameplay.UpdateRankings(userRankings);
    }

    [TargetRpc]
    private void TargetUpdateLocalRank(NetworkConnection conn, int rank, UserRanking ranking)
    {
        if (uiGameplay != null)
            uiGameplay.UpdateLocalRank(rank, ranking);
    }
}
