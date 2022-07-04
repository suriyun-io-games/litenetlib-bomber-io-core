using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class UIMainMenu : MonoBehaviour
{
    public enum PreviewState
    {
        Idle,
        Run,
        Dead,
    }
    public Text textSelectCharacter;
    public Text textSelectHead;
    public Text textSelectBomb;
    public InputField inputName;
    public Transform characterModelTransform;
    public Transform bombEntityTransform;
    public string onlineNetworkAddress;
    public int onlineNetworkPort;
    public UIEnterNetworkAddress enterNetworkAddressDialog;
    public UILanNetworking lanNetworkingDialog;
    private int selectCharacter = 0;
    private int selectHead = 0;
    private int selectBomb = 0;
    private bool readyToUpdate;
    // Showing character / items
    public CharacterModel characterModel;
    public BombEntity bombEntity;
    public CharacterData characterData;
    public HeadData headData;
    public BombData bombData;
    public PreviewState previewState;

    public int SelectCharacter
    {
        get { return selectCharacter; }
        set
        {
            selectCharacter = value;
            if (selectCharacter < 0)
                selectCharacter = MaxCharacter;
            if (selectCharacter > MaxCharacter)
                selectCharacter = 0;
            UpdateCharacter();
        }
    }

    public int SelectHead
    {
        get { return selectHead; }
        set
        {
            selectHead = value;
            if (selectHead < 0)
                selectHead = MaxHead;
            if (selectHead > MaxHead)
                selectHead = 0;
            UpdateHead();
        }
    }

    public int SelectBomb
    {
        get { return selectBomb; }
        set
        {
            selectBomb = value;
            if (selectBomb < 0)
                selectBomb = MaxBomb;
            if (selectBomb > MaxBomb)
                selectBomb = 0;
            UpdateBomb();
        }
    }

    public int MaxHead
    {
        get { return GameInstance.AvailableHeads.Count - 1; }
    }

    public int MaxCharacter
    {
        get { return GameInstance.AvailableCharacters.Count - 1; }
    }

    public int MaxBomb
    {
        get { return GameInstance.AvailableBombs.Count - 1; }
    }

    private void Start()
    {
        StartCoroutine(StartRoutine());
        var uis = FindObjectsOfType<UIProductList>(true);
        foreach (var ui in uis)
        {
            ui.onPurchaseSuccess.RemoveListener(UpdateAvailableItems);
            ui.onPurchaseSuccess.AddListener(UpdateAvailableItems);
        }
    }

    private IEnumerator StartRoutine()
    {
        yield return null;
        OnClickLoadData();
        readyToUpdate = true;
    }

    private void Update()
    {
        if (!readyToUpdate)
            return;

        textSelectCharacter.text = (SelectCharacter + 1) + "/" + (MaxCharacter + 1);
        textSelectHead.text = (SelectHead + 1) + "/" + (MaxHead + 1);
        textSelectBomb.text = (SelectBomb + 1) + "/" + (MaxBomb + 1);

        if (characterModel != null)
        {
            var animator = characterModel.CacheAnimator;
            switch (previewState)
            {
                case PreviewState.Idle:
                    animator.SetBool("IsDead", false);
                    animator.SetFloat("JumpSpeed", 0);
                    animator.SetFloat("MoveSpeed", 0);
                    animator.SetBool("IsGround", true);
                    break;
                case PreviewState.Run:
                    animator.SetBool("IsDead", false);
                    animator.SetFloat("JumpSpeed", 0);
                    animator.SetFloat("MoveSpeed", 1);
                    animator.SetBool("IsGround", true);
                    break;
                case PreviewState.Dead:
                    animator.SetBool("IsDead", true);
                    animator.SetFloat("JumpSpeed", 0);
                    animator.SetFloat("MoveSpeed", 0);
                    animator.SetBool("IsGround", true);
                    break;
            }
        }
    }

    private void UpdateCharacter()
    {
        if (characterModel != null)
            Destroy(characterModel.gameObject);
        characterData = GameInstance.GetAvailableCharacter(SelectCharacter);
        if (characterData == null || characterData.modelObject == null)
            return;
        characterModel = Instantiate(characterData.modelObject, characterModelTransform);
        characterModel.transform.localPosition = Vector3.zero;
        characterModel.transform.localEulerAngles = Vector3.zero;
        characterModel.transform.localScale = Vector3.one;
        if (headData != null)
            characterModel.SetHeadModel(headData.modelObject);
        characterModel.gameObject.SetActive(true);
    }

    private void UpdateHead()
    {
        headData = GameInstance.GetAvailableHead(SelectHead);
        if (characterModel != null && headData != null)
            characterModel.SetHeadModel(headData.modelObject);
    }

    private void UpdateBomb()
    {
        if (bombEntity != null)
            Destroy(bombEntity.gameObject);
        bombData = GameInstance.GetAvailableBomb(SelectBomb);
        if (bombData == null || bombData.bombPrefab == null)
            return;
        bombEntity = Instantiate(bombData.bombPrefab, bombEntityTransform);
        bombEntity.transform.localPosition = Vector3.zero;
        bombEntity.transform.localEulerAngles = Vector3.zero;
        bombEntity.transform.localScale = Vector3.one;
        bombEntity.gameObject.SetActive(true);
    }

    public void OnClickBackCharacter()
    {
        --SelectCharacter;
    }

    public void OnClickNextCharacter()
    {
        ++SelectCharacter;
    }

    public void OnClickBackHead()
    {
        --SelectHead;
    }

    public void OnClickNextHead()
    {
        ++SelectHead;
    }

    public void OnClickBackBomb()
    {
        --SelectBomb;
    }

    public void OnClickNextBomb()
    {
        ++SelectBomb;
    }

    public void OnInputNameChanged(string eventInput)
    {
        PlayerSave.SetPlayerName(inputName.text);
    }

    public void OnClickSaveData()
    {
        PlayerSave.SetCharacter(SelectCharacter);
        PlayerSave.SetHead(SelectHead);
        PlayerSave.SetBomb(SelectBomb);
        PlayerSave.SetPlayerName(inputName.text);
    }

    public void OnClickLoadData()
    {
        inputName.text = PlayerSave.GetPlayerName();
        SelectHead = PlayerSave.GetHead();
        SelectCharacter = PlayerSave.GetCharacter();
        SelectBomb = PlayerSave.GetBomb();
    }

    public void OnClickLan()
    {
        OnClickSaveData();
        if (lanNetworkingDialog != null)
            lanNetworkingDialog.Show();
    }

    public void OnClickOnline()
    {
        OnClickSaveData();
        if (!string.IsNullOrEmpty(onlineNetworkAddress) && onlineNetworkPort >= 0)
        {
            var networkManager = GameNetworkManager.Singleton;
            networkManager.networkAddress = onlineNetworkAddress;
            networkManager.networkPort = onlineNetworkPort;
            networkManager.StartClient();
            return;
        }
        if (enterNetworkAddressDialog != null)
            enterNetworkAddressDialog.Show();
    }

    public void UpdateAvailableItems()
    {
        GameInstance.Singleton.UpdateAvailableItems();
    }
}
