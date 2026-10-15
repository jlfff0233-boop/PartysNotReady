using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// 기존 생성 규칙을 그대로 사용하는 명령행 실행 진입점입니다.
/// </summary>
public static partial class PrefabGenerator
{
    /// <summary>
    /// 명령행에서 전달하는 모델 목록입니다.
    /// </summary>
    [Serializable]
    public sealed class BatchManifest
    {
        public string OutputFolder = "Assets/Prefabs/Generated";
        public BatchItem[] Items;
    }

    /// <summary>
    /// 모델별 확인 상태와 명시적인 콜라이더 설정입니다.
    /// </summary>
    [Serializable]
    public sealed class BatchItem
    {
        public bool Selected = true;
        public string AssetGuid;
        public string OutputName;
        public string Pack;
        public string AssetPath;
        public string ColliderShape = "Box";
        public float Yaw;
        public bool OrientationConfirmed;
        public string ReviewReason;
    }

    /// <summary>
    /// 각 모델의 생성 또는 검토 결과입니다.
    /// </summary>
    [Serializable]
    public sealed class BatchResult
    {
        public string AssetPath;
        public string PrefabPath;
        public string Status;
        public string Reason;
    }

    /// <summary>
    /// 집계와 모델별 결과를 함께 보관합니다.
    /// </summary>
    [Serializable]
    public sealed class BatchReport
    {
        public int Total;
        public int Existing;
        public int Success;
        public int Failed;
        public int Skipped;
        public int Review;
        public int ValidationFailed;
        public int Checked;
        public bool Stopped;
        public string StopReason;
        public List<PackReport> Packs = new List<PackReport>();
        public List<BatchResult> Items = new List<BatchResult>();
    }

    /// <summary>
    /// 에셋 묶음별 처리 수와 실패 이유를 기록합니다.
    /// </summary>
    [Serializable]
    public sealed class PackReport
    {
        public string Name;
        public int Total;
        public int Success;
        public int Skipped;
        public int Failed;
        public int ValidationFailed;
        public int Checked;
        public int Review;
        public List<string> Failures = new List<string>();
    }

    /// <summary>
    /// 확정된 51개 목록 이외의 원본이나 중복 대상을 차단합니다.
    /// </summary>
    static void CheckApprovedManifest(BatchManifest manifest)
    {
        string path = Path.Combine(Path.GetDirectoryName(Application.dataPath), "work/approved-51-manifest.json");
        BatchManifest approved = JsonUtility.FromJson<BatchManifest>(File.ReadAllText(path));
        if (approved?.Items == null || approved.Items.Length != 51)
        {
            throw new InvalidOperationException("확정된 51개 허용 목록이 필요합니다.");
        }

        Dictionary<string, string> allowed = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (BatchItem item in approved.Items)
        {
            GetBatchSpec(item);
            allowed.Add(item.AssetPath, item.Pack);
        }

        HashSet<string> seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (BatchItem item in manifest.Items)
        {
            GetBatchSpec(item);
            if (!allowed.TryGetValue(item.AssetPath, out string pack) || pack != item.Pack || !seen.Add(item.AssetPath))
            {
                throw new InvalidOperationException("확정 목록 외 모델, 묶음 불일치 또는 중복입니다: " + item.AssetPath);
            }
        }
    }

    /// <summary>
    /// 묶음별 완료 상태와 실패 파일을 Unity 로그에 출력합니다.
    /// </summary>
    static void LogPack(PackReport pack)
    {
        Debug.Log($"[PrefabGenerator][{pack.Name}] Target: {pack.Total}, Success: {pack.Success}, " +
            $"Skipped: {pack.Skipped}, Failed: {pack.Failed}, ValidationFailed: {pack.ValidationFailed}, " +
            $"Review: {pack.Review}, Checked: {pack.Checked}, Pending: {pack.Total - pack.Success - pack.Skipped - pack.Failed - pack.Review - pack.Checked}\n" + string.Join("\n", pack.Failures));
    }

    /// <summary>
    /// 현재 프로젝트를 기준으로 명령행 인수의 파일 경로를 해석합니다.
    /// </summary>
    static string GetBatchArgument(string name, string fallback)
    {
        string[] arguments = Environment.GetCommandLineArgs();
        for (int index = 0; index + 1 < arguments.Length; index++)
        {
            if (arguments[index] == name)
            {
                return Path.GetFullPath(arguments[index + 1]);
            }
        }

        return Path.Combine(Path.GetDirectoryName(Application.dataPath), fallback);
    }

