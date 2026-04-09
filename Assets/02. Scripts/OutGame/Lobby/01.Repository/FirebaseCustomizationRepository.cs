using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;
#if !UNITY_WEBGL || UNITY_EDITOR
using Firebase.Firestore;
#endif

public static class FirebaseCustomizationRepository
{
    private const string CollectionName = "Customization";

#if !UNITY_WEBGL || UNITY_EDITOR
    /// <summary>현재 로그인된 유저의 커스터마이징 데이터를 Firestore에 저장합니다.</summary>
    public static async Task Save(CustomizationSaveData data)
    {
        try
        {
            string userId = FirebaseAuthRepository.GetCurrentUserEmail();
            if (string.IsNullOrEmpty(userId))
            {
                Debug.LogWarning("[FirebaseCustomization] 로그인되지 않아 저장할 수 없습니다.");
                return;
            }

            await FirebaseInitializer.Instance.DB
                .Collection(CollectionName)
                .Document(userId)
                .SetAsync(data);

            Debug.Log("[FirebaseCustomization] 커스터마이징 저장 성공");
        }
        catch (Exception e)
        {
            Debug.LogError($"[FirebaseCustomization] 저장 실패: {e.Message}");
        }
    }

    /// <summary>특정 파츠 하나만 Firestore에 업데이트합니다.</summary>
    public static async Task SavePart(CharacterCustomizationPart part, int index)
    {
        try
        {
            string userId = FirebaseAuthRepository.GetCurrentUserEmail();
            if (string.IsNullOrEmpty(userId))
                return;

            string fieldPath = $"Parts.{part}";

            await FirebaseInitializer.Instance.DB
                .Collection(CollectionName)
                .Document(userId)
                .UpdateAsync(fieldPath, index);

            Debug.Log($"[FirebaseCustomization] 파츠 저장: {part} = {index}");
        }
        catch (Exception e)
        {
            // 문서가 아직 없으면 전체 저장으로 폴백
            Debug.LogWarning($"[FirebaseCustomization] 파츠 업데이트 실패, 전체 저장 시도: {e.Message}");
            var data = CustomizationSaveData.CreateDefault();
            data.SetPartIndex(part, index);
            await Save(data);
        }
    }

    /// <summary>닉네임만 Firestore에 업데이트합니다.</summary>
    public static async Task SaveNickname(string nickname)
    {
        try
        {
            string userId = FirebaseAuthRepository.GetCurrentUserEmail();
            if (string.IsNullOrEmpty(userId))
                return;

            await FirebaseInitializer.Instance.DB
                .Collection(CollectionName)
                .Document(userId)
                .SetAsync(
                    new Dictionary<string, object> { { "Nickname", nickname } },
                    SetOptions.MergeAll);

            Debug.Log($"[FirebaseCustomization] 닉네임 저장: {nickname}");
        }
        catch (Exception e)
        {
            Debug.LogError($"[FirebaseCustomization] 닉네임 저장 실패: {e.Message}");
        }
    }

    /// <summary>현재 로그인된 유저의 커스터마이징 데이터를 Firestore에서 로드합니다.</summary>
    public static async Task<CustomizationSaveData> Load()
    {
        try
        {
            string userId = FirebaseAuthRepository.GetCurrentUserEmail();
            if (string.IsNullOrEmpty(userId))
            {
                Debug.LogWarning("[FirebaseCustomization] 로그인되지 않아 기본값 반환");
                return CustomizationSaveData.CreateDefault();
            }

            DocumentSnapshot snapshot = await FirebaseInitializer.Instance.DB
                .Collection(CollectionName)
                .Document(userId)
                .GetSnapshotAsync();

            if (snapshot.Exists)
            {
                var data = snapshot.ConvertTo<CustomizationSaveData>();
                Debug.Log("[FirebaseCustomization] 커스터마이징 로드 성공");
                return data;
            }

            Debug.Log("[FirebaseCustomization] 저장된 데이터 없음, 기본값 반환");
            return CustomizationSaveData.CreateDefault();
        }
        catch (Exception e)
        {
            Debug.LogError($"[FirebaseCustomization] 로드 실패: {e.Message}");
            return CustomizationSaveData.CreateDefault();
        }
    }
#else
    public static Task Save(CustomizationSaveData data)
    {
        Debug.LogWarning("[FirebaseCustomization] WebGL: Firebase 사용 불가");
        return Task.CompletedTask;
    }

    public static Task SavePart(CharacterCustomizationPart part, int index)
    {
        Debug.LogWarning("[FirebaseCustomization] WebGL: Firebase 사용 불가");
        return Task.CompletedTask;
    }

    public static Task SaveNickname(string nickname)
    {
        Debug.LogWarning("[FirebaseCustomization] WebGL: Firebase 사용 불가");
        return Task.CompletedTask;
    }

    public static Task<CustomizationSaveData> Load()
    {
        Debug.LogWarning("[FirebaseCustomization] WebGL: Firebase 사용 불가");
        return Task.FromResult(CustomizationSaveData.CreateDefault());
    }
#endif
}
