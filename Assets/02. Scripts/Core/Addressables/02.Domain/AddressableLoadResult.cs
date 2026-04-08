using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;

public readonly struct AddressableLoadResult<T> where T : Object
{
    public T Asset { get; }
    public AsyncOperationHandle<T> Handle { get; }

    public AddressableLoadResult(T asset, AsyncOperationHandle<T> handle)
    {
        Asset = asset;
        Handle = handle;
    }

    public void Release()
    {
        if (Handle.IsValid())
            Addressables.Release(Handle);
    }
}
