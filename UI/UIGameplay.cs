using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using LiteNetLibManager;

public class UIGameplay : MonoBehaviour
{
    public Text textPowerUpBombRange;
    public Text textPowerUpBombAmount;
    public Text textPowerUpHeart;
    public Text textPowerUpMoveSpeed;
    public Text textRespawnCountDown;
    public Text textWatchedAdsCount;
    public Text textMatchCountDown;
    public UIBlackFade blackFade;
    public GameObject respawnUiContainer;
    public GameObject respawnButtonContainer;
    public UINetworkGameScores[] uiGameScores;
    public UIKillNotifies uiKillNotifies;
    public GameObject matchEndUi;
    public GameObject[] mobileOnlyUis;
    public GameObject[] hidingIfDedicateServerUis;
    private bool isNetworkActiveDirty;
    private bool isRespawnShown;

    private void Update()
    {
        var isNetworkActive = SimpleLanNetworkManager.Singleton.IsNetworkActive;
        if (isNetworkActiveDirty != isNetworkActive)
        {
            foreach (var hidingIfDedicateUi in hidingIfDedicateServerUis)
            {
                if (hidingIfDedicateUi != null)
                    hidingIfDedicateUi.SetActive(!SimpleLanNetworkManager.Singleton.IsServer || SimpleLanNetworkManager.Singleton.IsClient);
            }
            isNetworkActiveDirty = isNetworkActive;
        }

        foreach (var mobileOnlyUi in mobileOnlyUis)
        {
            bool showJoystick = Application.isMobilePlatform;
#if UNITY_EDITOR
            showJoystick = GameInstance.Singleton.showJoystickInEditor;
#endif
            if (mobileOnlyUi != null)
                mobileOnlyUi.SetActive(showJoystick);
        }

        var localCharacter = BaseNetworkGameCharacter.Local as CharacterEntity;
        if (localCharacter == null)
            return;

        var gameplayManager = GameplayManager.Singleton;

        if (textPowerUpBombRange != null)
            textPowerUpBombRange.text = localCharacter.PowerUpBombRange.ToString("N0") + "/" + gameplayManager.maxBombRangePowerUp;

        if (textPowerUpBombAmount != null)
            textPowerUpBombAmount.text = localCharacter.PowerUpBombAmount.ToString("N0") + "/" + gameplayManager.maxBombAmountPowerUp;

        if (textPowerUpHeart != null)
            textPowerUpHeart.text = localCharacter.PowerUpHeart.ToString("N0") + "/" + gameplayManager.maxHeartPowerUp;

        if (textPowerUpMoveSpeed != null)
            textPowerUpMoveSpeed.text = localCharacter.PowerUpMoveSpeed.ToString("N0") + "/" + gameplayManager.maxMoveSpeedPowerUp;

        if (localCharacter.isDead)
        {
            if (!isRespawnShown)
            {
                if (respawnUiContainer != null)
                    respawnUiContainer.SetActive(true);
                isRespawnShown = true;
            }
            if (isRespawnShown)
            {
                var remainTime = GameplayManager.Singleton.respawnDuration - (Time.unscaledTime - localCharacter.deathTime);
                var watchAdsRespawnAvailable = GameplayManager.Singleton.watchAdsRespawnAvailable;
                if (remainTime < 0)
                    remainTime = 0;
                if (textRespawnCountDown != null)
                    textRespawnCountDown.text = Mathf.Abs(remainTime).ToString("N0");
                if (textWatchedAdsCount != null)
                    textWatchedAdsCount.text = (watchAdsRespawnAvailable - localCharacter.watchAdsCount) + "/" + watchAdsRespawnAvailable;
                if (respawnButtonContainer != null)
                    respawnButtonContainer.SetActive(remainTime == 0);
            }
        }
        else
        {
            if (respawnUiContainer != null)
                respawnUiContainer.SetActive(false);
            isRespawnShown = false;
        }

        if (textMatchCountDown != null)
        {
            if (localCharacter.NetworkManager != null)
            {
                var formattedTime = string.Empty;
                var timer = localCharacter.NetworkManager.RemainsMatchTime;
                if (timer > 0f)
                {
                    int minutes = Mathf.FloorToInt(timer / 60f);
                    int seconds = Mathf.FloorToInt(timer - minutes * 60);
                    formattedTime = string.Format("{0:0}:{1:00}", minutes, seconds);
                }
                textMatchCountDown.text = formattedTime;
            }
        }

        if (matchEndUi != null)
        {
            if (localCharacter.NetworkManager != null)
                matchEndUi.SetActive(localCharacter.NetworkManager.IsMatchEnded);
        }
    }

    public void UpdateRankings(NetworkGameScore[] rankings)
    {
        for (var i = 0; i < uiGameScores.Length; ++i)
        {
            var uiGameScore = uiGameScores[i];
            if (uiGameScore != null)
                uiGameScore.UpdateRankings(rankings);
        }
    }

    public void KillNotify(string killerName, string victimName, string weaponId)
    {
        if (uiKillNotifies != null)
        {
            string weaponName = "Unknow Weapon";
            Texture weaponIcon = null;
            var bombData = GameInstance.GetBomb(weaponId.MakeHashId());
            if (bombData != null)
            {
                weaponName = bombData.title;
                weaponIcon = bombData.iconTexture;
            }
            uiKillNotifies.Notify(killerName, victimName, weaponName, weaponIcon);
        }
    }

    public void Respawn()
    {
        var character = BaseNetworkGameCharacter.Local as CharacterEntity;
        if (character == null)
            return;
        character.CmdRespawn(false);
    }

    public void WatchAdsRespawn()
    {
        var character = BaseNetworkGameCharacter.Local as CharacterEntity;
        if (character == null)
            return;

        if (character.watchAdsCount >= GameplayManager.Singleton.watchAdsRespawnAvailable)
        {
            character.CmdRespawn(false);
            return;
        }
        MonetizationManager.ShowAd(GameInstance.Singleton.watchAdsRespawnPlacement, OnWatchAdsRespawnResult);
    }

    private void OnWatchAdsRespawnResult(MonetizationManager.RemakeShowResult result)
    {
        if (result == MonetizationManager.RemakeShowResult.Finished)
        {
            var character = BaseNetworkGameCharacter.Local as CharacterEntity;
            character.CmdRespawn(true);
        }
    }
    
    public void ExitGame()
    {
        if (blackFade != null)
        {
            blackFade.onFadeIn.AddListener(() =>
            {
                GameNetworkManager.Singleton.StopHost();
            });
            blackFade.FadeIn();
        }
        else
        {
            Destroy(gameObject);
            GameNetworkManager.Singleton.StopHost();
        }
    }

    public void FadeIn()
    {
        if (blackFade != null)
            blackFade.FadeIn();
    }

    public void FadeOut()
    {
        if (blackFade != null)
            blackFade.FadeOut();
    }
}
