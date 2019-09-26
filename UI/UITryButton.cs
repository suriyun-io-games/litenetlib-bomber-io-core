using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class UITryButton : MonoBehaviour
{
    public UIInGameProductData uiProductData;
    private UIMainMenu uiMainMenu;

    private void Start()
    {
        uiMainMenu = FindObjectOfType<UIMainMenu>();
    }

    public void OnClickTry()
    {
        var characterModel = uiMainMenu.characterModel;
        var bombEntity = uiMainMenu.bombEntity;
        var characterData = uiMainMenu.characterData;
        var bombData = uiMainMenu.bombData;
        var headData = uiMainMenu.headData;

        if (uiProductData.productData is CharacterData)
        {
            characterData = uiProductData.productData as CharacterData;
            Destroy(characterModel.gameObject);
            if (characterData == null || characterData.modelObject == null)
                return;
            characterModel = Instantiate(characterData.modelObject, uiMainMenu.characterModelTransform);
            characterModel.transform.localPosition = Vector3.zero;
            characterModel.transform.localEulerAngles = Vector3.zero;
            characterModel.transform.localScale = Vector3.one;
            if (headData != null)
                characterModel.SetHeadModel(headData.modelObject);
            characterModel.gameObject.SetActive(true);
        }

        if (uiProductData.productData is BombData)
        {
            bombData = uiProductData.productData as BombData;
            Destroy(bombEntity.gameObject);
            if (bombData == null || bombData.bombPrefab == null)
                return;
            bombEntity = Instantiate(bombData.bombPrefab, uiMainMenu.bombEntityTransform);
            bombEntity.transform.localPosition = Vector3.zero;
            bombEntity.transform.localEulerAngles = Vector3.zero;
            bombEntity.transform.localScale = Vector3.one;
            bombEntity.gameObject.SetActive(true);
        }

        if (uiProductData.productData is HeadData)
        {
            headData = uiProductData.productData as HeadData;
            characterModel.SetHeadModel(headData.modelObject);
        }

        uiMainMenu.characterModel = characterModel;
        uiMainMenu.bombEntity = bombEntity;
        uiMainMenu.characterData = characterData;
        uiMainMenu.bombData = bombData;
        uiMainMenu.headData = headData;
    }
}
