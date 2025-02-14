using DCL;
using DCL.Configuration;
using DCL.Helpers.NFT;
using System.Collections.Generic;
using System.Linq;

public static class BIWCatalogManager
{
    public static bool VERBOSE = false;
    private static bool IS_INIT = false;

    public static void Init()
    {
        if (!IS_INIT)
        {
            BuilderInWorldNFTController.i.OnNftsFetched += ConvertCollectiblesPack;
            AssetCatalogBridge.OnSceneObjectAdded += AddSceneObject;
            AssetCatalogBridge.OnSceneAssetPackAdded += AddSceneAssetPack;
            IS_INIT = true;
        }
    }

    public static void Dispose()
    {
        BuilderInWorldNFTController.i.OnNftsFetched -= ConvertCollectiblesPack;
        AssetCatalogBridge.OnSceneObjectAdded -= AddSceneObject;
        AssetCatalogBridge.OnSceneAssetPackAdded -= AddSceneAssetPack;
        IS_INIT = false;
    }

    public static void ClearCatalog()
    {
        DataStore.i.builderInWorld.catalogItemDict.Clear();
        DataStore.i.builderInWorld.catalogItemPackDict.Clear();
    }

    public static List<CatalogItemPack> GetCatalogItemPackList()
    {
        var assetPacks = DataStore.i.builderInWorld.catalogItemPackDict.GetValues();

        foreach (CatalogItemPack catalogAssetPack in assetPacks)
        {
            foreach (CatalogItem catalogItem in catalogAssetPack.assets)
            {
                catalogItem.categoryName = catalogItem.category;
            }
        }

        return assetPacks.OrderBy(x => x.title).ToList();
    }

    public static List<CatalogItemPack> GetCatalogItemPacksFilteredByCategories()
    {
        var assetPacks = DataStore.i.builderInWorld.catalogItemPackDict.GetValues();

        Dictionary<string, CatalogItemPack> assetPackDic = new Dictionary<string, CatalogItemPack>();

        if (DataStore.i.builderInWorld.catalogItemPackDict.ContainsKey(BuilderInWorldSettings.ASSETS_COLLECTIBLES))
            assetPackDic.Add(BuilderInWorldSettings.ASSETS_COLLECTIBLES, DataStore.i.builderInWorld.catalogItemPackDict[BuilderInWorldSettings.ASSETS_COLLECTIBLES]);
        else
            CreateNewCollectiblePack();

        foreach (CatalogItemPack catalogAssetPack in assetPacks)
        {
            foreach (CatalogItem catalogItem in catalogAssetPack.assets)
            {
                if (catalogItem.IsNFT())
                    continue;
                if (!assetPackDic.ContainsKey(catalogItem.category))
                {
                    CatalogItemPack categoryAssetPack = new CatalogItemPack();
                    categoryAssetPack.SetThumbnailULR(catalogItem.thumbnailURL);
                    categoryAssetPack.title = catalogItem.category;
                    categoryAssetPack.assets = new List<CatalogItem>();
                    catalogItem.categoryName = catalogAssetPack.title;
                    categoryAssetPack.assets.Add(catalogItem);

                    if (!string.IsNullOrEmpty(categoryAssetPack.title))
                    {
                        if (categoryAssetPack.title.Length == 1)
                            categoryAssetPack.title = categoryAssetPack.title.ToUpper();
                        else
                            categoryAssetPack.title = char.ToUpper(categoryAssetPack.title[0]) + categoryAssetPack.title.Substring(1);
                    }

                    assetPackDic.Add(catalogItem.category, categoryAssetPack);
                    continue;
                }
                else
                {
                    catalogItem.categoryName = catalogAssetPack.title;
                    assetPackDic[catalogItem.category].assets.Add(catalogItem);
                }
            }
        }

        return assetPackDic.Values.OrderBy(x => x.title).ToList();
    }

    public static void AddSceneObject(SceneObject sceneObject)
    {
        if (DataStore.i.builderInWorld.catalogItemDict.ContainsKey(sceneObject.id))
            return;

        //TODO: SmartItems disabled until kernel has them implemented
        if (sceneObject.IsSmartItem())
            return;

        CatalogItem catalogItem = CreateCatalogItem(sceneObject);
        DataStore.i.builderInWorld.catalogItemDict.Add(catalogItem.id, catalogItem);
    }

    public static void AddSceneAssetPack(SceneAssetPack sceneAssetPack)
    {
        if (DataStore.i.builderInWorld.catalogItemPackDict.ContainsKey(sceneAssetPack.id))
            return;

        CatalogItemPack catalogItemPack = CreateCatalogItemPack(sceneAssetPack);

        if (catalogItemPack.assets.Count != 0)
            DataStore.i.builderInWorld.catalogItemPackDict.Add(catalogItemPack.id, catalogItemPack);
    }

