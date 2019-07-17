using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[System.Serializable]
public class BotData
{
    public string name;
    public int headDataIndex;
    public int bombDataIndex;
    public int characterDataIndex;

    public int GetSelectHead()
    {
        var headKeys = new List<int>(GameInstance.Heads.Keys);
        return headDataIndex < 0 || headDataIndex > headKeys.Count ? headKeys[Random.Range(0, headKeys.Count)] : headKeys[headDataIndex];
    }

    public int GetSelectBomb()
    {
        var bombKeys = new List<int>(GameInstance.Bombs.Keys);
        return bombDataIndex < 0 || bombDataIndex > bombKeys.Count ? bombKeys[Random.Range(0, bombKeys.Count)] : bombKeys[bombDataIndex];
    }

    public int GetSelectCharacter()
    {
        var characterKeys = new List<int>(GameInstance.Characters.Keys);
        return characterDataIndex < 0 || characterDataIndex > characterKeys.Count ? characterKeys[Random.Range(0, characterKeys.Count)] : characterKeys[characterDataIndex];
    }
}
