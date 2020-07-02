using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using LiteNetLibManager;

public class BombEntity : LiteNetLibBehaviour
{
    public const float DurationBeforeDestroy = 2f;
    [SyncField]
    public int addBombRange;
    [SyncField]
    public uint planterNetId;
    public AudioClip explosionSound;
    public EffectEntity explosionEffect;
    public float radius = 0.4f;
    public float explosionForceRadius = 0f;
    public float explosionForce = 0f;
    public float lifeTime = 2f;
    public float kickMoveSpeed = 5f;
    public bool canExplodeThroughBricks;

    public bool Exploded { get; protected set; }
    private uint _kickerNetId;
    private sbyte _dirX;
    private sbyte _dirZ;
    private List<CharacterEntity> ignoredCharacters;
    private CharacterEntity planter;
    public CharacterEntity Planter
    {
        get
        {
            if (planter == null)
            {
                LiteNetLibIdentity identity;
                if (Manager.Assets.TryGetSpawnedObject(planterNetId, out identity))
                    planter = identity.GetComponent<CharacterEntity>();
            }
            return planter;
        }
    }
    public Transform CacheTransform { get; private set; }
    public Rigidbody CacheRigidbody { get; private set; }
    public Collider CacheCollider { get; private set; }

    private void Awake()
    {
        gameObject.layer = GameInstance.Singleton.bombLayer;
        CacheTransform = GetComponent<Transform>();
        CacheRigidbody = GetComponent<Rigidbody>();
        CacheCollider = GetComponent<Collider>();
        StartCoroutine(Exploding());
        CacheCollider.isTrigger = true;
    }

    private void Start()
    {
        var collideObjects = Physics.OverlapSphere(CacheTransform.position, 0.4f);
        ignoredCharacters = new List<CharacterEntity>();
        foreach (var collideObject in collideObjects)
        {
            var character = collideObject.GetComponent<CharacterEntity>();
            if (character != null)
            {
                Physics.IgnoreCollision(character.CacheCollider, CacheCollider, true);
                ignoredCharacters.Add(character);
            }
        }
        CacheCollider.isTrigger = false;
    }

    private void FixedUpdate()
    {
        if (Exploded)
            return;

        var collideObjects = Physics.OverlapSphere(CacheTransform.position, 0.4f);
        var newIgnoreList = new List<CharacterEntity>();
        foreach (var collideObject in collideObjects)
        {
            var character = collideObject.GetComponent<CharacterEntity>();
            if (character != null && ignoredCharacters.Contains(character))
                newIgnoreList.Add(character);
        }
        foreach (var ignoredCharacter in ignoredCharacters)
        {
            if (ignoredCharacter != null && !newIgnoreList.Contains(ignoredCharacter))
                Physics.IgnoreCollision(ignoredCharacter.CacheCollider, CacheCollider, false);
        }
        ignoredCharacters = newIgnoreList;

        UpdateMovement();
    }

    private void UpdateMovement()
    {
        if (!IsServer || CacheRigidbody == null)
            return;

        if (Mathf.Abs(_dirX) > 0 || Mathf.Abs(_dirZ) > 0)
        {
            CacheRigidbody.isKinematic = false;
            Vector3 targetVelocity = new Vector3(_dirX, 0, _dirZ) * kickMoveSpeed;
            // Apply a force that attempts to reach our target velocity
            Vector3 velocity = CacheRigidbody.velocity;
            Vector3 velocityChange = (targetVelocity - velocity);
            velocityChange.x = Mathf.Clamp(velocityChange.x, -kickMoveSpeed, kickMoveSpeed);
            velocityChange.y = 0;
            velocityChange.z = Mathf.Clamp(velocityChange.z, -kickMoveSpeed, kickMoveSpeed);
            CacheRigidbody.AddForce(velocityChange, ForceMode.VelocityChange);
        }
        else
        {
            CacheRigidbody.isKinematic = true;
        }
    }

    private void OnCollisionStay(Collision collision)
    {
        var characterEntity = collision.gameObject.GetComponent<CharacterEntity>();
        if (characterEntity != null)
        {
            if (characterEntity.ObjectId == _kickerNetId)
                return;
            _dirX = 0;
            _dirZ = 0;
            return;
        }
        var bombEntity = collision.gameObject.GetComponent<BombEntity>();
        if (bombEntity != null)
        {
            _dirX = 0;
            _dirZ = 0;
            return;
        }
    }

    private void OnDrawGizmos()
    {
        DrawBombGizmos();
    }

    private IEnumerator Exploding()
    {
        yield return new WaitForSeconds(lifeTime);
        Explode();
    }

    private IEnumerator Destroying()
    {
        CacheCollider.enabled = false;
        var renderers = GetComponentsInChildren<Renderer>();
        foreach (var renderer in renderers)
            renderer.enabled = false;
        yield return new WaitForSeconds(DurationBeforeDestroy);
        NetworkDestroy();
    }

