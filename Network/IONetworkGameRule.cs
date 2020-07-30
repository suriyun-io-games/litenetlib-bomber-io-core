using UnityEngine;
using LiteNetLibManager;

public class IONetworkGameRule : BaseNetworkGameRule
{
    public UIGameplay uiGameplayPrefab;

    public override bool HasOptionBotCount { get { return true; } }
    public override bool HasOptionMatchTime { get { return false; } }
    public override bool HasOptionMatchKill { get { return false; } }
    public override bool HasOptionMatchScore { get { return false; } }
    public override bool ShowZeroScoreWhenDead { get { return true; } }
    public override bool ShowZeroKillCountWhenDead { get { return true; } }
    public override bool ShowZeroAssistCountWhenDead { get { return true; } }
    public override bool ShowZeroDieCountWhenDead { get { return true; } }

    protected override BaseNetworkGameCharacter NewBot()
    {
        var gameInstance = GameInstance.Singleton;
        var botList = gameInstance.bots;
        var bot = botList[Random.Range(0, botList.Length)];
        var botEntity = Instantiate(gameInstance.botPrefab);
        botEntity.playerName = bot.name;
        botEntity.selectHead = bot.GetSelectHead();
        botEntity.selectCharacter = bot.GetSelectCharacter();
        botEntity.selectBomb = bot.GetSelectBomb();
        return botEntity;
    }

    protected override void EndMatch()
    {
    }

    public override bool CanCharacterRespawn(BaseNetworkGameCharacter character, params object[] extraParams)
    {
        var gameplayManager = GameplayManager.Singleton;
        var targetCharacter = character as CharacterEntity;
        return Time.unscaledTime - targetCharacter.deathTime >= gameplayManager.respawnDuration;
    }

    public override bool RespawnCharacter(BaseNetworkGameCharacter character, params object[] extraParams)
    {
        var isWatchedAds = false;
        if (extraParams.Length > 0 && extraParams[0] is bool)
            isWatchedAds = (bool)extraParams[0];

        var targetCharacter = character as CharacterEntity;
        var gameplayManager = GameplayManager.Singleton;
        if (!isWatchedAds || targetCharacter.watchAdsCount >= gameplayManager.watchAdsRespawnAvailable)
        {
            targetCharacter.ResetScore();
            targetCharacter.ResetKillCount();
            targetCharacter.ResetAssistCount();
            targetCharacter.ResetItemAndStats();
        }
        else
        {
            ++targetCharacter.watchAdsCount;
        }

        return true;
    }

    public override void InitialClientObjects(LiteNetLibClient client)
    {
        var ui = FindObjectOfType<UIGameplay>();
        if (ui == null && uiGameplayPrefab != null)
            ui = Instantiate(uiGameplayPrefab);
        if (ui != null)
            ui.gameObject.SetActive(true);
    }

    public override void RegisterPrefabs()
    {
        if (GameInstance.Singleton.characterPrefab != null)
            networkManager.Assets.RegisterPrefab(GameInstance.Singleton.characterPrefab.Identity);

        if (GameInstance.Singleton.botPrefab != null)
            networkManager.Assets.RegisterPrefab(GameInstance.Singleton.botPrefab.Identity);

        var bombs = GameInstance.Bombs.Values;
        foreach (var bomb in bombs)
        {
            if (bomb != null && bomb.bombPrefab != null)
                networkManager.Assets.RegisterPrefab(bomb.bombPrefab.Identity);
        }

        foreach (var obj in networkManager.Assets.GetSceneObjects())
        {
            var gameplayManager = obj.GetComponentInChildren<GameplayManager>();
            if (gameplayManager)
                gameplayManager.RegisterPrefabs();
        }
    }
}
