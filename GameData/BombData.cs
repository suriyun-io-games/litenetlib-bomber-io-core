using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Networking;

public class BombData : ItemData
{
    public BombEntity bombPrefab;

    public BombEntity Plant(CharacterEntity planter, Vector3 position)
    {
        if (planter == null)
            return null;
        var bombEntity = Instantiate(bombPrefab);
        bombEntity.transform.position = new Vector3(
            Mathf.RoundToInt(position.x),
            position.y, 
            Mathf.RoundToInt(position.z));
        bombEntity.addBombRange = planter.PowerUpBombRange;
        bombEntity.planterNetId = planter.netId;
        // We're going to use velocy in case that client cannot find `Attacker` entity
        NetworkServer.Spawn(bombEntity.gameObject);
        return bombEntity;
    }
}
