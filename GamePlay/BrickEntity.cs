using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using LiteNetLibManager;

[RequireComponent(typeof(Collider))]
public class BrickEntity : LiteNetLibBehaviour
{
    [Tooltip("Use this delay to play dead animation")]
    public float disableRenderersDelay;
    public Animator animator;
    [SyncField]
    public bool isDead;
    /// <summary>
    /// Use this flag to set brick renderer disabled, so when player's character come closer when is dead player won't see the brick
    /// </summary>
    [SyncField(onChangeMethodName = nameof(OnIsRendererDisabledChanged))]
    public bool isRendererDisabled;
    public float deathTime { get; private set; }
    
    private Transform tempTransform;
    public Transform TempTransform
    {
        get
        {
            if (tempTransform == null)
                tempTransform = GetComponent<Transform>();
            return tempTransform;
        }
    }
    private Collider tempCollider;
    public Collider TempCollider
    {
        get
        {
            if (tempCollider == null)
                tempCollider = GetComponent<Collider>();
            return tempCollider;
        }
    }

    private void Awake()
    {
        gameObject.layer = GameInstance.Singleton.brickLayer;
    }

    public override void OnStartClient()
    {
        if (!IsServer)
            OnIsRendererDisabledChanged(isRendererDisabled);
    }

    public override void OnStartServer()
    {
        OnIsRendererDisabledChanged(isRendererDisabled);
    }

    private void OnIsRendererDisabledChanged(bool value)
    {
        isRendererDisabled = value;
        SetEnabledAllRenderer(!value);
    }

    private void Update()
    {
        TempCollider.enabled = !isDead;

        if (!IsServer || !isDead)
            return;

        // Respawning.
        var gameplayManager = GameplayManager.Singleton;
        if (Time.unscaledTime - deathTime >= gameplayManager.brickRespawnDuration && !IsNearPlayerOrBomb())
        {
            KillNearlyPowerup();
            isDead = false;
            if (animator != null)
                animator.SetBool("IsDead", isDead);
            RpcIsDeadChanged(isDead);
            isRendererDisabled = isDead;
        }
    }

    public void ReceiveDamage()
    {
        if (!IsServer || isDead)
            return;
        deathTime = Time.unscaledTime;
        isDead = true;
        if (animator != null)
            animator.SetBool("IsDead", isDead);
        StartCoroutine(PlayDeadAnimation());
        RpcIsDeadChanged(isDead);
        // Spawn powerup when it dead.
        GameplayManager.Singleton.SpawnPowerUp(TempTransform.position);
    }

    private void SetEnabledAllRenderer(bool isEnable)
    {
        // GetComponentsInChildren will include this transform so it will be fine without GetComponents calls
        var renderers = GetComponentsInChildren<Renderer>();
        foreach (var renderer in renderers)
            renderer.enabled = isEnable;
    }

    private IEnumerator PlayDeadAnimation()
    {
        yield return new WaitForSeconds(disableRenderersDelay);
        if (IsServer)
        {
            isRendererDisabled = true;
            SetEnabledAllRenderer(!isRendererDisabled);
        }
    }

    private bool IsNearPlayerOrBomb()
    {
        var currentPosition = TempTransform.position;
        var colliders = Physics.OverlapSphere(currentPosition, 5);
        foreach (var collider in colliders)
        {
            if (collider.GetComponent<CharacterEntity>() != null || collider.GetComponent<BombEntity>() != null)
                return true;
        }
        return false;
    }

    private void KillNearlyPowerup()
    {
        var currentPosition = TempTransform.position;
        var colliders = Physics.OverlapSphere(currentPosition, 0.4f);
        foreach (var collider in colliders)
        {
            var powerUp = collider.GetComponent<PowerUpEntity>();
            if (powerUp != null)
                powerUp.NetworkDestroy();
        }
    }

    public void RpcIsDeadChanged(bool isDead)
    {
        CallNetFunction(_RpcIsDeadChanged, FunctionReceivers.All, isDead);
    }

    [NetFunction]
    private void _RpcIsDeadChanged(bool isDead)
    {
        if (IsServer)
            return;

        if (!isDead)
        {
            if (animator != null)
                animator.SetBool("IsDead", isDead);
            SetEnabledAllRenderer(true);
        }
        else
        {
            if (animator != null)
                animator.SetBool("IsDead", isDead);
            StartCoroutine(PlayDeadAnimation());
        }
    }
}
