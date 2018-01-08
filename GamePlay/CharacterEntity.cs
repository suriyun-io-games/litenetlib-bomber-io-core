using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.UI;

[RequireComponent(typeof(Rigidbody))]
public class CharacterEntity : NetworkBehaviour, IComparable<CharacterEntity>
{
    public static CharacterEntity Local { get; private set; }
    public Transform damageLaunchTransform;
    public Transform effectTransform;
    public Transform characterModelTransform;
    [Header("UI")]
    public Text nameText;
    [Header("Effect")]
    public GameObject invincibleEffect;
    [Header("Online data")]
    [SyncVar]
    public string playerName;

    [SyncVar]
    public int score;
    public int Score
    {
        get { return score; }
        set
        {
            if (!isServer)
                return;

            score = value;
            GameplayManager.Singleton.UpdateRank(netId);
        }
    }

    [SyncVar]
    public int killCount;

    [SyncVar]
    public int watchAdsCount;

    [SyncVar]
    public int powerUpBombRange;

    [SyncVar]
    public int powerUpBombAmount;

    [SyncVar]
    public int powerUpHeart;

    [SyncVar]
    public int powerUpMoveSpeed;

    [SyncVar(hook = "OnIsDeadChanged")]
    public bool isDead;

    [SyncVar(hook = "OnCharacterChanged")]
    public string selectCharacter = "";

    [SyncVar(hook = "OnHeadChanged")]
    public string selectHead = "";

    [SyncVar(hook = "OnBombChanged")]
    public string selectBomb = "";

    [SyncVar]
    public bool isInvincible;

    protected readonly List<BombEntity> bombs = new List<BombEntity>();
    protected Camera targetCamera;
    protected CharacterModel characterModel;
    protected CharacterData characterData;
    protected HeadData headData;
    protected BombData bombData;

