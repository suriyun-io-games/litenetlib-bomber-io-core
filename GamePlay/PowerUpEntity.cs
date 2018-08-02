using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Networking;

public class PowerUpEntity : NetworkBehaviour
{
    public const float DestroyDelay = 1f;
    [Header("Stats / Currencies")]
    public CharacterStats stats;
    public InGameCurrency[] currencies;
    [Header("Effect")]
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
            }
            if (character.isLocalPlayer)
            {
                foreach (var currency in currencies)
                {
                    MonetizationManager.Save.AddCurrency(currency.id, currency.amount);
                }
            }
            StartCoroutine(DestroyRoutine());
        }
    }

    IEnumerator DestroyRoutine()
    {
        var renderers = GetComponentsInChildren<Renderer>();
        foreach (var renderer in renderers)
        {
            renderer.enabled = false;
        }
        yield return new WaitForSeconds(DestroyDelay);
        // Destroy this on all clients
        if (isServer)
            NetworkServer.Destroy(gameObject);
    }
}