    public static void ConvertCollectiblesPack(List<NFTInfo> nftList)
    {
        if (nftList == null)
            return;

        CatalogItemPack collectiblesItemPack;

        if (!DataStore.i.builderInWorld.catalogItemPackDict.ContainsKey(BuilderInWorldSettings.ASSETS_COLLECTIBLES))
        {
            collectiblesItemPack = CreateNewCollectiblePack();
        }
        else
        {
            collectiblesItemPack = DataStore.i.builderInWorld.catalogItemPackDict[BuilderInWorldSettings.ASSETS_COLLECTIBLES];
            foreach (CatalogItem catalogItem in collectiblesItemPack.assets)
            {
                if (DataStore.i.builderInWorld.catalogItemDict.ContainsKey(catalogItem.id))
                    DataStore.i.builderInWorld.catalogItemDict.Remove(catalogItem.id);
            }

            collectiblesItemPack.assets.Clear();
        }

        foreach (NFTInfo info in nftList)
        {
            CatalogItem catalogItem = CreateCatalogItem(info);
            if (!DataStore.i.builderInWorld.catalogItemDict.ContainsKey(catalogItem.id))
                DataStore.i.builderInWorld.catalogItemDict.Add(catalogItem.id, catalogItem);

            collectiblesItemPack.assets.Add(catalogItem);
        }
    }

    private static CatalogItemPack CreateNewCollectiblePack()
    {
        CatalogItemPack collectiblesItemPack = new CatalogItemPack();
        collectiblesItemPack.id = BuilderInWorldSettings.ASSETS_COLLECTIBLES;
        collectiblesItemPack.title = BuilderInWorldSettings.ASSETS_COLLECTIBLES;
        collectiblesItemPack.assets = new List<CatalogItem>();
        if (!DataStore.i.builderInWorld.catalogItemPackDict.ContainsKey(collectiblesItemPack.id))
            DataStore.i.builderInWorld.catalogItemPackDict.Add(collectiblesItemPack.id, collectiblesItemPack);
        return collectiblesItemPack;
    }

    public static CatalogItemPack CreateCatalogItemPack(SceneAssetPack sceneAssetPack)
    {
        CatalogItemPack catalogItemPack = new CatalogItemPack();

        catalogItemPack.id = sceneAssetPack.id;
        catalogItemPack.title = sceneAssetPack.title;

        catalogItemPack.assets = new List<CatalogItem>();

        catalogItemPack.SetThumbnailULR(sceneAssetPack.ComposeThumbnailUrl());

        foreach (SceneObject sceneObject in sceneAssetPack.assets)
        {
            //TODO: SmartItems disabled until kernel has them implemented
            if (sceneObject.IsSmartItem())
                continue;

            catalogItemPack.assets.Add(CreateCatalogItem(sceneObject));
        }

        return catalogItemPack;
    }

    public static CatalogItem CreateCatalogItem(SceneObject sceneObject)
    {
        CatalogItem catalogItem = new CatalogItem();
        catalogItem.id = sceneObject.id;
        if (sceneObject.asset_pack_id == BuilderInWorldSettings.VOXEL_ASSETS_PACK_ID)
            catalogItem.isVoxel = true;
        catalogItem.name = sceneObject.name;
        catalogItem.model = sceneObject.model;
        catalogItem.thumbnailURL = sceneObject.GetComposedThumbnailUrl();
        catalogItem.tags = sceneObject.tags;

        catalogItem.category = sceneObject.category;
        catalogItem.categoryName = catalogItem.category;

        catalogItem.contents = sceneObject.contents;

        catalogItem.metrics = sceneObject.metrics;

        if (!string.IsNullOrEmpty(sceneObject.script))
        {
            catalogItem.itemType = CatalogItem.ItemType.SMART_ITEM;
            catalogItem.parameters = sceneObject.parameters;
            catalogItem.actions = sceneObject.actions;
        }
        else
        {
            catalogItem.itemType = CatalogItem.ItemType.SCENE_OBJECT;
        }

        return catalogItem;
    }

    public static CatalogItem CreateCatalogItem(NFTInfo nFTInfo)
    {
        CatalogItem catalogItem = new CatalogItem();
        catalogItem.itemType = CatalogItem.ItemType.NFT;

        catalogItem.id = nFTInfo.assetContract.address;
        catalogItem.thumbnailURL = nFTInfo.thumbnailUrl;
        catalogItem.name = nFTInfo.name;
        catalogItem.category = nFTInfo.assetContract.name;
        catalogItem.model = $"{BuilderInWorldSettings.COLLECTIBLE_MODEL_PROTOCOL}{nFTInfo.assetContract.address}/{nFTInfo.tokenId}";
        catalogItem.tags = new List<string>();
        catalogItem.contents = new Dictionary<string, string>();
        catalogItem.metrics = new SceneObject.ObjectMetrics();

        return catalogItem;
    }
}