using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Networking;

public class PowerUpEntity : NetworkBehaviour
{
    public CharacterStats stats;
    public EffectEntity powerUpEffect;

    private bool isDead;

    private void Awake()
    {
        var collider = GetComponent<Collider>();
        collider.isTrigger = true;
    }

    private void OnTriggerEnter(Collider other)
    {
        if (isDead)
            return;

        var character = other.GetComponent<CharacterEntity>();
        var gameplayManager = GameplayManager.Singleton;
        if (character != null && !character.isDead)
        {
            isDead = true;
            EffectEntity.PlayEffect(powerUpEffect, character.effectTransform);
            if (isServer)
            {
                character.PowerUpBombRange += stats.bombRange;
                character.PowerUpBombAmount += stats.bombAmount;
                character.PowerUpHeart += stats.heart;
                character.PowerUpMoveSpeed += stats.moveSpeed;
                // Destroy this on all clients
                NetworkServer.Destroy(gameObject);
            }
        }
    }
}
