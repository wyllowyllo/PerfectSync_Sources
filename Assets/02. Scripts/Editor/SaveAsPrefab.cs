using UnityEngine;
using UnityEditor;

public class SaveAsPrefab 
{
    [MenuItem("Tools/Save Selection As New Prefab")]
       static void Save()
       {
           GameObject selected = Selection.activeGameObject;
           if (selected == null) return;
   
           string path = EditorUtility.SaveFilePanelInProject(
               "Save Prefab", selected.name, "prefab", "저장 위치 선택");
   
           if (!string.IsNullOrEmpty(path))
           {
               // 선택한 오브젝트를 복제해서 씬 루트로 올림
               GameObject copy = Object.Instantiate(selected);
               copy.name = selected.name;
   
               // 복제본은 프리팹 연결 없는 독립 오브젝트이므로 바로 저장 가능
               PrefabUtility.SaveAsPrefabAsset(copy, path);
               Object.DestroyImmediate(copy);
   
               Debug.Log("저장 완료: " + path);
           }
       }
}
