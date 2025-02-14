using DCL;
using DCL.Components;
using DCL.Configuration;
using DCL.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Serialization;

public class BIWCreatorController : BIWController
{
    [Header("Prefab references")]
    public BIWModeController biwModeController;

    public BIWFloorHandler biwFloorHandler;
    public BuilderInWorldEntityHandler builderInWorldEntityHandler;

    [FormerlySerializedAs("loadingGO")]
    [Header("Project references")]
    public GameObject loadingObjectPrefab;
    public GameObject errorPrefab;

    [SerializeField]
    internal InputAction_Trigger toggleCreateLastSceneObjectInputAction;

    public Action OnInputDone;

    public Action OnCatalogItemPlaced;

    private CatalogItem lastCatalogItemCreated;

    private InputAction_Trigger.Triggered createLastSceneObjectDelegate;

    private readonly Dictionary<string, BIWLoadingPlaceHolder> loadingGameObjects = new Dictionary<string, BIWLoadingPlaceHolder>();
    private readonly Dictionary<DCLBuilderInWorldEntity, GameObject> errorGameObjects = new Dictionary<DCLBuilderInWorldEntity, GameObject>();

    private void Start()
    {
        createLastSceneObjectDelegate = (action) => CreateLastCatalogItem();
        toggleCreateLastSceneObjectInputAction.OnTriggered += createLastSceneObjectDelegate;
    }

    private void OnDestroy()
    {
        toggleCreateLastSceneObjectInputAction.OnTriggered -= createLastSceneObjectDelegate;
        if (HUDController.i.builderInWorldMainHud != null)
        {
            HUDController.i.builderInWorldMainHud.OnCatalogItemSelected -= OnCatalogItemSelected;
            HUDController.i.builderInWorldMainHud.OnCatalogItemDropped -= OnCatalogItemDropped;
        }

        Clean();
    }

    public void Clean()
    {
        foreach (BIWLoadingPlaceHolder placeHolder in loadingGameObjects.Values)
        {
            placeHolder.Dispose();
        }

        foreach (DCLBuilderInWorldEntity entity in errorGameObjects.Keys.ToArray())
        {
            DeleteErrorOnEntity(entity);
        }

        loadingGameObjects.Clear();
        errorGameObjects.Clear();
    }

    public override void Init()
    {
        base.Init();
        if (HUDController.i.builderInWorldMainHud != null)
        {
            HUDController.i.builderInWorldMainHud.OnCatalogItemSelected += OnCatalogItemSelected;
            HUDController.i.builderInWorldMainHud.OnCatalogItemDropped += OnCatalogItemDropped;
        }
    }

    public bool IsAnyErrorOnEntities() { return errorGameObjects.Count > 0; }

    private bool IsInsideTheLimits(CatalogItem sceneObject)
    {
        if (HUDController.i.builderInWorldMainHud == null)
            return false;

        SceneMetricsModel limits = sceneToEdit.metricsController.GetLimits();
        SceneMetricsModel usage = sceneToEdit.metricsController.GetModel();

        if (limits.bodies < usage.bodies + sceneObject.metrics.bodies)
        {
            HUDController.i.builderInWorldMainHud.ShowSceneLimitsPassed();
            return false;
        }

        if (limits.entities < usage.entities + sceneObject.metrics.entities)
        {
            HUDController.i.builderInWorldMainHud.ShowSceneLimitsPassed();
            return false;
        }

        if (limits.materials < usage.materials + sceneObject.metrics.materials)
        {
            HUDController.i.builderInWorldMainHud.ShowSceneLimitsPassed();
            return false;
        }

        if (limits.meshes < usage.meshes + sceneObject.metrics.meshes)
        {
            HUDController.i.builderInWorldMainHud.ShowSceneLimitsPassed();
            return false;
        }

        if (limits.textures < usage.textures + sceneObject.metrics.textures)
        {
            HUDController.i.builderInWorldMainHud.ShowSceneLimitsPassed();
            return false;
        }

        if (limits.triangles < usage.triangles + sceneObject.metrics.triangles)
        {
            HUDController.i.builderInWorldMainHud.ShowSceneLimitsPassed();
            return false;
        }

        return true;
    }

    public void CreateErrorOnEntity(DCLBuilderInWorldEntity entity)
    {
        if (errorGameObjects.ContainsKey(entity))
            return;

        GameObject instantiatedError = Instantiate(errorPrefab, entity.transform);
        errorGameObjects.Add(entity, instantiatedError);
        entity.OnDelete += DeleteErrorOnEntity;
    }

