[System.Serializable]
public struct CharacterStats
{
    public int bombRange;
    public int bombAmount;
    public int heart;
    public int moveSpeed;
    public bool canKickBomb;
    public static CharacterStats operator +(CharacterStats a, CharacterStats b)
    {
        var result = new CharacterStats();
        result.bombRange = a.bombRange + b.bombRange;
        result.bombAmount = a.bombAmount + b.bombAmount;
        result.heart = a.heart + b.heart;
        result.moveSpeed = a.moveSpeed + b.moveSpeed;
        result.canKickBomb = a.canKickBomb || b.canKickBomb;
        return result;
    }

    public static CharacterStats operator -(CharacterStats a, CharacterStats b)
    {
        var result = new CharacterStats();
        result.bombRange = a.bombRange - b.bombRange;
        result.bombAmount = a.bombAmount - b.bombAmount;
        result.heart = a.heart - b.heart;
        result.moveSpeed = a.moveSpeed - b.moveSpeed;
        result.canKickBomb = a.canKickBomb && b.canKickBomb;
        return result;
    }
}
