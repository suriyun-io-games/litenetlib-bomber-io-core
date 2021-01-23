using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using LiteNetLibManager;
using UnityEngine.UI;
using static LiteNetLibManager.LiteNetLibSyncList;

[RequireComponent(typeof(LiteNetLibTransform))]
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
    [SyncField]
    public int watchAdsCount;

    [SyncField(hook = "OnIsDeadChanged")]
    public bool isDead;

    [SyncField(hook = "OnCharacterChanged")]
    public int selectCharacter = 0;

    [SyncField(hook = "OnHeadChanged")]
    public int selectHead = 0;

    [SyncField(hook = "OnBombChanged")]
    public int selectBomb = 0;

    public SyncListInt selectCustomEquipments = new SyncListInt();

    [SyncField]
    public bool isInvincible;

    [SyncField]
    public CharacterStats addStats;

    [SyncField]
    public string extra;

    [HideInInspector]
    public int rank = 0;

    public override bool IsDead
    {
        get { return isDead; }
    }

    public override bool IsBot
    {
        get { return false; }
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
    protected Vector3? previousPosition;
    protected Vector3 currentVelocity;
    protected Vector3 currentMoveDirection;
    protected BombEntity kickingBomb;

    public bool isReady { get; private set; }
    public float deathTime { get; private set; }
    public float invincibleTime { get; private set; }

    private bool isHidding;
    public bool IsHidding
    {
        get { return isHidding; }
        set
        {
            isHidding = value;
            var renderers = GetComponentsInChildren<Renderer>();
            foreach (var renderer in renderers)
                renderer.enabled = !isHidding;
            var canvases = GetComponentsInChildren<Canvas>();
            foreach (var canvas in canvases)
                canvas.enabled = !isHidding;
        }
    }

    public Transform CacheTransform { get; private set; }
    public Rigidbody CacheRigidbody { get; private set; }
    public Collider CacheCollider { get; private set; }
    public LiteNetLibTransform CacheNetTransform { get; private set; }

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
        selectCustomEquipments.onOperation = OnCustomEquipmentsChanged;
        gameObject.layer = GameInstance.Singleton.characterLayer;
        CacheTransform = transform;
        CacheRigidbody = GetComponent<Rigidbody>();
        CacheCollider = GetComponent<Collider>();
        CacheNetTransform = GetComponent<LiteNetLibTransform>();
        CacheNetTransform.ownerClientCanSendTransform = true;
        CacheNetTransform.ownerClientNotInterpolate = true;
        if (damageLaunchTransform == null)
            damageLaunchTransform = CacheTransform;
        if (effectTransform == null)
            effectTransform = CacheTransform;
        if (characterModelTransform == null)
            characterModelTransform = CacheTransform;
        foreach (var localPlayerObject in localPlayerObjects)
        {
            localPlayerObject.SetActive(false);
        }
        deathTime = Time.unscaledTime;
    }

    public override void OnStartClient()
    {
        if (!IsServer)
        {
            OnIsDeadChanged(isDead);
            OnHeadChanged(selectHead);
            OnCharacterChanged(selectCharacter);
            OnBombChanged(selectBomb);
            OnCustomEquipmentsChanged(Operation.Dirty, 0);
        }
    }

    public override void OnStartServer()
    {
        OnIsDeadChanged(isDead);
        OnHeadChanged(selectHead);
        OnCharacterChanged(selectCharacter);
        OnBombChanged(selectBomb);
        OnCustomEquipmentsChanged(Operation.Dirty, 0);
    }

    public override void OnStartOwnerClient()
    {
        base.OnStartOwnerClient();

        var followCam = FindObjectOfType<FollowCamera>();
        followCam.target = CacheTransform;
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
            if (!IsServer && IsOwnerClient && Time.unscaledTime - deathTime >= DISCONNECT_WHEN_NOT_RESPAWN_DURATION)
                GameNetworkManager.Singleton.StopHost();
        }

        if (IsServer && isInvincible && Time.unscaledTime - invincibleTime >= GameplayManager.Singleton.invincibleDuration)
            isInvincible = false;
        if (invincibleEffect != null)
            invincibleEffect.SetActive(isInvincible);
        if (nameText != null)
            nameText.text = playerName;
        UpdateAnimation();
        UpdateInput();
        CacheCollider.enabled = IsServer || IsOwnerClient;
    }

    private void FixedUpdate()
    {
        if (!previousPosition.HasValue)
            previousPosition = CacheTransform.position;
        var currentMove = CacheTransform.position - previousPosition.Value;
        currentVelocity = currentMove / Time.deltaTime;
        previousPosition = CacheTransform.position;

        if (NetworkManager != null && NetworkManager.IsMatchEnded)
            return;

        UpdateMovements();
    }

    private void OnCollisionEnter(Collision collision)
    {
        if (!IsOwnerClient)
            return;

        if (PowerUpCanKickBomb)
            kickingBomb = collision.gameObject.GetComponent<BombEntity>();
    }

    private void OnCollisionStay(Collision collision)
    {
        if (!IsOwnerClient)
            return;

        if (!PowerUpCanKickBomb || kickingBomb == null)
            return;

        if (kickingBomb == collision.gameObject.GetComponent<BombEntity>())
        {
            var moveDirNorm = currentMoveDirection.normalized;
            var heading = kickingBomb.CacheTransform.position - CacheTransform.position;
            var distance = heading.magnitude;
            var direction = heading / distance;

            if ((moveDirNorm.x > 0.5f && direction.x > 0.5f) ||
                (moveDirNorm.z > 0.5f && direction.z > 0.5f) ||
                (moveDirNorm.x < -0.5f && direction.x < -0.5f) ||
                (moveDirNorm.z < -0.5f && direction.z < -0.5f))
            {
                // Kick bomb if direction is opposite
                CmdKick(kickingBomb.ObjectId, (sbyte)moveDirNorm.x, (sbyte)moveDirNorm.z);
            }
        }
    }

    private void OnCollisionExit(Collision collision)
    {
        if (!IsOwnerClient)
            return;

        if (!PowerUpCanKickBomb || kickingBomb == collision.gameObject.GetComponent<BombEntity>())
            kickingBomb = null;
    }

    protected virtual void UpdateInput()
    {
        if (!IsOwnerClient || isDead)
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
                CmdPlantBomb(RoundXZ(CacheTransform.position));
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
            var velocity = currentVelocity;
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
        if (direction.sqrMagnitude > 0)
        {
            if (direction.sqrMagnitude > 1)
                direction = direction.normalized;
            direction.y = 0;

            var targetSpeed = GetMoveSpeed();
            var targetVelocity = direction * targetSpeed;
            var rigidbodyVel = CacheRigidbody.velocity;
            rigidbodyVel.y = 0;
            if (rigidbodyVel.sqrMagnitude < 1)
                CacheTransform.position += targetVelocity * Time.deltaTime;

            var rotateHeading = (CacheTransform.position + direction) - CacheTransform.position;
            var targetRotation = Quaternion.LookRotation(rotateHeading);
            CacheTransform.rotation = Quaternion.Lerp(CacheTransform.rotation, targetRotation, Time.deltaTime * 6f);
        }
    }

    protected virtual void UpdateMovements()
    {
        if (!IsOwnerClient || isDead)
            return;

        currentMoveDirection = new Vector3(inputMove.x, 0, inputMove.y);
        Move(currentMoveDirection);
    }

    public void RemoveBomb(BombEntity bomb)
    {
        if (!IsServer || bombs == null)
            return;
        bombs.Remove(bomb);
    }

    public void ReceiveDamage(CharacterEntity attacker)
    {
        if (!IsServer)
            return;

        if (isDead || isInvincible)
            return;

        var gameplayManager = GameplayManager.Singleton;
        if (!gameplayManager.CanReceiveDamage(this, attacker))
            return;

        if (addStats.heart == 0)
        {
            if (attacker != null)
                attacker.KilledTarget(this);
            deathTime = Time.unscaledTime;
            ++dieCount;
            isDead = true;
            var velocity = CacheRigidbody.velocity;
            CacheRigidbody.velocity = new Vector3(0, velocity.y, 0);
        }

        if (addStats.heart > 0)
        {
            var tempStats = addStats;
            --tempStats.heart;
            addStats = tempStats;
            ServerInvincible();
        }
    }

    public void KilledTarget(CharacterEntity target)
    {
        if (!IsServer)
            return;

        var gameplayManager = GameplayManager.Singleton;
        if (target == this)
            score += gameplayManager.suicideScore;
        else
        {
            score += gameplayManager.killScore;
            foreach (var rewardCurrency in gameplayManager.rewardCurrencies)
            {
                var currencyId = rewardCurrency.currencyId;
                var amount = Random.Range(rewardCurrency.randomAmountMin, rewardCurrency.randomAmountMax);
                TargetRewardCurrency(ConnectionId, currencyId, amount);
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

    protected virtual void OnCustomEquipmentsChanged(Operation op, int itemIndex)
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

    public void ServerInvincible()
    {
        if (!IsServer)
            return;
        invincibleTime = Time.unscaledTime;
        isInvincible = true;
    }

    public void ServerSpawn(bool isWatchedAds)
    {
        if (!IsServer)
            return;
        if (Respawn(isWatchedAds))
        {
            var gameplayManager = GameplayManager.Singleton;
            ServerInvincible();
            OnSpawn();
            var position = gameplayManager.GetCharacterSpawnPosition(this);
            CacheTransform.position = position;
            TargetSpawn(ConnectionId, position);
            isDead = false;
        }
    }

    public void ServerRespawn(bool isWatchedAds)
    {
        if (!IsServer)
            return;
        if (CanRespawn(isWatchedAds))
            ServerSpawn(isWatchedAds);
    }

    public void ResetItemAndStats()
    {
        if (!IsServer)
            return;
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

    public void CmdReady()
    {
        CallNetFunction(_CmdReady, FunctionReceivers.Server);
    }

    [NetFunction]
    protected void _CmdReady()
    {
        if (!isReady)
        {
            ServerSpawn(false);
            isReady = true;
        }
    }

    public void CmdRespawn(bool isWatchedAds)
    {
        CallNetFunction(_CmdRespawn, FunctionReceivers.Server, isWatchedAds);
    }

    [NetFunction]
    protected void _CmdRespawn(bool isWatchedAds)
    {
        ServerRespawn(isWatchedAds);
    }

    public void CmdPlantBomb(Vector3 position)
    {
        CallNetFunction(_CmdPlantBomb, FunctionReceivers.Server, position);
    }

    [NetFunction]
    protected void _CmdPlantBomb(Vector3 position)
    {
        // Avoid hacks
        if (Vector3.Distance(position, CacheTransform.position) > 3)
            position = CacheTransform.position;
        if (bombs.Count >= 1 + PowerUpBombAmount || !BombEntity.CanPlant(position))
            return;
        if (bombData != null)
            bombs.Add(bombData.Plant(this, position));
    }

    public void TargetSpawn(long conn, Vector3 position)
    {
        CallNetFunction(_TargetSpawn, conn, position);
    }

    [NetFunction]
    protected void _TargetSpawn(Vector3 position)
    {
        transform.position = position;
    }

    public void TargetRewardCurrency(long conn, string currencyId, int amount)
    {
        CallNetFunction(_TargetRewardCurrency, conn, currencyId, amount);
    }

    [NetFunction]
    protected void _TargetRewardCurrency(string currencyId, int amount)
    {
        MonetizationManager.Save.AddCurrency(currencyId, amount);
    }

    public void CmdKick(uint bombNetId, sbyte dirX, sbyte dirZ)
    {
        CallNetFunction(_CmdKick, FunctionReceivers.Server, bombNetId, dirX, dirZ);
    }

    [NetFunction]
    protected void _CmdKick(uint bombNetId, sbyte dirX, sbyte dirZ)
    {
        LiteNetLibIdentity identity;
        if (Manager.Assets.TryGetSpawnedObject(bombNetId, out identity))
            identity.GetComponent<BombEntity>().Kick(ObjectId, dirX, dirZ);
    }

    protected Vector3 RoundXZ(Vector3 vector)
    {
        return new Vector3(
            Mathf.RoundToInt(vector.x),
            vector.y,
            Mathf.RoundToInt(vector.z));
    }
}
