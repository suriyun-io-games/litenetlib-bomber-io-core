using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using LiteNetLibManager;

public class PowerUpEntity : LiteNetLibBehaviour
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
        gameObject.layer = Physics.IgnoreRaycastLayer;
        var collider = GetComponent<Collider>();
        collider.isTrigger = true;
    }

    private void OnTriggerEnter(Collider other)
    {
        if (isDead)
            return;

        if (other.gameObject.layer == Physics.IgnoreRaycastLayer)
            return;

        var character = other.GetComponent<CharacterEntity>();
        if (character != null && !character.isDead)
        {
            isDead = true;
            EffectEntity.PlayEffect(powerUpEffect, character.effectTransform);
            if (IsServer)
                character.addStats += stats;
            if (character.IsOwnerClient)
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
        if (IsServer)
            NetworkDestroy();
    }
}