    public bool isReady { get; private set; }
    public float deathTime { get; private set; }
    public float invincibleTime { get; private set; }

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
    private Rigidbody tempRigidbody;
    public Rigidbody TempRigidbody
    {
        get
        {
            if (tempRigidbody == null)
                tempRigidbody = GetComponent<Rigidbody>();
            return tempRigidbody;
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

    public int PowerUpBombRange
    {
        get { return powerUpBombRange; }
        set
        {
            if (!isServer)
                return;
            powerUpBombRange = value;
            var max = GameplayManager.Singleton.maxBombRangePowerUp;
            if (powerUpBombRange > max)
                powerUpBombRange = max;
        }
    }

    public int PowerUpBombAmount
    {
        get { return powerUpBombAmount; }
        set
        {
            if (!isServer)
                return;
            powerUpBombAmount = value;
            var max = GameplayManager.Singleton.maxBombAmountPowerUp;
            if (powerUpBombAmount > max)
                powerUpBombAmount = max;
        }
    }

    public int PowerUpHeart
    {
        get { return powerUpHeart; }
        set
        {
            if (!isServer)
                return;
            powerUpHeart = value;
            var max = GameplayManager.Singleton.maxHeartPowerUp;
            if (powerUpHeart > max)
                powerUpHeart = max;
        }
    }

    public int PowerUpMoveSpeed
    {
        get { return powerUpMoveSpeed; }
        set
        {
            if (!isServer)
                return;
            powerUpMoveSpeed = value;
            var max = GameplayManager.Singleton.maxMoveSpeedPowerUp;
            if (powerUpMoveSpeed > max)
                powerUpMoveSpeed = max;
        }
    }

    public int TotalMoveSpeed
    {
        get
        {
            var gameplayManager = GameplayManager.Singleton;
            var total = gameplayManager.minMoveSpeed + (powerUpMoveSpeed * gameplayManager.addMoveSpeedPerPowerUp);
            return total;
        }
    }

    private void Awake()
    {
        gameObject.layer = GameInstance.Singleton.characterLayer;
        if (damageLaunchTransform == null)
            damageLaunchTransform = TempTransform;
        if (effectTransform == null)
            effectTransform = TempTransform;
        if (characterModelTransform == null)
            characterModelTransform = TempTransform;
    }

    public override void OnStartClient()
    {
        if (!isServer)
        {
            OnIsDeadChanged(isDead);
            OnHeadChanged(selectHead);
            OnCharacterChanged(selectCharacter);
            OnBombChanged(selectBomb);
        }
    }

    public override void OnStartServer()
    {
        OnIsDeadChanged(isDead);
        OnHeadChanged(selectHead);
        OnCharacterChanged(selectCharacter);
        OnBombChanged(selectBomb);
    }

    public override void OnStartLocalPlayer()
    {
        if (Local != null)
            return;

        Local = this;
        var followCam = FindObjectOfType<FollowCamera>();
        followCam.target = TempTransform;
        targetCamera = followCam.GetComponent<Camera>();
        GameplayManager.Singleton.uiGameplay.FadeOut();

        CmdReady();
    }

    private void Update()
    {
        if (isServer && isInvincible && Time.unscaledTime - invincibleTime >= GameplayManager.Singleton.invincibleDuration)
            isInvincible = false;
        if (invincibleEffect != null)
            invincibleEffect.SetActive(isInvincible);
        if (nameText != null)
            nameText.text = playerName;
        UpdateAnimation();
        TempCollider.enabled = isServer || isLocalPlayer;
        if (isLocalPlayer && !isDead)
        {
            if (InputManager.GetButtonDown("Fire1"))
                CmdPlantBomb(RoundXZ(TempTransform.position));
        }
    }

    private void FixedUpdate()
    {
        UpdateMovements();
    }

    protected virtual void UpdateAnimation()
    {
        if (characterModel == null)
            return;
        var animator = characterModel.TempAnimator;
        if (animator == null)
            return;
        if (isDead)
        {
            animator.SetBool("IsDead", true);
            animator.SetFloat("JumpSpeed", 0);
            animator.SetFloat("MoveSpeed", 0);
            animator.SetBool("IsGround", true);
        }
        else
        {
            var velocity = TempRigidbody.velocity;
            var xzMagnitude = new Vector3(velocity.x, 0, velocity.z).magnitude;
            var ySpeed = velocity.y;
            animator.SetBool("IsDead", false);
            animator.SetFloat("JumpSpeed", ySpeed);
            animator.SetFloat("MoveSpeed", xzMagnitude);
            animator.SetBool("IsGround", Mathf.Abs(ySpeed) < 0.5f);
        }
    }

    protected virtual float GetMoveSpeed()
    {
        return TotalMoveSpeed * GameplayManager.REAL_MOVE_SPEED_RATE;
    }

    protected virtual void Move(Vector3 direction)
    {
        if (direction.magnitude != 0)
        {
            if (direction.magnitude > 1)
                direction = direction.normalized;

            var targetSpeed = GetMoveSpeed();
            var targetVelocity = direction * targetSpeed;

            // Apply a force that attempts to reach our target velocity
            Vector3 velocity = TempRigidbody.velocity;
            Vector3 velocityChange = (targetVelocity - velocity);
            velocityChange.x = Mathf.Clamp(velocityChange.x, -targetSpeed, targetSpeed);
            velocityChange.y = 0;
            velocityChange.z = Mathf.Clamp(velocityChange.z, -targetSpeed, targetSpeed);
            TempRigidbody.AddForce(velocityChange, ForceMode.VelocityChange);
            
            var rotateHeading = (TempTransform.position + direction) - TempTransform.position;
            var targetRotation = Quaternion.LookRotation(rotateHeading);
            TempTransform.rotation = Quaternion.Lerp(TempTransform.rotation, targetRotation, Time.deltaTime * 6f);
        }
    }

    protected virtual void UpdateMovements()
    {
        if (!isLocalPlayer || isDead)
            return;

        var direction = new Vector3(InputManager.GetAxis("Horizontal", false), 0, InputManager.GetAxis("Vertical", false));
        Move(direction);
    }

    public void RemoveBomb(BombEntity bomb)
    {
        if (!isServer || bombs == null)
            return;
        bombs.Remove(bomb);
    }

    [Server]
    public void ReceiveDamage(CharacterEntity attacker)
    {
        if (isDead || isInvincible)
            return;

        if (powerUpHeart == 0)
        {
            if (attacker != null)
                attacker.KilledTarget(this);
            isDead = true;
            deathTime = Time.unscaledTime;
        }

        if (powerUpHeart > 0)
        {
            --powerUpHeart;
            ServerInvincible();
        }
    }

    [Server]
    public void KilledTarget(CharacterEntity target)
    {
        var gameplayManager = GameplayManager.Singleton;
        if (target == this)
            Score += gameplayManager.suicideScore;
        else
            Score += gameplayManager.killScore;
        ++killCount;
    }

    private void OnIsDeadChanged(bool value)
    {
        if (!isDead && value)
            deathTime = Time.unscaledTime;
        isDead = value;
    }

    private void OnCharacterChanged(string value)
    {
        selectCharacter = value;
        if (characterModel != null)
            Destroy(characterModel.gameObject);
        characterData = GameInstance.GetCharacter(value);
        if (characterData == null || characterData.modelObject == null)
            return;
        characterModel = Instantiate(characterData.modelObject, characterModelTransform);
        characterModel.transform.localPosition = Vector3.zero;
        characterModel.transform.localEulerAngles = Vector3.zero;
        characterModel.transform.localScale = Vector3.one;
        if (headData != null)
            characterModel.SetHeadModel(headData.modelObject);
        characterModel.gameObject.SetActive(true);
    }

    private void OnHeadChanged(string value)
    {
        selectHead = value;
        headData = GameInstance.GetHead(value);
        if (characterModel != null && headData != null)
            characterModel.SetHeadModel(headData.modelObject);
    }

    private void OnBombChanged(string value)
    {
        selectBomb = value;
        bombData = GameInstance.GetBomb(value);
    }

    [Server]
    public void ServerInvincible()
    {
        invincibleTime = Time.unscaledTime;
        isInvincible = true;
    }

    [Server]
    public void ServerSpawn(bool isWatchedAds)
    {
        var gameplayManager = GameplayManager.Singleton;
        if (!isWatchedAds || watchAdsCount >= gameplayManager.watchAdsRespawnAvailable)
            Reset();
        else
        {
            ++watchAdsCount;
            isDead = false;
        }
        ServerInvincible();

        var position = gameplayManager.GetCharacterSpawnPosition();
        TempTransform.position = position;
        if (connectionToClient != null)
            TargetSpawn(connectionToClient, position);
    }

    [Server]
    public void ServerRespawn(bool isWatchedAds)
    {
        var gameplayManager = GameplayManager.Singleton;
        if (Time.unscaledTime - deathTime >= gameplayManager.respawnDuration)
            ServerSpawn(isWatchedAds);
    }

    [Server]
    public void Reset()
    {
        isDead = false;
        PowerUpBombRange = 0;
        PowerUpBombAmount = 0;
        PowerUpHeart = 0;
        PowerUpMoveSpeed = 0;
        if (characterData != null)
        {
            PowerUpBombRange += characterData.stats.bombRange;
            PowerUpBombAmount += characterData.stats.bombAmount;
            PowerUpHeart += characterData.stats.heart;
            PowerUpMoveSpeed += characterData.stats.moveSpeed;
        }
        if (headData != null)
        {
            PowerUpBombRange += headData.stats.bombRange;
            PowerUpBombAmount += headData.stats.bombAmount;
            PowerUpHeart += headData.stats.heart;
            PowerUpMoveSpeed += headData.stats.moveSpeed;
        }
        if (bombData != null)
        {
            PowerUpBombRange += bombData.stats.bombRange;
            PowerUpBombAmount += bombData.stats.bombAmount;
            PowerUpHeart += bombData.stats.heart;
            PowerUpMoveSpeed += bombData.stats.moveSpeed;
        }
        bombs.Clear();
    }

    [Command]
    public void CmdReady()
    {
        if (!isReady)
        {
            ServerSpawn(false);
            GameplayManager.Singleton.UpdateRank(netId);
            isReady = true;
        }
    }

    [Command]
    public void CmdRespawn(bool isWatchedAds)
    {
        ServerRespawn(isWatchedAds);
    }

    [Command]
    public void CmdPlantBomb(Vector3 position)
    {
        // Avoid hacks
        if (Vector3.Distance(position, TempTransform.position) > 3)
            position = TempTransform.position;
        if (bombs.Count >= 1 + PowerUpBombAmount || !BombEntity.CanPlant(position))
            return;
        if (bombData != null)
            bombs.Add(bombData.Plant(this, position));
    }

    [TargetRpc]
    private void TargetSpawn(NetworkConnection conn, Vector3 position)
    {
        transform.position = position;
    }

    public int CompareTo(CharacterEntity other)
    {
        return ((-1 * Score.CompareTo(other.Score)) * 10) + netId.Value.CompareTo(other.netId.Value);
    }

    protected Vector3 RoundXZ(Vector3 vector)
    {
        return new Vector3(
            Mathf.RoundToInt(vector.x),
            vector.y,
            Mathf.RoundToInt(vector.z));
    }
}
