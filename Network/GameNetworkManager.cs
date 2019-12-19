﻿using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Networking;

[RequireComponent(typeof(GameNetworkDiscovery))]
public class GameNetworkManager : BaseNetworkGameManager
{
    public static new GameNetworkManager Singleton
    {
        get { return singleton as GameNetworkManager; }
    }

    private JoinMessage MakeJoinMessage()
    {
        var msg = new JoinMessage();
        msg.playerName = PlayerSave.GetPlayerName();
        var headData = GameInstance.GetAvailableHead(PlayerSave.GetHead());
        var characterData = GameInstance.GetAvailableCharacter(PlayerSave.GetCharacter());
        var bombData = GameInstance.GetAvailableBomb(PlayerSave.GetBomb());
        msg.selectHead = headData != null ? headData.GetHashId() : 0;
        msg.selectCharacter = characterData != null ? characterData.GetHashId() : 0;
        msg.selectBomb = bombData != null ? bombData.GetHashId() : 0;
        // Custom Equipments
        var savedCustomEquipments = PlayerSave.GetCustomEquipments();
        var selectCustomEquipments = new List<int>();
        foreach (var savedCustomEquipment in savedCustomEquipments)
        {
            var data = GameInstance.GetAvailableCustomEquipment(savedCustomEquipment.Value);
            if (data != null)
                selectCustomEquipments.Add(data.GetHashId());
        }
        msg.selectCustomEquipments = selectCustomEquipments.ToArray();
        return msg;
    }

    public override void OnClientConnect(NetworkConnection conn)
    {
        if (!clientLoadedScene)
        {
            // Ready/AddPlayer is usually triggered by a scene load completing. if no scene was loaded, then Ready/AddPlayer it here instead.
            ClientScene.Ready(conn);
            ClientScene.AddPlayer(conn, 0, MakeJoinMessage());
        }
    }

    public override void OnClientSceneChanged(NetworkConnection conn)
    {
        // always become ready.
        ClientScene.Ready(conn);

        bool addPlayer = (ClientScene.localPlayers.Count == 0);
        bool foundPlayer = false;
        for (int i = 0; i < ClientScene.localPlayers.Count; i++)
        {
            if (ClientScene.localPlayers[i].gameObject != null)
            {
                foundPlayer = true;
                break;
            }
        }
        // there are players, but their game objects have all been deleted
        if (!foundPlayer)
            addPlayer = true;

        if (addPlayer)
            ClientScene.AddPlayer(conn, 0, MakeJoinMessage());
    }

    protected override BaseNetworkGameCharacter NewCharacter(NetworkReader extraMessageReader)
    {
        var joinMessage = extraMessageReader.ReadMessage<JoinMessage>();
        var character = Instantiate(GameInstance.Singleton.characterPrefab);
        character.playerName = joinMessage.playerName;
        character.selectHead = joinMessage.selectHead;
        character.selectCharacter = joinMessage.selectCharacter;
        character.selectBomb = joinMessage.selectBomb;
        foreach (var customEquipment in joinMessage.selectCustomEquipments)
        {
            character.selectCustomEquipments.Add(customEquipment);
        }
        character.extra = joinMessage.extra;
        return character;
    }

    protected override void UpdateScores(NetworkGameScore[] scores)
    {
        var rank = 0;
        foreach (var score in scores)
        {
            ++rank;
            if (BaseNetworkGameCharacter.Local != null && score.netId.Equals(BaseNetworkGameCharacter.Local.netId))
            {
                (BaseNetworkGameCharacter.Local as CharacterEntity).rank = rank;
                break;
            }
        }
        var uiGameplay = FindObjectOfType<UIGameplay>();
        if (uiGameplay != null)
            uiGameplay.UpdateRankings(scores);
    }

    protected override void KillNotify(string killerName, string victimName, string weaponId)
    {
        var uiGameplay = FindObjectOfType<UIGameplay>();
        if (uiGameplay != null)
            uiGameplay.KillNotify(killerName, victimName, weaponId);
    }

    [System.Serializable]
    public class JoinMessage : MessageBase
    {
        public string playerName;
        public int selectHead;
        public int selectCharacter;
        public int selectBomb;
        public int[] selectCustomEquipments;
        public string extra;
    }
}
