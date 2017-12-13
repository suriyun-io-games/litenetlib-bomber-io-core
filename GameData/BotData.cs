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

    public string GetSelectHead()
    {
        var headKeys = new List<string>(GameInstance.Heads.Keys);
        return headDataIndex < 0 || headDataIndex > headKeys.Count ? headKeys[Random.Range(0, headKeys.Count)] : headKeys[headDataIndex];
    }

    public string GetSelectBomb()
    {
        var bombKeys = new List<string>(GameInstance.Bombs.Keys);
        return bombDataIndex < 0 || bombDataIndex > bombKeys.Count ? bombKeys[Random.Range(0, bombKeys.Count)] : bombKeys[bombDataIndex];
    }

    public string GetSelectCharacter()
    {
        var characterKeys = new List<string>(GameInstance.Characters.Keys);
        return characterDataIndex < 0 || characterDataIndex > characterKeys.Count ? characterKeys[Random.Range(0, characterKeys.Count)] : characterKeys[characterDataIndex];
    }
}
