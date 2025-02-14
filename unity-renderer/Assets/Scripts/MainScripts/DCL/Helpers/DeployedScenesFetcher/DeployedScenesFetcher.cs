using System.Collections.Generic;
using System.Linq;
using DCL.Helpers;

public static class DeployedScenesFetcher
{
    private const float DEFAULT_SCENES_CACHE_TIME = 3 * 60;
    private const float DEFAULT_LAND_CACHE_TIME = 5 * 60;

    public static Promise<DeployedScene[]> FetchScenes(ICatalyst catalyst, string[] parcels, float cacheMaxAgeSeconds = DEFAULT_SCENES_CACHE_TIME)
    {
        Promise<DeployedScene[]> promise = new Promise<DeployedScene[]>();
        catalyst.GetDeployedScenes(parcels, cacheMaxAgeSeconds)
            .Then(result =>
            {
                promise.Resolve(result.Select(deployment => new DeployedScene(deployment, catalyst.contentUrl)).ToArray());
            })
            .Catch(err => promise.Reject(err));
        return promise;
    }

    public static Promise<LandWithAccess[]> FetchLandsFromOwner(ICatalyst catalyst, ITheGraph theGraph, string ethAddress, string tld,
        float cacheMaxAgeSecondsLand = DEFAULT_LAND_CACHE_TIME, float cacheMaxAgeSecondsScenes = DEFAULT_SCENES_CACHE_TIME)
    {
        Promise<LandWithAccess[]> resultPromise = new Promise<LandWithAccess[]>();

        List<Land> lands = new List<Land>();

        Promise<string[]> getOwnedParcelsPromise = new Promise<string[]>();
        Promise<DeployedScene[]> getDeployedScenesPromise = new Promise<DeployedScene[]>();

        theGraph.QueryLands(tld, ethAddress, cacheMaxAgeSecondsLand)
            .Then(landsReceived =>
            {
                lands = landsReceived;

                List<string> parcels = new List<string>();
                for (int i = 0; i < landsReceived.Count; i++)
                {
                    if (landsReceived[i].parcels == null)
                        continue;

                    parcels.AddRange(landsReceived[i].parcels.Select(parcel => $"{parcel.x},{parcel.y}"));
                }

                getOwnedParcelsPromise.Resolve(parcels.ToArray());
            })
            .Catch(err => getOwnedParcelsPromise.Reject(err));

        getOwnedParcelsPromise.Then(parcels =>
            {
                if (parcels.Length > 0)
                {
                    FetchScenes(catalyst, parcels, cacheMaxAgeSecondsScenes)
                        .Then(scenes => getDeployedScenesPromise.Resolve(scenes))
                        .Catch(err => getDeployedScenesPromise.Reject(err));
                }
                else
                {
                    getDeployedScenesPromise.Resolve(new DeployedScene[] { });
                }
            })
            .Catch(err => getDeployedScenesPromise.Reject(err));

        getDeployedScenesPromise.Then(scenes =>
            {
                resultPromise.Resolve(GetLands(lands, scenes));
            })
            .Catch(err => resultPromise.Reject(err));

        return resultPromise;
    }

    private static LandWithAccess[] GetLands(List<Land> lands, DeployedScene[] scenes)
    {
        LandWithAccess[] result = new LandWithAccess[lands.Count];

        for (int i = 0; i < lands.Count; i++)
        {
            result[i] = ProcessLand(lands[i], scenes);
        }

        return result;
    }

    private static LandWithAccess ProcessLand(Land land, DeployedScene[] scenes)
    {
        List<DeployedScene> scenesInLand = new List<DeployedScene>();

        LandWithAccess result = new LandWithAccess(land);
        for (int i = 0; i < result.parcels.Length; i++)
        {
            DeployedScene sceneInParcel = scenes.FirstOrDefault(scene => scene.parcels.Contains(result.parcels[i]) && !scenesInLand.Contains(scene));
            if (sceneInParcel != null)
            {
                sceneInParcel.sceneLand = result;
                scenesInLand.Add(sceneInParcel);
            }
        }

        result.scenes = scenesInLand;

        return result;
    }
}