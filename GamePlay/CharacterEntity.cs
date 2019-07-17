using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.UI;

[RequireComponent(typeof(Rigidbody))]
public class CharacterEntity : BaseNetworkGameCharacter
{
    public const float DISCONNECT_WHEN_NOT_RESPAWN_DURATION = 60;
    public Transform damageLaunchTransform;
    public Transform effectTransform;
    public Transform characterModelTransform;
    public GameObject[] localPlayerObjects;
    [Header("UI")]
    public Text nameText;
    [Header("Effect")]
    public GameObject invincibleEffect;
    [Header("Online data")]
    [SyncVar]
    public int watchAdsCount;
    
    [SyncVar(hook = "OnIsDeadChanged")]
    public bool isDead;

    [SyncVar(hook = "OnCharacterChanged")]
    public int selectCharacter = 0;

    [SyncVar(hook = "OnHeadChanged")]
    public int selectHead = 0;

    [SyncVar(hook = "OnBombChanged")]
    public int selectBomb = 0;

    public SyncListInt selectCustomEquipments = new SyncListInt();

    [SyncVar]
    public bool isInvincible;

    [SyncVar]
    public CharacterStats addStats;

    [SyncVar]
    public string extra;

    [HideInInspector]
    public int rank = 0;

    public override bool IsDead
    {
        get { return isDead; }
    }

