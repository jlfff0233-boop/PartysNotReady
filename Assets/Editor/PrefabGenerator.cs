using System;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// 선택한 모델을 원본 보존 방식으로 테스트 프리팹으로 생성합니다.
/// </summary>
public static partial class PrefabGenerator
{
    const string _sourceFolder = "Assets/RabbitParty_Props/TestModels/Picnic/";
    const string _outputFolder = "Assets/Prefabs/Generated";
    const float _tolerance = 0.0001f;

    /// <summary>
    /// 샘플별 콜라이더와 확인한 방향 보정을 정의합니다.
    /// </summary>
    enum Shape
    {
        Box,
        Sphere,
        Capsule
    }

    /// <summary>
    /// 모델별 명시적인 생성 설정입니다.
    /// </summary>
    readonly struct ModelSpec
    {
        public readonly string Name;
        public readonly Shape ColliderShape;
        public readonly float Yaw;
        public readonly string AssetPath;

        /// <summary>
        /// 원본 파일명, 형태 및 방향 보정값을 저장합니다.
        /// </summary>
        public ModelSpec(string name, Shape colliderShape, float yaw = 0f, string assetPath = null)
        {
            Name = name;
            ColliderShape = colliderShape;
            Yaw = yaw;
            AssetPath = assetPath ?? _sourceFolder + name + ".fbx";
        }
    }

    static readonly ModelSpec[] _samples =
    {
        new ModelSpec("cooler", Shape.Box),
        new ModelSpec("apple", Shape.Sphere),
        new ModelSpec("drink_can", Shape.Capsule),
        new ModelSpec("radio", Shape.Box),
        new ModelSpec("plate_A", Shape.Box)
    };

    /// <summary>
    /// 시각적 렌더러의 경계 모서리를 지정 좌표계로 변환하여 합칩니다.
    /// </summary>
    static Bounds GetBounds(GameObject model, Transform basis)
    {
        bool found = false;
        Bounds result = default;
        foreach (Renderer renderer in model.GetComponentsInChildren<Renderer>(true))
        {
            if (!(renderer is MeshRenderer) && !(renderer is SkinnedMeshRenderer))
            {
                continue;
            }

            Bounds local = renderer.localBounds;
            Matrix4x4 matrix = basis.worldToLocalMatrix * renderer.localToWorldMatrix;
            for (int corner = 0; corner < 8; corner++)
            {
                Vector3 offset = new Vector3(
                    (corner & 1) == 0 ? -local.extents.x : local.extents.x,
                    (corner & 2) == 0 ? -local.extents.y : local.extents.y,
                    (corner & 4) == 0 ? -local.extents.z : local.extents.z);
                Vector3 point = matrix.MultiplyPoint3x4(local.center + offset);
                if (!float.IsFinite(point.x) || !float.IsFinite(point.y) || !float.IsFinite(point.z))
                {
                    throw new InvalidOperationException("유효하지 않은 모델 Bounds입니다.");
                }

                if (!found)
                {
                    result = new Bounds(point, Vector3.zero);
                    found = true;
                }
                else
                {
                    result.Encapsulate(point);
                }
            }
        }

        if (!found || result.size.sqrMagnitude <= 0f)
        {
            throw new InvalidOperationException("시각적 Mesh Bounds를 찾을 수 없습니다.");
        }

        return result;
    }

    /// <summary>
    /// 보정 부모의 로컬 Bounds에 하나의 기본 콜라이더를 맞춥니다.
    /// </summary>
    static void AddCollider(GameObject holder, Bounds bounds, Shape shape)
    {
        switch (shape)
        {
            case Shape.Sphere:
                SphereCollider sphere = holder.AddComponent<SphereCollider>();
                sphere.center = bounds.center;
                sphere.radius = Mathf.Max(bounds.extents.x, bounds.extents.y, bounds.extents.z);
                break;
            case Shape.Capsule:
                CapsuleCollider capsule = holder.AddComponent<CapsuleCollider>();
                capsule.center = bounds.center;
                capsule.direction = 1;
                capsule.radius = Mathf.Max(bounds.extents.x, bounds.extents.z);
                capsule.height = Mathf.Max(bounds.size.y, capsule.radius * 2f);
                break;
            default:
                BoxCollider box = holder.AddComponent<BoxCollider>();
                box.center = bounds.center;
                box.size = bounds.size;
                break;
        }
    }

