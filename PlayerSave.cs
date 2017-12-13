﻿using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class PlayerSave
{
    public const string KeyPlayerName = "SavePlayerName";
    public const string KeyCharacter = "SaveSelectCharacter";
    public const string KeyHead = "SaveSelectHead";
    public const string KeyBomb = "SaveSelectBomb";

    public static string GetPlayerName()
    {
        if (!PlayerPrefs.HasKey(KeyPlayerName))
            SetPlayerName("Guest-" + string.Format("{0:0000}", Random.Range(1, 9999)));
        return PlayerPrefs.GetString(KeyPlayerName);
    }

    public static void SetPlayerName(string value)
    {
        PlayerPrefs.SetString(KeyPlayerName, value);
        PlayerPrefs.Save();
    }

    public static int GetCharacter()
    {
        return PlayerPrefs.GetInt(KeyCharacter, 0);
    }

    public static void SetCharacter(int value)
    {
        PlayerPrefs.SetInt(KeyCharacter, value);
        PlayerPrefs.Save();
    }

    public static int GetHead()
    {
        return PlayerPrefs.GetInt(KeyHead, 0);
    }

    public static void SetHead(int value)
    {
        PlayerPrefs.SetInt(KeyHead, value);
        PlayerPrefs.Save();
    }

    public static int GetBomb()
    {
        return PlayerPrefs.GetInt(KeyBomb, 0);
    }

    public static void SetBomb(int value)
    {
        PlayerPrefs.SetInt(KeyBomb, value);
        PlayerPrefs.Save();
    }
}
