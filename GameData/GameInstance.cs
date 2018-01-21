using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Networking;

public class GameInstance : BaseNetworkGameInstance
{
    public static GameInstance Singleton { get; private set; }
    public CharacterEntity characterPrefab;
    public BotEntity botPrefab;
    public CharacterData[] characters;
    public HeadData[] heads;
    public BombData[] bombs;
    public BotData[] bots;
    [Tooltip("Physic layer for characters to avoid it collision")]
    public int characterLayer = 8;
    public int bombLayer = 9;
    public int brickLayer = 10;
    public int mapLayer = 11;
    public bool showJoystickInEditor = true;
    public string watchAdsRespawnPlacement = "respawnPlacement";
    // An available list, list of item that already unlocked
    public static readonly List<HeadData> AvailableHeads = new List<HeadData>();
    public static readonly List<CharacterData> AvailableCharacters = new List<CharacterData>();
    public static readonly List<BombData> AvailableBombs = new List<BombData>();
    // All item list
    public static readonly Dictionary<string, HeadData> Heads = new Dictionary<string, HeadData>();
    public static readonly Dictionary<string, CharacterData> Characters = new Dictionary<string, CharacterData>();
    public static readonly Dictionary<string, BombData> Bombs = new Dictionary<string, BombData>();
    protected override void Awake()
    {
        base.Awake();
        if (Singleton != null)
        {
            Destroy(gameObject);
            return;
        }
        Singleton = this;
        DontDestroyOnLoad(gameObject);
        Physics.IgnoreLayerCollision(characterLayer, characterLayer, true);

        if (!ClientScene.prefabs.ContainsValue(characterPrefab.gameObject))
            ClientScene.RegisterPrefab(characterPrefab.gameObject);

        if (!ClientScene.prefabs.ContainsValue(botPrefab.gameObject))
            ClientScene.RegisterPrefab(botPrefab.gameObject);

        Heads.Clear();
        foreach (var head in heads)
        {
            Heads.Add(head.GetId(), head);
        }

        Characters.Clear();
        foreach (var characterModel in characters)
        {
            Characters.Add(characterModel.GetId(), characterModel);
        }

        Bombs.Clear();
        foreach (var bomb in bombs)
        {
            Bombs.Add(bomb.GetId(), bomb);
            var bombPrefab = bomb.bombPrefab;
            if (bombPrefab != null && !ClientScene.prefabs.ContainsValue(bombPrefab.gameObject))
                ClientScene.RegisterPrefab(bombPrefab.gameObject);
        }

        UpdateAvailableItems();
        ValidatePlayerSave();
    }

    public void UpdateAvailableItems()
    {
        AvailableHeads.Clear();
        foreach (var helmet in heads)
        {
            if (helmet != null && helmet.IsUnlock())
                AvailableHeads.Add(helmet);
        }

        AvailableCharacters.Clear();
        foreach (var character in characters)
        {
            if (character != null && character.IsUnlock())
                AvailableCharacters.Add(character);
        }

        AvailableBombs.Clear();
        foreach (var bomb in bombs)
        {
            if (bomb != null && bomb.IsUnlock())
                AvailableBombs.Add(bomb);
        }
    }

    public void ValidatePlayerSave()
    {
        var head = PlayerSave.GetHead();
        if (head < 0 || head >= AvailableHeads.Count)
            PlayerSave.SetHead(0);

        var character = PlayerSave.GetCharacter();
        if (character < 0 || character >= AvailableCharacters.Count)
            PlayerSave.SetCharacter(0);

        var bomb = PlayerSave.GetBomb();
        if (bomb < 0 || bomb >= AvailableBombs.Count)
            PlayerSave.SetBomb(0);
    }

    public static HeadData GetHead(string key)
    {
        if (Heads.Count == 0)
            return null;
        HeadData result;
        Heads.TryGetValue(key, out result);
        return result;
    }

    public static CharacterData GetCharacter(string key)
    {
        if (Characters.Count == 0)
            return null;
        CharacterData result;
        Characters.TryGetValue(key, out result);
        return result;
    }

    public static BombData GetBomb(string key)
    {
        if (Bombs.Count == 0)
            return null;
        BombData result;
        Bombs.TryGetValue(key, out result);
        return result;
    }

    public static HeadData GetAvailableHead(int index)
    {
        if (AvailableHeads.Count == 0)
            return null;
        if (index <= 0 || index >= AvailableHeads.Count)
            index = 0;
        return AvailableHeads[index];
    }

    public static CharacterData GetAvailableCharacter(int index)
    {
        if (AvailableCharacters.Count == 0)
            return null;
        if (index <= 0 || index >= AvailableCharacters.Count)
            index = 0;
        return AvailableCharacters[index];
    }

    public static BombData GetAvailableBomb(int index)
    {
        if (AvailableBombs.Count == 0)
            return null;
        if (index <= 0 || index >= AvailableBombs.Count)
            index = 0;
        return AvailableBombs[index];
    }
}
