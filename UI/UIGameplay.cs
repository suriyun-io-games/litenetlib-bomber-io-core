using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Networking;

public class UIGameplay : MonoBehaviour
{
    public Text textPowerUpBombRange;
    public Text textPowerUpBombAmount;
    public Text textPowerUpHeart;
    public Text textPowerUpMoveSpeed;
    public Text textRespawnCountDown;
    public Text textWatchedAdsCount;
    public UIBlackFade blackFade;
    public GameObject respawnUiContainer;
    public GameObject respawnButtonContainer;
    public UINetworkGameScoreEntry[] userRankings;
    public UINetworkGameScoreEntry localRanking;
    public GameObject[] mobileOnlyUis;
    public GameObject[] hidingIfDedicateServerUis;
    private bool isNetworkActiveDirty;
    private bool isRespawnShown;

    private void Awake()
    {
        foreach (var mobileOnlyUi in mobileOnlyUis)
        {
            mobileOnlyUi.SetActive(Application.isMobilePlatform);
        }
    }

    private void Update()
    {
        var isNetworkActive = NetworkManager.singleton.isNetworkActive;
        if (isNetworkActiveDirty != isNetworkActive)
        {
            foreach (var hidingIfDedicateUi in hidingIfDedicateServerUis)
            {
                hidingIfDedicateUi.SetActive(!NetworkServer.active || NetworkServer.localClientActive);
            }
            if (isNetworkActive)
                FadeOut();
            isNetworkActiveDirty = isNetworkActive;
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
    }

    public void UpdateRankings(NetworkGameScore[] rankings)
    {
        for (var i = 0; i < userRankings.Length; ++i)
        {
            var userRanking = userRankings[i];
            if (i < rankings.Length)
            {
                var ranking = rankings[i];
                userRanking.SetData(i + 1, ranking);

                var isLocal = BaseNetworkGameCharacter.Local != null && ranking.netId.Equals(BaseNetworkGameCharacter.Local.netId);
                if (isLocal)
                    UpdateLocalRank(i + 1, ranking);
            }
            else
                userRanking.Clear();
        }
    }

    public void UpdateLocalRank(int rank, NetworkGameScore ranking)
    {
        if (localRanking != null)
            localRanking.SetData(rank, ranking);
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
        GameNetworkManager.Singleton.StopHost();
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