    public void DeleteErrorOnEntity(DCLBuilderInWorldEntity entity)
    {
        if (!errorGameObjects.ContainsKey(entity))
            return;

        entity.OnDelete -= DeleteErrorOnEntity;
        Destroy(errorGameObjects[entity]);
        errorGameObjects.Remove(entity);
    }

    public void CreateCatalogItem(CatalogItem catalogItem, bool autoSelect = true, bool isFloor = false) { CreateCatalogItem(catalogItem, biwModeController.GetModeCreationEntryPoint(), autoSelect, isFloor); }

    public DCLBuilderInWorldEntity CreateCatalogItem(CatalogItem catalogItem, Vector3 startPosition, bool autoSelect = true, bool isFloor = false, Action<IDCLEntity> onFloorLoadedAction = null)
    {
        if (catalogItem.IsNFT() && BuilderInWorldNFTController.i.IsNFTInUse(catalogItem.id))
            return null;

        IsInsideTheLimits(catalogItem);

        //Note (Adrian): This is a workaround until the mapping is handle by kernel
        AddSceneMappings(catalogItem);

        Vector3 editionPosition = biwModeController.GetCurrentEditionPosition();

        DCLBuilderInWorldEntity entity = builderInWorldEntityHandler.CreateEmptyEntity(sceneToEdit, startPosition, editionPosition, false);
        entity.isFloor = isFloor;
        entity.SetRotation(Vector3.zero);

        if (!isFloor)
            CreateLoadingObject(entity);

        entity.rootEntity.OnShapeUpdated += (entity) => onFloorLoadedAction?.Invoke(entity);
        AddShape(catalogItem, entity);

        AddEntityNameComponent(catalogItem, entity);

        AddLockedComponent(entity);

        if (catalogItem.IsSmartItem())
        {
            AddSmartItemComponent(entity);
        }

        if (catalogItem.IsVoxel())
            entity.isVoxel = true;

        if (autoSelect)
        {
            builderInWorldEntityHandler.DeselectEntities();
            builderInWorldEntityHandler.Select(entity.rootEntity);
        }

        entity.gameObject.transform.eulerAngles = Vector3.zero;

        biwModeController.CreatedEntity(entity);

        lastCatalogItemCreated = catalogItem;

        builderInWorldEntityHandler.EntityListChanged();
        builderInWorldEntityHandler.NotifyEntityIsCreated(entity.rootEntity);
        OnInputDone?.Invoke();
        OnCatalogItemPlaced?.Invoke();
        return entity;
    }

    #region LoadingObjects

    public bool ExistsLoadingGameObjectForEntity(string entityId) { return loadingGameObjects.ContainsKey(entityId); }

    public void CreateLoadingObject(DCLBuilderInWorldEntity entity)
    {
        if (loadingGameObjects.ContainsKey(entity.rootEntity.entityId))
            return;

        BIWLoadingPlaceHolder loadingPlaceHolder = GameObject.Instantiate(loadingObjectPrefab, entity.gameObject.transform).GetComponent<BIWLoadingPlaceHolder>();
        loadingGameObjects.Add(entity.rootEntity.entityId, loadingPlaceHolder);
        entity.OnShapeFinishLoading += OnShapeLoadFinish;
    }

    private void OnShapeLoadFinish(DCLBuilderInWorldEntity entity)
    {
        entity.OnShapeFinishLoading -= OnShapeLoadFinish;
        RemoveLoadingObject(entity.rootEntity.entityId);
    }

    public void RemoveLoadingObject(string entityId)
    {
        if (!loadingGameObjects.ContainsKey(entityId))
            return;
        BIWLoadingPlaceHolder loadingPlaceHolder = loadingGameObjects[entityId];
        loadingGameObjects.Remove(entityId);
        loadingPlaceHolder.DestroyAfterAnimation();
    }

    public void RemoveLoadingObjectInmediate(string entityId)
    {
        if (!loadingGameObjects.ContainsKey(entityId))
            return;
        BIWLoadingPlaceHolder loadingPlaceHolder = loadingGameObjects[entityId];
        loadingGameObjects.Remove(entityId);
        loadingPlaceHolder.Dispose();
    }

    #endregion

    #region Add Components

    private void AddSmartItemComponent(DCLBuilderInWorldEntity entity)
    {
        //Note (Adrian): This will disable the smart item component until it is implemented in kernel
        //TODO: After the implementation in kernel of smart items, we should eliminate this return
        return;
        SmartItemComponent.Model model = new SmartItemComponent.Model();
        model.values = new Dictionary<object, object>();

        sceneToEdit.EntityComponentCreateOrUpdateWithModel(entity.rootEntity.entityId, CLASS_ID_COMPONENT.SMART_ITEM, model);

        //Note (Adrian): We can't wait to set the component 1 frame, so we set it
        if (entity.rootEntity.TryGetBaseComponent(CLASS_ID_COMPONENT.SMART_ITEM, out IEntityComponent component))
            ((SmartItemComponent) component).UpdateFromModel(model);
    }

