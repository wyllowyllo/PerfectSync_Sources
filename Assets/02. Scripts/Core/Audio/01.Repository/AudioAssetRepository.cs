using System.Threading.Tasks;
using UnityEngine;

public static class AudioAssetRepository
{
    public static Task<AddressableLoadResult<AudioClip>> LoadClipAsync(object key) =>
        AddressableAssetLoader.LoadAssetAsync<AudioClip>(key);
}
