using UnityEngine;

[CreateAssetMenu(fileName = "Map_", menuName = "PerfectSync/Map Definition")]
public class MapDefinition : ScriptableObject
{
    [SerializeField] private string _mapName;
    [SerializeField] private int _mapNumber;
    [SerializeField] private Sprite _thumbnail;

    public string MapName => _mapName;
    public int MapNumber => _mapNumber;
    public Sprite Thumbnail => _thumbnail;
    public string SceneName => $"InGame_{_mapNumber}";
}
