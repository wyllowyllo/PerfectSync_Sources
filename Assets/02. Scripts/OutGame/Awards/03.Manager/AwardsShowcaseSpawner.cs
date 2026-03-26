using UnityEngine;

public sealed class AwardsShowcaseSpawner
{
    private const string DividedModeChildName = "DividedMode";
    private const string MergedModeChildName = "MergedMode";
    private const string AvatarAChildName = "DividedBodyA";
    private const string AvatarBChildName = "DividedBodyB";
    private const string RubberBandChildName = "RubberBand";

    private readonly Transform _spawnSlotA;
    private readonly Transform _spawnSlotB;
    private readonly string _teamCharacterPrefabName;

    public AwardsShowcaseSpawner(Transform spawnSlotA, Transform spawnSlotB, string teamCharacterPrefabName)
    {
        _spawnSlotA = spawnSlotA;
        _spawnSlotB = spawnSlotB;
        _teamCharacterPrefabName = teamCharacterPrefabName;
    }

    public bool TrySpawnShowcaseTeam()
    {
        if (_spawnSlotA == null || _spawnSlotB == null)
            return false;

        int team = PhotonTeamManager.GetLocalTeamRaw();
        if (team == PhotonTeamManager.TeamNone)
            return false;

        var prefab = Resources.Load<GameObject>(_teamCharacterPrefabName);
        if (prefab == null)
            return false;

        Vector3 center = (_spawnSlotA.position + _spawnSlotB.position) * 0.5f;
        var character = Object.Instantiate(prefab, center, Quaternion.identity);

        DisableAllScripts(character);
        FreezeAllRigidbodies(character);
        ApplySeparatedMode(character);

        return true;
    }

    private static void DisableAllScripts(GameObject root)
    {
        foreach (var mb in root.GetComponentsInChildren<MonoBehaviour>(true))
            mb.enabled = false;
    }

    private static void FreezeAllRigidbodies(GameObject root)
    {
        foreach (var rb in root.GetComponentsInChildren<Rigidbody>(true))
            rb.isKinematic = true;
    }

    private void ApplySeparatedMode(GameObject character)
    {
        var mergedMode = character.transform.Find(MergedModeChildName);
        var dividedMode = character.transform.Find(DividedModeChildName);

        if (mergedMode != null)
            mergedMode.gameObject.SetActive(false);
        if (dividedMode != null)
            dividedMode.gameObject.SetActive(true);

        if (dividedMode == null)
            return;

        var rubberBand = dividedMode.Find(RubberBandChildName);
        if (rubberBand != null)
            rubberBand.gameObject.SetActive(false);

        PositionChild(dividedMode, AvatarAChildName, _spawnSlotA);
        PositionChild(dividedMode, AvatarBChildName, _spawnSlotB);
    }

    private static void PositionChild(Transform parent, string childName, Transform target)
    {
        var child = parent.Find(childName);
        if (child == null || target == null)
            return;

        child.position = target.position;
        child.rotation = target.rotation;
    }
}