    /// <summary>
    /// 루트, 계층, 바닥 정렬 및 콜라이더 설정을 검증합니다.
    /// </summary>
    static string Validate(GameObject root, ModelSpec spec)
    {
        Transform transform = root.transform;
        if (transform.localPosition != Vector3.zero ||
            Quaternion.Angle(transform.localRotation, Quaternion.identity) > _tolerance ||
            transform.localScale != Vector3.one)
        {
            throw new InvalidOperationException("Root Transform 규칙 위반입니다.");
        }

        if (transform.childCount != 1 || transform.GetChild(0).name != "Renderer")
        {
            throw new InvalidOperationException("Root/Renderer 계층 규칙 위반입니다.");
        }

        Transform holder = transform.GetChild(0);
        if (holder.childCount != 1 || holder.localScale != Vector3.one ||
            Quaternion.Angle(holder.localRotation, Quaternion.Euler(0f, spec.Yaw, 0f)) > _tolerance)
        {
            throw new InvalidOperationException("모델 계층 또는 보정 Transform 규칙 위반입니다.");
        }

        Bounds groundBounds = GetBounds(holder.gameObject, transform);
        if (Mathf.Abs(groundBounds.min.y) > _tolerance ||
            Mathf.Abs(groundBounds.center.x) > _tolerance ||
            Mathf.Abs(groundBounds.center.z) > _tolerance)
        {
            throw new InvalidOperationException("바닥 중앙 Pivot 정렬 실패입니다.");
        }

        Collider[] colliders = root.GetComponentsInChildren<Collider>(true);
        if (colliders.Length != 1 || colliders[0].gameObject != holder.gameObject)
        {
            throw new InvalidOperationException("Renderer에 콜라이더 하나만 있어야 합니다.");
        }

        Bounds local = GetBounds(holder.gameObject, holder);
        Collider collider = colliders[0];
        Vector3 center;
        Vector3 size;
        if (collider is BoxCollider box && spec.ColliderShape == Shape.Box)
        {
            center = box.center;
            size = box.size;
        }
        else if (collider is SphereCollider sphere && spec.ColliderShape == Shape.Sphere)
        {
            center = sphere.center;
            size = Vector3.one * sphere.radius * 2f;
        }
        else if (collider is CapsuleCollider capsule && spec.ColliderShape == Shape.Capsule && capsule.direction == 1)
        {
            center = capsule.center;
            size = new Vector3(capsule.radius * 2f, capsule.height, capsule.radius * 2f);
        }
        else
        {
            throw new InvalidOperationException("허용된 콜라이더 설정이 아닙니다.");
        }

        if ((center - local.center).sqrMagnitude > _tolerance * _tolerance ||
            size.x + _tolerance < local.size.x || size.y + _tolerance < local.size.y ||
            size.z + _tolerance < local.size.z || size.magnitude > local.size.magnitude * 3f)
        {
            throw new InvalidOperationException("콜라이더 Bounds 검증 실패입니다.");
        }

        return $"{spec.Name}\nCollider: {collider.GetType().Name}\n" +
            $"Forward: +Z (Yaw: {spec.Yaw})\nGround Offset: {groundBounds.min.y:F6}\n" +
            $"Root Transform: OK\nBounds Size: {groundBounds.size}\nResult: Success";
    }

    /// <summary>
    /// 미리보기 장면에만 모델을 만들고 원본 Transform을 유지합니다.
    /// </summary>
    static GameObject CreateRoot(ModelSpec spec, Scene scene)
    {
        GameObject asset = AssetDatabase.LoadAssetAtPath<GameObject>(spec.AssetPath);
        if (asset == null)
        {
            throw new InvalidOperationException("모델 임포트가 완료되지 않았습니다: " + spec.Name);
        }

        GameObject root = new GameObject(spec.Name);
        SceneManager.MoveGameObjectToScene(root, scene);
        try
        {
            GameObject holder = new GameObject("Renderer");
            holder.transform.SetParent(root.transform, false);
            holder.transform.localRotation = Quaternion.Euler(0f, spec.Yaw, 0f);
            GameObject model = (GameObject)PrefabUtility.InstantiatePrefab(asset, scene);
            model.transform.SetParent(holder.transform, false);
            model.name = asset.name;
            if (model.GetComponentsInChildren<Collider>(true).Length != 0)
            {
                throw new InvalidOperationException("원본에 Collider가 있어 자동 생성을 중단합니다.");
            }

            Bounds bounds = GetBounds(model, root.transform);
            holder.transform.localPosition = -new Vector3(bounds.center.x, bounds.min.y, bounds.center.z);
            AddCollider(holder, GetBounds(model, holder.transform), spec.ColliderShape);
            return root;
        }
        catch
        {
            UnityEngine.Object.DestroyImmediate(root);
            throw;
        }
    }

