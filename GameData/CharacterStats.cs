using LiteNetLib.Utils;

[System.Serializable]
public struct CharacterStats : INetSerializable
{
    public int bombRange;
    public int bombAmount;
    public int heart;
    public int moveSpeed;
    public bool canKickBomb;

    public void Deserialize(NetDataReader reader)
    {
        bombRange = reader.GetInt();
        bombAmount = reader.GetInt();
        heart = reader.GetInt();
        moveSpeed = reader.GetInt();
        canKickBomb = reader.GetBool();
    }

    public void Serialize(NetDataWriter writer)
    {
        writer.Put(bombRange);
        writer.Put(bombAmount);
        writer.Put(heart);
        writer.Put(moveSpeed);
        writer.Put(canKickBomb);
    }

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
