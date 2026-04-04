using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;

public static class AddressableAssetLoader
{
    public static async Task<AddressableLoadResult<T>> LoadAssetAsync<T>(object key) where T : UnityEngine.Object
    {
        AsyncOperationHandle<T> handle = Addressables.LoadAssetAsync<T>(key);
        try
        {
            await handle.Task;
            return new AddressableLoadResult<T>(handle.Result, handle);
        }
        catch (Exception)
        {
            if (handle.IsValid())
                Addressables.Release(handle);
            throw;
        }
    }

    public static async Task<AddressableLoadListResult<T>> LoadAssetsAsync<T>(object key) where T : UnityEngine.Object
    {
        var handle = Addressables.LoadAssetsAsync<T>(key, null);
        try
        {
            await handle.Task;
            return new AddressableLoadListResult<T>(new List<T>(handle.Result), handle);
        }
        catch (Exception)
        {
            if (handle.IsValid())
                Addressables.Release(handle);
            throw;
        }
    }

    public static async Task<AddressableLoadResult<GameObject>> InstantiateAsync(object key, Transform parent = null, bool instantiateInWorldSpace = false)
    {
        AsyncOperationHandle<GameObject> handle = Addressables.InstantiateAsync(key, parent, instantiateInWorldSpace);
        try
        {
            await handle.Task;
            return new AddressableLoadResult<GameObject>(handle.Result, handle);
        }
        catch (Exception)
        {
            if (handle.IsValid())
                Addressables.Release(handle);
            throw;
        }
    }

    public static void Release<T>(AsyncOperationHandle<T> handle)
    {
        if (handle.IsValid())
            Addressables.Release(handle);
    }
}
