using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class BotEntity : CharacterEntity
{
    public const float ReachedTargetDistance = 0.1f;
    private Queue<Vector3> waypoints = new Queue<Vector3>();
    private Vector3 targetPosition;
    private BombEntity bomb;
    public override void OnStartServer()
    {
        base.OnStartServer();
        ServerSpawn(false);
        targetPosition = TempTransform.position;
        StartCoroutine(UpdateState());
    }

    public override void OnStartLocalPlayer()
    {
        // Do nothing
    }

    protected override void UpdateMovements()
    {
        if (!isServer)
            return;
        
        if (GameNetworkManager.Singleton.numPlayers <= 0)
            return;

        if (isDead)
        {
            ServerRespawn(false);
            targetPosition = TempTransform.position;
            waypoints.Clear();
            return;
        }

        // Gets a vector that points from the player's position to the target's.
        if (!IsReachedTargetPosition())
        {
            var heading = targetPosition - TempTransform.position;
            var distance = heading.magnitude;
            var direction = heading / distance; // This is now the normalized direction.
            Move(direction);
            var targetRotation = Quaternion.LookRotation(heading);
            TempTransform.rotation = Quaternion.Lerp(TempTransform.rotation, targetRotation, Time.deltaTime * 5f);
            BombEntity foundBomb = null;
            if (IsNearBomb(TempTransform.position, direction, out foundBomb))
                bomb = foundBomb;
        }
        else
        {
            var velocity = TempRigidbody.velocity;
            velocity.x = 0;
            velocity.z = 0;
            TempRigidbody.velocity = velocity;
        }
    }

    private IEnumerator UpdateState()
    {
        yield return 0;
        while (true)
        {
            if (GameNetworkManager.Singleton.numPlayers > 0 && !isDead)
            {
                yield return StartCoroutine(FindWaypoints());
                yield return StartCoroutine(WalkToLastPosition());
                // Reached last waypoint position, plant bomb
                if (bomb == null && bombData != null)
                    bomb = bombData.Plant(this, TempTransform.position);
                // If bomb planted, move to avoid it
                if (bomb != null)
                {
                    yield return StartCoroutine(FindWaypoints());
                    yield return StartCoroutine(WalkToLastPosition());
                    yield return StartCoroutine(WaitTillBombExploded());
                }
            }
            yield return 0;
        }
    }

    private IEnumerator FindWaypoints()
    {
        waypoints.Clear();
        var characterPosition = RoundXZ(TempTransform.position);
        var currentPosition = characterPosition;
        var loopCount = 0;
        var exceptDirection = Vector3.zero;
        List<Vector3> availableDirections;
        while (FindDirectionToGo(currentPosition, out availableDirections, new List<Vector3>() { exceptDirection }) && loopCount < 10)
        {
            var currentDirection = availableDirections[Random.Range(0, availableDirections.Count)];
            exceptDirection = currentDirection * -1;
            currentPosition += currentDirection;
            waypoints.Enqueue(RoundXZ(currentPosition));
            var bombDistance = 1 + powerUpBombRange;
            if (bomb == null && waypoints.Count > 4 && 
                (IsNearBrickOrPlayer(currentPosition, Vector3.left, bombDistance) ||
                IsNearBrickOrPlayer(currentPosition, Vector3.right, bombDistance) ||
                IsNearBrickOrPlayer(currentPosition, Vector3.back, bombDistance) ||
                IsNearBrickOrPlayer(currentPosition, Vector3.forward, bombDistance)))
                break;
            if (bomb != null && waypoints.Count > 4 && !CanHitBomb(currentPosition, bomb))
                break;
            ++loopCount;
            yield return 0;
        }
    }

    private IEnumerator WalkToLastPosition()
    {
        if (waypoints.Count > 0)
        {
            // Dequeue first target position
            targetPosition = waypoints.Dequeue();
            // Looping walk to last position in queue
            while (waypoints.Count > 0)
            {
                if (IsReachedTargetPosition())
                    targetPosition = waypoints.Dequeue();
                yield return 0;
            }
            while (!IsReachedTargetPosition())
            {
                yield return 0;
            }
        }
    }

    private IEnumerator WaitTillBombExploded()
    {
        while (bomb != null)
        {
            yield return 0;
        }
    }

    private bool IsReachedTargetPosition()
    {
        return Vector3.Distance(targetPosition, TempTransform.position) < ReachedTargetDistance;
    }

    private bool IsNearWallOrBrickOrBomb(Vector3 position, Vector3 direction, float distance = 1)
    {
        RaycastHit hitInfo;
        if (Physics.Raycast(position + Vector3.up * 0.1f, direction, out hitInfo, distance))
        {
            // If hit powerup or character, then it's mean bot still can walk to that direction
            var brick = hitInfo.transform.GetComponent<BrickEntity>();
            var powerup = hitInfo.transform.GetComponent<PowerUpEntity>();
            var character = hitInfo.transform.GetComponent<CharacterEntity>();
            if (brick != null && brick.isDead)
                return false;
            return (powerup == null && character == null);
        }
        return false;
    }

    private bool IsNearBrickOrPlayer(Vector3 position, Vector3 direction, float distance = 1)
    {
        RaycastHit hitInfo;
        if (Physics.Raycast(position + Vector3.up * 0.1f, direction, out hitInfo, distance))
        {
            // If hit brick or character, then it's mean bot should plant the bomb here
            var brick = hitInfo.transform.GetComponent<BrickEntity>();
            var character = hitInfo.transform.GetComponent<CharacterEntity>();
            return (brick != null && !brick.isDead) || 
                (character != null && character != this && !character.isDead);
        }
        return false;
    }

    private bool IsNearBomb(Vector3 position, Vector3 direction, out BombEntity bomb, float distance = 1)
    {
        bomb = null;
        RaycastHit hitInfo;
        if (Physics.Raycast(position + Vector3.up * 0.1f, direction, out hitInfo, distance))
        {
            // If hit brick or character, then it's mean bot should plant the bomb here
            bomb = hitInfo.transform.GetComponent<BombEntity>();
            return bomb != null;
        }
        return false;
    }

    private bool FindDirectionToGo(Vector3 position, out List<Vector3> avilableDirections, List<Vector3> exceptDirections = null)
    {
        avilableDirections = new List<Vector3>();
        if (exceptDirections == null)
            exceptDirections = new List<Vector3>();
        if (!exceptDirections.Contains(Vector3.left) && !IsNearWallOrBrickOrBomb(position, Vector3.left))
            avilableDirections.Add(Vector3.left);
        if (!exceptDirections.Contains(Vector3.right) && !IsNearWallOrBrickOrBomb(position, Vector3.right))
            avilableDirections.Add(Vector3.right);
        if (!exceptDirections.Contains(Vector3.forward) && !IsNearWallOrBrickOrBomb(position, Vector3.forward))
            avilableDirections.Add(Vector3.forward);
        if (!exceptDirections.Contains(Vector3.back) && !IsNearWallOrBrickOrBomb(position, Vector3.back))
            avilableDirections.Add(Vector3.back);
        if (avilableDirections.Count > 0)
            return true;
        return false;   // Can't move, this should not happend
    }

    private bool CanHitBomb(Vector3 currentPosition, BombEntity bomb)
    {
        if (bomb == null)
            return false;
        var bombPosition = bomb.TempTransform.position;
        if (Mathf.RoundToInt(bombPosition.x) == Mathf.RoundToInt(currentPosition.x) ||
            Mathf.RoundToInt(bombPosition.z) == Mathf.RoundToInt(currentPosition.z))
            return Vector3.Distance(bombPosition, currentPosition) <= 1 + bomb.addBombRange;
        return false;
    }

    private bool ShouldPlantBomb()
    {
        var bombDistance = 1 + PowerUpBombRange;
        var currentPosition = TempTransform.position;
        return (
            (IsNearBrickOrPlayer(currentPosition, Vector3.left, bombDistance) && 
            (!IsNearWallOrBrickOrBomb(currentPosition, Vector3.right) ||
            !IsNearWallOrBrickOrBomb(currentPosition, Vector3.forward) ||
            !IsNearWallOrBrickOrBomb(currentPosition, Vector3.back))) ||
            (IsNearBrickOrPlayer(currentPosition, Vector3.right, bombDistance) && 
            (!IsNearWallOrBrickOrBomb(currentPosition, Vector3.left) ||
            !IsNearWallOrBrickOrBomb(currentPosition, Vector3.forward) ||
            !IsNearWallOrBrickOrBomb(currentPosition, Vector3.back))) ||
            (IsNearBrickOrPlayer(currentPosition, Vector3.forward, bombDistance) && 
            (!IsNearWallOrBrickOrBomb(currentPosition, Vector3.back) ||
            !IsNearWallOrBrickOrBomb(currentPosition, Vector3.left) ||
            !IsNearWallOrBrickOrBomb(currentPosition, Vector3.right))) ||
            (IsNearBrickOrPlayer(currentPosition, Vector3.back, bombDistance) && 
            (!IsNearWallOrBrickOrBomb(currentPosition, Vector3.forward) ||
            !IsNearWallOrBrickOrBomb(currentPosition, Vector3.left) ||
            !IsNearWallOrBrickOrBomb(currentPosition, Vector3.right))));
    }
}