    private void AddEntityNameComponent(CatalogItem catalogItem, DCLBuilderInWorldEntity entity)
    {
        DCLName name = (DCLName) sceneToEdit.SharedComponentCreate(Guid.NewGuid().ToString(), Convert.ToInt32(CLASS_ID.NAME));
        sceneToEdit.SharedComponentAttach(entity.rootEntity.entityId, name.id);
        builderInWorldEntityHandler.SetEntityName(entity, catalogItem.name, false);
    }

    private void AddLockedComponent(DCLBuilderInWorldEntity entity)
    {
        DCLLockedOnEdit entityLocked = (DCLLockedOnEdit) sceneToEdit.SharedComponentCreate(Guid.NewGuid().ToString(), Convert.ToInt32(CLASS_ID.LOCKED_ON_EDIT));
        if (entity.isFloor)
            entityLocked.SetIsLocked(true);
        else
            entityLocked.SetIsLocked(false);

        sceneToEdit.SharedComponentAttach(entity.rootEntity.entityId, entityLocked.id);
    }

    private void AddShape(CatalogItem catalogItem, DCLBuilderInWorldEntity entity)
    {
        if (catalogItem.IsNFT())
        {
            NFTShape nftShape = (NFTShape) sceneToEdit.SharedComponentCreate(catalogItem.id, Convert.ToInt32(CLASS_ID.NFT_SHAPE));
            nftShape.model = new NFTShape.Model();
            nftShape.model.color = new Color(0.6404918f, 0.611472f, 0.8584906f);
            nftShape.model.src = catalogItem.model;
            nftShape.model.assetId = catalogItem.id;
            sceneToEdit.SharedComponentAttach(entity.rootEntity.entityId, nftShape.id);

            nftShape.CallWhenReady(entity.ShapeLoadFinish);
        }
        else
        {
            GLTFShape gltfComponent = (GLTFShape) sceneToEdit.SharedComponentCreate(catalogItem.id, Convert.ToInt32(CLASS_ID.GLTF_SHAPE));
            gltfComponent.model = new LoadableShape.Model();
            gltfComponent.model.src = catalogItem.model;
            gltfComponent.model.assetId = catalogItem.id;
            sceneToEdit.SharedComponentAttach(entity.rootEntity.entityId, gltfComponent.id);

            gltfComponent.CallWhenReady(entity.ShapeLoadFinish);
        }
    }

    #endregion

    private void AddSceneMappings(CatalogItem catalogItem)
    {
        LoadParcelScenesMessage.UnityParcelScene data = sceneToEdit.sceneData;
        data.baseUrl = BuilderInWorldSettings.BASE_URL_CATALOG;
        if (data.contents == null)
            data.contents = new List<ContentServerUtils.MappingPair>();
        foreach (KeyValuePair<string, string> content in catalogItem.contents)
        {
            ContentServerUtils.MappingPair mappingPair = new ContentServerUtils.MappingPair();
            mappingPair.file = content.Key;
            mappingPair.hash = content.Value;
            bool found = false;
            foreach (ContentServerUtils.MappingPair mappingPairToCheck in data.contents)
            {
                if (mappingPairToCheck.file == mappingPair.file)
                {
                    found = true;
                    break;
                }
            }

            if (!found)
                data.contents.Add(mappingPair);
        }

        DCL.Environment.i.world.sceneController.UpdateParcelScenesExecute(data);
    }

    public void CreateLastCatalogItem()
    {
        if (lastCatalogItemCreated != null)
        {
            if (builderInWorldEntityHandler.IsAnyEntitySelected())
                builderInWorldEntityHandler.DeselectEntities();
            OnCatalogItemSelected(lastCatalogItemCreated);
            OnInputDone?.Invoke();
        }
    }

    private void OnCatalogItemSelected(CatalogItem catalogItem)
    {
        if (biwFloorHandler.IsCatalogItemFloor(catalogItem))
        {
            biwFloorHandler.ChangeFloor(catalogItem);
        }
        else
        {
            CreateCatalogItem(catalogItem);
        }
    }

    private void OnCatalogItemDropped(CatalogItem catalogItem)
    {
        OnCatalogItemSelected(catalogItem);
        builderInWorldEntityHandler.DeselectEntities();
    }
}