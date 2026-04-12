using System;
using UnityEngine;
using Photon.Pun;
using Hashtable = ExitGames.Client.Photon.Hashtable;

public class MapSelectionManager : SingletonPunCallbacks<MapSelectionManager>
{
    public const string SelectedMapKey = "selectedMap";

    [SerializeField] private MapDefinition[] _maps;

    protected override bool PersistAcrossScenes => false;

    public event Action<MapDefinition> OnMapSelected;

    public void SelectRandomMap()
    {
        if (!PhotonNetwork.IsMasterClient) return;
        if (_maps == null || _maps.Length == 0) return;

        int index = UnityEngine.Random.Range(0, _maps.Length);
        var ht = new Hashtable { { SelectedMapKey, _maps[index].MapNumber } };
        PhotonNetwork.CurrentRoom.SetCustomProperties(ht);
    }

    public bool IsMapSelected()
    {
        if (!PhotonNetwork.InRoom || PhotonNetwork.CurrentRoom == null) return false;
        return PhotonNetwork.CurrentRoom.CustomProperties.ContainsKey(SelectedMapKey);
    }

    public void NotifyMapSelected()
    {
        MapDefinition mapDef = GetSelectedMap();
        if (mapDef != null)
            OnMapSelected?.Invoke(mapDef);
    }

    public MapDefinition GetSelectedMap()
    {
        if (!PhotonNetwork.InRoom || PhotonNetwork.CurrentRoom == null) return null;
        if (!PhotonNetwork.CurrentRoom.CustomProperties
                .TryGetValue(SelectedMapKey, out object val)) return null;

        int mapNumber = (int)val;
        return Array.Find(_maps, m => m.MapNumber == mapNumber);
    }

    public string GetSelectedSceneName()
    {
        return GetSelectedMap()?.SceneName;
    }

    public MapDefinition[] GetAllMaps() => _maps;
}