    protected readonly List<BombEntity> bombs = new List<BombEntity>();
    protected Camera targetCamera;
    protected CharacterModel characterModel;
    protected CharacterData characterData;
    protected HeadData headData;
    protected BombData bombData;
    protected Dictionary<int, CustomEquipmentData> customEquipmentDict = new Dictionary<int, CustomEquipmentData>();
    protected bool isMobileInput;
    protected Vector2 inputMove;
    protected Vector3 currentMoveDirection;
    protected BombEntity kickingBomb;

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
        get
        {
            var max = GameplayManager.Singleton.maxBombRangePowerUp;
            if (addStats.bombRange > max)
                return max;
            return addStats.bombRange;
        }
    }

    public int PowerUpBombAmount
    {
        get
        {
            var max = GameplayManager.Singleton.maxBombAmountPowerUp;
            if (addStats.bombAmount > max)
                return max;
            return addStats.bombAmount;
        }
    }

    public int PowerUpHeart
    {
        get
        {
            var max = GameplayManager.Singleton.maxHeartPowerUp;
            if (addStats.heart > max)
                return max;
            return addStats.heart;
        }
    }

    public int PowerUpMoveSpeed
    {
        get
        {
            var max = GameplayManager.Singleton.maxMoveSpeedPowerUp;
            if (addStats.moveSpeed > max)
                return max;
            return addStats.moveSpeed;
        }
    }

    public bool PowerUpCanKickBomb
    {
        get { return addStats.canKickBomb; }
    }

    public int TotalMoveSpeed
    {
        get
        {
            var gameplayManager = GameplayManager.Singleton;
            var total = gameplayManager.minMoveSpeed + (PowerUpMoveSpeed * gameplayManager.addMoveSpeedPerPowerUp);
            return total;
        }
    }

    private void Awake()
    {
        selectCustomEquipments.Callback = OnCustomEquipmentsChanged;
        gameObject.layer = GameInstance.Singleton.characterLayer;
        if (damageLaunchTransform == null)
            damageLaunchTransform = TempTransform;
        if (effectTransform == null)
            effectTransform = TempTransform;
        if (characterModelTransform == null)
            characterModelTransform = TempTransform;
        foreach (var localPlayerObject in localPlayerObjects)
        {
            localPlayerObject.SetActive(false);
        }
        deathTime = Time.unscaledTime;
    }

    public override void OnStartClient()
    {
        if (!isServer)
        {
            OnIsDeadChanged(isDead);
            OnHeadChanged(selectHead);
            OnCharacterChanged(selectCharacter);
            OnBombChanged(selectBomb);
            OnCustomEquipmentsChanged(SyncList<int>.Operation.OP_DIRTY, 0);
        }
    }

    public override void OnStartServer()
    {
        OnIsDeadChanged(isDead);
        OnHeadChanged(selectHead);
        OnCharacterChanged(selectCharacter);
        OnBombChanged(selectBomb);
        OnCustomEquipmentsChanged(SyncList<int>.Operation.OP_DIRTY, 0);
    }

    public override void OnStartLocalPlayer()
    {
        base.OnStartLocalPlayer();

        var followCam = FindObjectOfType<FollowCamera>();
        followCam.target = TempTransform;
        targetCamera = followCam.GetComponent<Camera>();
        var uiGameplay = FindObjectOfType<UIGameplay>();
        if (uiGameplay != null)
            uiGameplay.FadeOut();

        foreach (var localPlayerObject in localPlayerObjects)
        {
            localPlayerObject.SetActive(true);
        }

        CmdReady();
    }

    protected override void Update()
    {
        base.Update();
        if (NetworkManager != null && NetworkManager.IsMatchEnded)
            return;

        if (IsDead)
        {
            if (!isServer && isLocalPlayer && Time.unscaledTime - deathTime >= DISCONNECT_WHEN_NOT_RESPAWN_DURATION)
                GameNetworkManager.Singleton.StopHost();
        }

        if (isServer && isInvincible && Time.unscaledTime - invincibleTime >= GameplayManager.Singleton.invincibleDuration)
            isInvincible = false;
        if (invincibleEffect != null)
            invincibleEffect.SetActive(isInvincible);
        if (nameText != null)
            nameText.text = playerName;
        UpdateAnimation();
        UpdateInput();
        TempCollider.enabled = isServer || isLocalPlayer;
    }

    private void FixedUpdate()
    {
        if (NetworkManager != null && NetworkManager.IsMatchEnded)
            return;

        UpdateMovements();
    }

    private void OnCollisionEnter(Collision collision)
    {
        if (!isLocalPlayer)
            return;

        if (PowerUpCanKickBomb)
            kickingBomb = collision.gameObject.GetComponent<BombEntity>();
    }

    private void OnCollisionStay(Collision collision)
    {
        if (!isLocalPlayer)
            return;

        if (!PowerUpCanKickBomb || kickingBomb == null)
            return;

        if (kickingBomb == collision.gameObject.GetComponent<BombEntity>())
        {
            var moveDirNorm = currentMoveDirection.normalized;
            var heading = kickingBomb.TempTransform.position - TempTransform.position;
            var distance = heading.magnitude;
            var direction = heading / distance;

            if ((moveDirNorm.x > 0.5f && direction.x > 0.5f) ||
                (moveDirNorm.z > 0.5f && direction.z > 0.5f) ||
                (moveDirNorm.x < -0.5f && direction.x < -0.5f) ||
                (moveDirNorm.z < -0.5f && direction.z < -0.5f))
            {
                // Kick bomb if direction is opposite
                kickingBomb.CmdKick(netId, (sbyte)moveDirNorm.x, (sbyte)moveDirNorm.z);
            }
        }
    }

    private void OnCollisionExit(Collision collision)
    {
        if (!isLocalPlayer)
            return;

        if (!PowerUpCanKickBomb || kickingBomb == collision.gameObject.GetComponent<BombEntity>())
            kickingBomb = null;
    }

    protected virtual void UpdateInput()
    {
        if (!isLocalPlayer || isDead)
            return;

        bool canControl = true;
        var fields = FindObjectsOfType<InputField>();
        foreach (var field in fields)
        {
            if (field.isFocused)
            {
                canControl = false;
                break;
            }
        }

        isMobileInput = Application.isMobilePlatform;
#if UNITY_EDITOR
        isMobileInput = GameInstance.Singleton.showJoystickInEditor;
#endif
        InputManager.useMobileInputOnNonMobile = isMobileInput;

        inputMove = Vector2.zero;
        if (canControl)
        {
            inputMove = new Vector2(InputManager.GetAxis("Horizontal", false), InputManager.GetAxis("Vertical", false));
            if (InputManager.GetButtonDown("Fire1"))
                CmdPlantBomb(RoundXZ(TempTransform.position));
        }
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

        currentMoveDirection = new Vector3(inputMove.x, 0, inputMove.y);
        Move(currentMoveDirection);
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

        if (addStats.heart == 0)
        {
            if (attacker != null)
                attacker.KilledTarget(this);
            deathTime = Time.unscaledTime;
            ++dieCount;
            isDead = true;
            var velocity = TempRigidbody.velocity;
            TempRigidbody.velocity = new Vector3(0, velocity.y, 0);
        }

        if (addStats.heart > 0)
        {
            var tempStats = addStats;
            --tempStats.heart;
            addStats = tempStats;
            ServerInvincible();
        }
    }

    [Server]
    public void KilledTarget(CharacterEntity target)
    {
        var gameplayManager = GameplayManager.Singleton;
        if (target == this)
            score += gameplayManager.suicideScore;
        else
        {
            score += gameplayManager.killScore;
            if (connectionToClient != null)
            {
                foreach (var rewardCurrency in gameplayManager.rewardCurrencies)
                {
                    var currencyId = rewardCurrency.currencyId;
                    var amount = Random.Range(rewardCurrency.randomAmountMin, rewardCurrency.randomAmountMax);
                    TargetRewardCurrency(connectionToClient, currencyId, amount);
                }
            }
            ++killCount;
        }
        GameNetworkManager.Singleton.SendKillNotify(playerName, target.playerName, bombData == null ? string.Empty : bombData.GetId());
    }

    private void OnIsDeadChanged(bool value)
    {
        if (!isDead && value)
            deathTime = Time.unscaledTime;
        isDead = value;
    }

    private void OnCharacterChanged(int value)
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
        if (customEquipmentDict != null)
        {
            characterModel.ClearCustomModels();
            foreach (var customEquipmentEntry in customEquipmentDict.Values)
            {
                characterModel.SetCustomModel(customEquipmentEntry.containerIndex, customEquipmentEntry.modelObject);
            }
        }
        characterModel.gameObject.SetActive(true);
    }

    private void OnHeadChanged(int value)
    {
        selectHead = value;
        headData = GameInstance.GetHead(value);
        if (characterModel != null && headData != null)
            characterModel.SetHeadModel(headData.modelObject);
    }

    private void OnBombChanged(int value)
    {
        selectBomb = value;
        bombData = GameInstance.GetBomb(value);
    }

    protected virtual void OnCustomEquipmentsChanged(SyncList<int>.Operation op, int itemIndex)
    {
        if (characterModel != null)
            characterModel.ClearCustomModels();
        customEquipmentDict.Clear();
        for (var i = 0; i < selectCustomEquipments.Count; ++i)
        {
            var customEquipmentData = GameInstance.GetCustomEquipment(selectCustomEquipments[i]);
            if (customEquipmentData != null &&
                !customEquipmentDict.ContainsKey(customEquipmentData.containerIndex))
            {
                customEquipmentDict[customEquipmentData.containerIndex] = customEquipmentData;
                if (characterModel != null)
                    characterModel.SetCustomModel(customEquipmentData.containerIndex, customEquipmentData.modelObject);
            }
        }
    }

    public virtual void OnSpawn() { }

    [Server]
    public void ServerInvincible()
    {
        invincibleTime = Time.unscaledTime;
        isInvincible = true;
    }

    [Server]
    public void ServerSpawn(bool isWatchedAds)
    {
        if (Respawn(isWatchedAds))
        {
            var gameplayManager = GameplayManager.Singleton;
            ServerInvincible();
            OnSpawn();
            var position = gameplayManager.GetCharacterSpawnPosition(this);
            TempTransform.position = position;
            if (connectionToClient != null)
                TargetSpawn(connectionToClient, position);
            isDead = false;
        }
    }

    [Server]
    public void ServerRespawn(bool isWatchedAds)
    {
        if (CanRespawn(isWatchedAds))
            ServerSpawn(isWatchedAds);
    }

    [Server]
    public void Reset()
    {
        isDead = false;
        var stats = new CharacterStats();
        if (headData != null)
            stats += headData.stats;
        if (characterData != null)
            stats += characterData.stats;
        if (bombData != null)
            stats += bombData.stats;
        if (customEquipmentDict != null)
        {
            foreach (var value in customEquipmentDict.Values)
                stats += value.stats;
        }
        addStats = stats;
        bombs.Clear();
    }

    [Command]
    public void CmdReady()
    {
        if (!isReady)
        {
            ServerSpawn(false);
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

    [TargetRpc]
    private void TargetRewardCurrency(NetworkConnection conn, string currencyId, int amount)
    {
        MonetizationManager.Save.AddCurrency(currencyId, amount);
    }

    protected Vector3 RoundXZ(Vector3 vector)
    {
        return new Vector3(
            Mathf.RoundToInt(vector.x),
            vector.y,
            Mathf.RoundToInt(vector.z));
    }
}