    private void Explode()
    {
        // This flag, use to avoid unlimit loops, that can occurs when 2 bombs explode
        if (Exploded || !IsServer)
            return;

        Exploded = true;
        List<Vector3> playingEffectPositions = new List<Vector3>();
        // Create explosion at bomb
        var position = CacheTransform.position;
        bool isPlayingEffect;
        bool hitBrickOrWall;
        CreateExplosion(position, out isPlayingEffect, out hitBrickOrWall);
        playingEffectPositions.Add(position);
        // Create explosion around bomb
        CreateExplosions(Vector3.forward, playingEffectPositions);
        CreateExplosions(Vector3.right, playingEffectPositions);
        CreateExplosions(Vector3.back, playingEffectPositions);
        CreateExplosions(Vector3.left, playingEffectPositions);

        if (Planter != null)
            Planter.RemoveBomb(this);

        RpcExplode(playingEffectPositions.ToArray());
        StartCoroutine(Destroying());
    }

    private void DrawBombGizmos()
    {
        var center = CacheTransform.position;
        var size = Vector3.one;
        Gizmos.DrawWireCube(center, size);
        Gizmos.DrawWireCube(center + Vector3.forward, size);
        Gizmos.DrawWireCube(center + Vector3.right, size);
        Gizmos.DrawWireCube(center + Vector3.back, size);
        Gizmos.DrawWireCube(center + Vector3.left, size);
    }

    private void CreateExplosion(Vector3 position, out bool isPlayingEffect, out bool hitBrickOrWall)
    {
        // Find colliding objects, add up position relates to radius
        // Radius should not be fit to the gaps between bomb (1), so I set it to 0.4 (*2 = 0.8 = not fit to the gaps)
        var collidedObjects = Physics.OverlapSphere(position + Vector3.up * 0.1f, radius);
        // hit wall if it's hitting something
        var collideWalls = collidedObjects.Length > 0;
        var collideBrick = false;
        CharacterEntity characterEntity;
        PowerUpEntity powerUpEntity;
        BombEntity bombEntity;
        BrickEntity brickEntity;
        foreach (var collidedObject in collidedObjects)
        {
            characterEntity = collidedObject.GetComponent<CharacterEntity>();
            powerUpEntity = collidedObject.GetComponent<PowerUpEntity>();
            bombEntity = collidedObject.GetComponent<BombEntity>();
            brickEntity = collidedObject.GetComponent<BrickEntity>();
            // If hit character or power up or brick, determine that it does not hit wall
            if (characterEntity != null ||
                brickEntity != null ||
                powerUpEntity != null ||
                bombEntity != null)
                collideWalls = false;
            if (!canExplodeThroughBricks)
            {
                if (brickEntity != null && !brickEntity.isDead && !collideBrick)
                    collideBrick = true;
            }
            // Next logics will work only on server only so skip it on client
            if (characterEntity != null)
            {
                if (IsServer)
                    characterEntity.ReceiveDamage(Planter);
                characterEntity.CacheRigidbody.AddExplosionForce(explosionForce, CacheTransform.position, explosionForceRadius);
            }
            // Take damage to the brick
            if (brickEntity != null)
            {
                if (IsServer)
                    brickEntity.ReceiveDamage();
            }
            // Destroy powerup
            if (powerUpEntity != null)
            {
                if (IsServer)
                    powerUpEntity.NetworkDestroy();
            }
            // Make chains explode
            if (bombEntity != null && bombEntity != this && !bombEntity.Exploded)
            {
                if (IsServer)
                    bombEntity.Explode();
            }
        }
        isPlayingEffect = !collideWalls;
        hitBrickOrWall = collideWalls || collideBrick;
    }

    private void CreateExplosions(Vector3 direction, List<Vector3> appendingEffectPositions)
    {
        for (int i = 1; i <= 1 + addBombRange; i++)
        {
            var position = CacheTransform.position + (direction * i);
            bool isPlayingEffect;
            bool hitBrickOrWall;
            CreateExplosion(position, out isPlayingEffect, out hitBrickOrWall);
            if (isPlayingEffect)
                appendingEffectPositions.Add(position);
            if (hitBrickOrWall)
                return;
        }
    }

    public static bool CanPlant(Vector3 position)
    {
        position = new Vector3(Mathf.RoundToInt(position.x), 0, Mathf.RoundToInt(position.z));
        var collidedObjects = Physics.OverlapSphere(position + Vector3.up * 0.1f, 0.4f);
        foreach (var collidedObject in collidedObjects)
        {
            if (collidedObject.GetComponent<BombEntity>() != null)
                return false;
        }
        return true;
    }

    public void RpcExplode(Vector3[] positions)
    {
        CallNetFunction(_RpcExplode, FunctionReceivers.All, positions);
    }

    [NetFunction]
    protected void _RpcExplode(Vector3[] positions)
    {
        if (explosionSound != null && AudioManager.Singleton != null)
            AudioSource.PlayClipAtPoint(explosionSound, transform.position, AudioManager.Singleton.sfxVolumeSetting.Level);

        foreach (var position in positions)
        {
            EffectEntity.PlayEffect(explosionEffect, position, Quaternion.identity);
        }

        if (!IsServer)
        {
            CacheCollider.isTrigger = true;
            var renderers = GetComponentsInChildren<Renderer>();
            foreach (var renderer in renderers)
                renderer.enabled = false;
        }
    }

    public void Kick(uint kicker, sbyte dirX, sbyte dirZ)
    {
        _kickerNetId = kicker;
        _dirX = dirX;
        _dirZ = dirZ;
    }
}
