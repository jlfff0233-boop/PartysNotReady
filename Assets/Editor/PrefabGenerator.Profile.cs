using System;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

/// <summary>
/// 원본 변경 없이 작업 설정을 저장하고 불러옵니다.
/// </summary>
public static partial class PrefabGenerator
{
    /// <summary>
    /// 설정과 보고서는 JSON 파일에만 저장하도록 제한합니다.
    /// </summary>
    static void WriteJson(string path, object value)
    {
        if (!string.Equals(Path.GetExtension(path), ".json", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("설정과 결과는 .json 파일로만 저장할 수 있습니다.");
        }

        File.WriteAllText(path, JsonUtility.ToJson(value, true), new UTF8Encoding(true));
    }

    /// <summary>
    /// 작업 목록을 읽고 GUID로 이동한 모델을 찾습니다.
    /// </summary>
    static BatchManifest LoadProfile(string path)
    {
        string json = File.ReadAllText(path);
        BatchManifest profile = JsonUtility.FromJson<BatchManifest>(json);
        if (profile?.Items == null)
        {
            throw new InvalidOperationException("Items 모델 목록이 없습니다.");
        }

        bool legacy = json.IndexOf("\"Selected\"", StringComparison.Ordinal) < 0;
        if (string.IsNullOrWhiteSpace(profile.OutputFolder))
        {
            profile.OutputFolder = _outputFolder;
        }

        foreach (BatchItem item in profile.Items)
        {
            if (item == null)
            {
                throw new InvalidOperationException("빈 모델 항목입니다.");
            }

            // 기존 51개 JSON에는 선택 필드가 없으므로 모든 대상을 선택합니다.
            if (legacy)
            {
                item.Selected = true;
            }

            if (!string.IsNullOrWhiteSpace(item.AssetGuid))
            {
                string resolved = AssetDatabase.GUIDToAssetPath(item.AssetGuid);
                if (string.IsNullOrEmpty(resolved))
                {
                    item.Selected = false;
                    item.OrientationConfirmed = false;
                    item.ReviewReason = "GUID에 해당하는 원본을 찾을 수 없습니다.";
                    continue;
                }

                item.AssetPath = resolved;
            }
            else
            {
                item.AssetGuid = AssetDatabase.AssetPathToGUID(item.AssetPath);
            }
        }

        return profile;
    }

    /// <summary>
    /// 현재 작업 설정을 UTF-8 BOM JSON으로 저장합니다.
    /// </summary>
    static void SaveProfile(string path, BatchManifest profile)
    {
        foreach (BatchItem item in profile.Items)
        {
            item.AssetGuid = AssetDatabase.AssetPathToGUID(item.AssetPath);
        }

        WriteJson(path, profile);
    }
}