    /// <summary>
    /// 기존 프리팹의 파일명과 충돌하면 생성하지 않습니다.
    /// </summary>
    static bool Exists(string name)
    {
        foreach (string guid in AssetDatabase.FindAssets("t:Prefab", new[] { "Assets" }))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            if (string.Equals(Path.GetFileNameWithoutExtension(path), name, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return File.Exists(_outputFolder + "/" + name + ".prefab");
    }

    /// <summary>
    /// 앞뒤 방향을 확인하기 위한 모델 미리보기를 별도 파일로 저장합니다.
    /// </summary>
    [MenuItem("Tools/Utilities/Test Setup/Preview Five Samples")]
    public static void PreviewFiveSamples()
    {
        Directory.CreateDirectory("work");
        Scene scene = EditorSceneManager.NewPreviewScene();
        try
        {
            foreach (ModelSpec spec in _samples)
            {
                GameObject root = CreateRoot(spec, scene);
                try
                {
                    Bounds bounds = GetBounds(root, root.transform);
                    foreach (int sign in new[] { 1, -1 })
                    {
                        PreviewRenderUtility preview = new PreviewRenderUtility();
                        Material material = new Material(Shader.Find("Universal Render Pipeline/Unlit"));
                        material.SetColor("_BaseColor", new Color(0.75f, 0.8f, 0.85f));
                        try
                        {
                            preview.BeginStaticPreview(new Rect(0, 0, 512, 512));
                            preview.camera.orthographic = true;
                            preview.camera.orthographicSize = bounds.size.magnitude * 0.6f;
                            preview.camera.nearClipPlane = 0.001f;
                            preview.camera.farClipPlane = bounds.size.magnitude * 10f;
                            preview.camera.transform.position = bounds.center +
                                new Vector3(0.35f, 0.35f, sign) * bounds.size.magnitude * 2f;
                            preview.camera.transform.LookAt(bounds.center);
                            foreach (MeshFilter filter in root.GetComponentsInChildren<MeshFilter>())
                            {
                                Mesh mesh = filter.sharedMesh;
                                for (int submesh = 0; submesh < mesh.subMeshCount; submesh++)
                                {
                                    preview.DrawMesh(mesh, filter.transform.localToWorldMatrix, material, submesh);
                                }
                            }

                            preview.Render(true);
                            Texture2D image = preview.EndStaticPreview();
                            File.WriteAllBytes($"work/{spec.Name}_{sign}.png", image.EncodeToPNG());
                            UnityEngine.Object.DestroyImmediate(image);
                        }
                        finally
                        {
                            preview.Cleanup();
                            UnityEngine.Object.DestroyImmediate(material);
                        }
                    }
                }
                finally
                {
                    UnityEngine.Object.DestroyImmediate(root);
                }
            }
        }
        finally
        {
            EditorSceneManager.ClosePreviewScene(scene);
        }
    }
    /// <summary>
    /// 5개 샘플만 생성하고 저장된 프리팹을 다시 열어 검증합니다.
    /// </summary>
    [MenuItem("Tools/Utilities/Test Setup/Generate Five Samples")]
    public static void GenerateFiveSamples()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling)
        {
            Debug.LogError("[PrefabGenerator] 편집 모드에서만 실행해주세요.");
            return;
        }

        Directory.CreateDirectory("work");
        StringBuilder report = new StringBuilder("[PrefabGenerator]\n");
        int success = 0;
        int failed = 0;
        int skipped = 0;
        Scene scene = EditorSceneManager.NewPreviewScene();
        try
        {
            foreach (ModelSpec spec in _samples)
            {
                GameObject root = null;
                try
                {
                    if (Exists(spec.Name))
                    {
                        skipped++;
                        report.AppendLine(spec.Name + ": Skipped (기존 프리팹 보호)");
                        continue;
                    }

                    root = CreateRoot(spec, scene);
                    Validate(root, spec);
                    if (!AssetDatabase.IsValidFolder(_outputFolder))
                    {
                        AssetDatabase.CreateFolder("Assets/Prefabs", "Generated");
                    }

                    string path = _outputFolder + "/" + spec.Name + ".prefab";
                    // 저장 직전에도 기존 파일을 보호합니다.
                    if (File.Exists(path))
                    {
                        skipped++;
                        report.AppendLine(spec.Name + ": Skipped (저장 경로 충돌)");
                        continue;
                    }

                    PrefabUtility.SaveAsPrefabAsset(root, path, out bool saved);
                    if (!saved)
                    {
                        throw new IOException("프리팹 저장 실패입니다.");
                    }

                    GameObject loaded = PrefabUtility.LoadPrefabContents(path);
                    try
                    {
                        string result = Validate(loaded, spec);
                        report.AppendLine(result).AppendLine();
                        Debug.Log("[PrefabGenerator]\n" + result);
                    }
                    finally
                    {
                        PrefabUtility.UnloadPrefabContents(loaded);
                    }

                    success++;
                }
                catch (Exception exception)
                {
                    failed++;
                    report.AppendLine(spec.Name + ": Failed - " + exception.Message);
                    Debug.LogError("[PrefabGenerator] " + spec.Name + ": " + exception);
                }
                finally
                {
                    if (root != null)
                    {
                        UnityEngine.Object.DestroyImmediate(root);
                    }
                }
            }
        }
        finally
        {
            EditorSceneManager.ClosePreviewScene(scene);
            report.AppendLine($"Processed: {_samples.Length}\nSuccess: {success}\nFailed: {failed}\nSkipped: {skipped}");
            File.WriteAllText("work/PrefabGenerator-results.txt", report.ToString(), new UTF8Encoding(true));
            Debug.Log(report.ToString());
        }
    }
}
