using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;

public readonly struct AddressableLoadListResult<T> where T : Object
{
    public IList<T> Assets { get; }
    private readonly AsyncOperationHandle _handle;

    public AddressableLoadListResult(IList<T> assets, AsyncOperationHandle handle)
    {
        Assets = assets;
        _handle = handle;
    }

    public void Release()
    {
        if (_handle.IsValid())
            Addressables.Release(_handle);
    }
}