    /// <summary>
    /// 프로젝트 내부의 FBX 또는 OBJ만 명시적인 설정으로 처리합니다.
    /// </summary>
    static ModelSpec GetBatchSpec(BatchItem item)
    {
        if (item == null || string.IsNullOrWhiteSpace(item.AssetPath) ||
            !item.AssetPath.StartsWith("Assets/", StringComparison.Ordinal) ||
            item.AssetPath.Contains("..") || item.AssetPath.Contains("\\"))
        {
            throw new InvalidOperationException("프로젝트 내부의 정상적인 AssetPath가 필요합니다.");
        }

        string extension = Path.GetExtension(item.AssetPath);
        if (!extension.Equals(".fbx", StringComparison.OrdinalIgnoreCase) &&
            !extension.Equals(".obj", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("FBX/OBJ만 처리합니다. GLB는 대상에서 제외합니다.");
        }

        if (!Enum.TryParse(item.ColliderShape, false, out Shape shape) || !Enum.IsDefined(typeof(Shape), shape) ||
            !float.IsFinite(item.Yaw))
        {
            throw new InvalidOperationException("콜라이더 종류 또는 회전값이 잘못되었습니다.");
        }

        string name = string.IsNullOrWhiteSpace(item.OutputName) ? Path.GetFileNameWithoutExtension(item.AssetPath) : item.OutputName;
        if (name.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0 || name.Contains("/") || name.Contains("\\") ||
            name.EndsWith(".", StringComparison.Ordinal) || name.EndsWith(" ", StringComparison.Ordinal))
        {
            throw new InvalidOperationException("정상적인 출력 파일명이 필요합니다.");
        }

        return new ModelSpec(name, shape, item.Yaw, item.AssetPath);
    }

    /// <summary>
    /// 시각적 검토용 이미지를 생성하며 실제 프리팹이나 원본을 수정하지 않습니다.
    /// </summary>
    static void SaveBatchPreview(GameObject root, string outputPath, float zSign = 1f)
    {
        Bounds bounds = GetBounds(root, root.transform);
        PreviewRenderUtility preview = new PreviewRenderUtility();
        Material material = new Material(Shader.Find("Universal Render Pipeline/Unlit"));
        material.SetColor("_BaseColor", new Color(0.75f, 0.8f, 0.85f));
        try
        {
            preview.BeginStaticPreview(new Rect(0, 0, 256, 256));
            preview.camera.orthographic = true;
            preview.camera.orthographicSize = bounds.size.magnitude * 0.6f;
            preview.camera.nearClipPlane = 0.001f;
            preview.camera.farClipPlane = bounds.size.magnitude * 10f;
            preview.camera.transform.position = bounds.center + new Vector3(0.4f, 0.5f, zSign) * bounds.size.magnitude * 2f;
            preview.camera.transform.LookAt(bounds.center);
            foreach (MeshFilter filter in root.GetComponentsInChildren<MeshFilter>())
            {
                Mesh mesh = filter.sharedMesh;
                if (mesh == null)
                {
                    continue;
                }

                for (int submesh = 0; submesh < mesh.subMeshCount; submesh++)
                {
                    preview.DrawMesh(mesh, filter.transform.localToWorldMatrix, material, submesh);
                }
            }

            preview.Render(true);
            Texture2D image = preview.EndStaticPreview();
            try
            {
                File.WriteAllBytes(outputPath, image.EncodeToPNG());
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(image);
            }
        }
        finally
        {
            preview.Cleanup();
            UnityEngine.Object.DestroyImmediate(material);
        }
    }

    /// <summary>
    /// 공통 실행 기능을 명령행 설정에 연결합니다.
    /// </summary>
    static void RunBatch(bool previewOnly, bool approvedOnly = true)
    {
        if (!Application.isBatchMode)
        {
            throw new InvalidOperationException("이 진입점은 -batchmode에서만 실행합니다.");
        }

        string manifestPath = GetBatchArgument("-prefabManifest", "work/model-manifest.json");
        string reportPath = GetBatchArgument("-prefabReport", "work/PrefabGenerator-batch-results.json");
        Directory.CreateDirectory(Path.GetDirectoryName(reportPath));
        BatchReport report = new BatchReport();
        try
        {
            BatchManifest manifest = LoadProfile(manifestPath);
            if (approvedOnly)
            {
                CheckApprovedManifest(manifest);
                // 기존 51개 작업은 출력 이름과 폴더의 변경을 허용하지 않습니다.
                manifest.OutputFolder = _outputFolder;
                foreach (BatchItem item in manifest.Items)
                {
                    item.OutputName = null;
                }
            }

            List<BatchItem> selected = new List<BatchItem>();
            foreach (BatchItem item in manifest.Items)
            {
                if (approvedOnly || item.Selected)
                {
                    selected.Add(item);
                }
            }

            using (GenerationSession session = new GenerationSession(selected.ToArray(),
                manifest.OutputFolder, previewOnly, previewOnly ? Path.GetDirectoryName(reportPath) : null))
            {
                report = session.Report;
                while (!session.Complete)
                {
                    session.ProcessNext();
                }
            }
        }
        catch (Exception exception)
        {
            report.Failed++;
            report.Items.Add(new BatchResult { Status = "Fatal", Reason = exception.ToString() });
            Debug.LogError(exception);
        }
        finally
        {
            WriteJson(reportPath, report);
            Debug.Log($"[PrefabGenerator] Total: {report.Total}, Existing: {report.Existing}, " +
                $"Success: {report.Success}, Failed: {report.Failed}, Review: {report.Review}, Checked: {report.Checked}");
        }

        if (report.Failed > 0 || (!previewOnly && report.Review > 0))
        {
            EditorApplication.Exit(2);
        }
    }

    /// <summary>
    /// 사용자가 저장한 일반 작업 목록을 명령행에서 생성합니다.
    /// </summary>
    public static void GenerateProfileBatch()
    {
        RunBatch(false, false);
    }

    /// <summary>
    /// 사용자가 저장한 일반 작업 목록을 저장 없이 검사합니다.
    /// </summary>
    public static void PreviewProfileBatch()
    {
        RunBatch(true, false);
    }

    /// <summary>
    /// 명령행에서 전체 목록의 미리보기만 생성합니다.
    /// </summary>
    public static void PreviewBatch()
    {
        RunBatch(true);
    }

    /// <summary>
    /// 명령행에서 확인된 모델만 생성하고 나머지는 검토 목록에 남깁니다.
    /// </summary>
    public static void GenerateBatch()
    {
        RunBatch(false);
    }
}


