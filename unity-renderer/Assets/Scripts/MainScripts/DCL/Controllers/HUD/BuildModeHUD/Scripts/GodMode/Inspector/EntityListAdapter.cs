using DCL;
using DCL.Models;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class EntityListAdapter : MonoBehaviour
{
    public Color entitySelectedColor;
    public Color entityUnselectedColor;
    public Color entityInsideOfBoundsColor;
    public Color entityOutOfBoundsColor;
    public Color iconsSelectedColor;
    public Color iconsUnselectedColor;
    public TMP_InputField nameInputField;
    public TextMeshProUGUI nameInputField_Text;
    public Image selectedImg;
    public RawImage entityThumbnailImg;
    public Button unlockButton;
    public Button lockButton;
    public Image showImg;
    public Image textBoxImage;
    public System.Action<EntityAction, DCLBuilderInWorldEntity, EntityListAdapter> OnActionInvoked;
    public System.Action<DCLBuilderInWorldEntity, string> OnEntityRename;
    DCLBuilderInWorldEntity currentEntity;
    internal AssetPromise_Texture loadedThumbnailPromise;

    private void Start()
    {
        if (nameInputField != null)
        {
            nameInputField.onSelect.AddListener((currentText) => SetTextboxActive(true));

            nameInputField.onEndEdit.AddListener((newText) =>
            {
                Rename(newText);
                SetTextboxActive(false);

                if (EventSystem.current != null && !EventSystem.current.alreadySelecting)
                    EventSystem.current.SetSelectedGameObject(null);
            });

            nameInputField.onSubmit.AddListener((newText) => EventSystem.current?.SetSelectedGameObject(null));
        }

        SetTextboxActive(false);
    }

    private void OnDestroy()
    {
        if (nameInputField != null)
        {
            nameInputField.onSelect.RemoveAllListeners();
            nameInputField.onEndEdit.RemoveAllListeners();
            nameInputField.onSubmit.RemoveAllListeners();
        }

        if (currentEntity != null)
        {
            currentEntity.OnStatusUpdate -= SetInfo;
            currentEntity.OnDelete -= DeleteAdapter;
            DCL.Environment.i.world.sceneBoundsChecker.OnEntityBoundsCheckerStatusChanged -= ChangeEntityBoundsCheckerStatus;
        }
    }

    public void SetContent(DCLBuilderInWorldEntity decentrelandEntity)
    {
        if (currentEntity != null)
        {
            currentEntity.OnStatusUpdate -= SetInfo;
            currentEntity.OnDelete -= DeleteAdapter;
            DCL.Environment.i.world.sceneBoundsChecker.OnEntityBoundsCheckerStatusChanged -= ChangeEntityBoundsCheckerStatus;
        }

        currentEntity = decentrelandEntity;
        currentEntity.OnStatusUpdate += SetInfo;
        currentEntity.OnDelete += DeleteAdapter;
        DCL.Environment.i.world.sceneBoundsChecker.OnEntityBoundsCheckerStatusChanged += ChangeEntityBoundsCheckerStatus;

        AllowNameEdition(false);
        SetInfo(decentrelandEntity);

        entityThumbnailImg.enabled = false;
        CatalogItem entitySceneObject = decentrelandEntity.GetCatalogItemAssociated();
        GetThumbnail(entitySceneObject);
    }

    public void SelectOrDeselect()
    {
        if (currentEntity.IsVisible)
            OnActionInvoked?.Invoke(EntityAction.SELECT, currentEntity, this);
    }

    public void ShowOrHide() { OnActionInvoked?.Invoke(EntityAction.SHOW, currentEntity, this); }

    public void LockOrUnlock() { OnActionInvoked?.Invoke(EntityAction.LOCK, currentEntity, this); }

    public void DeleteEntity() { OnActionInvoked?.Invoke(EntityAction.DELETE, currentEntity, this); }

    void SetInfo(DCLBuilderInWorldEntity entityToEdit)
    {
        if (this != null)
        {
            if (string.IsNullOrEmpty(entityToEdit.GetDescriptiveName()))
                nameInputField.text = entityToEdit.rootEntity.entityId;
            else
                nameInputField.text = entityToEdit.GetDescriptiveName();

            //NOTE (Adrian): this is done to force the text component to update, otherwise it won't show the text, seems like a bug on textmeshpro to me
            nameInputField.textComponent.enabled = true;

            showImg.color = entityToEdit.IsVisible ? iconsSelectedColor : iconsUnselectedColor;
            nameInputField_Text.color = (!entityToEdit.IsVisible || entityToEdit.IsLocked) ? iconsUnselectedColor : iconsSelectedColor;

            unlockButton.gameObject.SetActive(!entityToEdit.IsLocked);
            lockButton.gameObject.SetActive(entityToEdit.IsLocked);

            if (entityToEdit.IsSelected)
            {
                AllowNameEdition(true);
                selectedImg.color = entitySelectedColor;
            }
            else
            {
                AllowNameEdition(false);
                selectedImg.color = entityUnselectedColor;
            }
        }
    }

    internal void GetThumbnail(CatalogItem catalogItem)
    {
        if (catalogItem == null)
            return;

        var url = catalogItem.thumbnailURL;

        if (string.IsNullOrEmpty(url))
            return;

        var newLoadedThumbnailPromise = new AssetPromise_Texture(url);
        newLoadedThumbnailPromise.OnSuccessEvent += SetThumbnail;
        newLoadedThumbnailPromise.OnFailEvent += x => { Debug.Log($"Error downloading: {url}"); };
        AssetPromiseKeeper_Texture.i.Keep(newLoadedThumbnailPromise);
        AssetPromiseKeeper_Texture.i.Forget(loadedThumbnailPromise);
        loadedThumbnailPromise = newLoadedThumbnailPromise;
    }

    internal void SetThumbnail(Asset_Texture texture)
    {
        if (entityThumbnailImg == null)
            return;
        entityThumbnailImg.enabled = true;
        entityThumbnailImg.texture = texture.texture;
    }

    public void Rename(string newName)
    {
        if (!string.IsNullOrEmpty(newName))
            OnEntityRename?.Invoke(currentEntity, newName);
        else
            nameInputField.text = currentEntity.GetDescriptiveName();
    }

    public void AllowNameEdition(bool isAllowed) { nameInputField.enabled = isAllowed; }

    void DeleteAdapter(DCLBuilderInWorldEntity entityToEdit)
    {
        if (this != null)
            if (entityToEdit.entityUniqueId == currentEntity.entityUniqueId)
                Destroy(gameObject);
    }

    private void ChangeEntityBoundsCheckerStatus(IDCLEntity entity, bool isInsideBoundaries)
    {
        if (currentEntity.rootEntity.entityId != entity.entityId || !currentEntity.IsVisible || currentEntity.IsLocked)
            return;

        nameInputField_Text.color = isInsideBoundaries ? entityInsideOfBoundsColor : entityOutOfBoundsColor;
    }

    private void SetTextboxActive(bool isActive)
    {
        if (textBoxImage == null)
            return;

        textBoxImage.enabled = isActive;
    }
}