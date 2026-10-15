using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// 사용자가 직접 모델과 생성 설정을 선택하는 전용 창입니다.
/// </summary>
public static partial class PrefabGenerator
{
    /// <summary>
    /// 대상 선택부터 미리보기와 생성 결과까지 제공하는 편집기 창입니다.
    /// </summary>
    public sealed class GeneratorWindow : EditorWindow
    {
        [SerializeField] List<BatchItem> _items = new List<BatchItem>();
        [SerializeField] string _output = "Assets/Prefabs/Generated";
        [SerializeField] int _focused = -1;
        Vector2 _scroll;
        GenerationSession _session;
        [SerializeField] BatchReport _report;
        PreviewRenderUtility _preview;
        Material _flatMaterial;
        GameObject _previewRoot;
        Scene _previewScene;
        bool _hasPreviewScene;
        bool _flat = true;
        int _view;
        Shape _bulkShape;
        float _bulkYaw;
        [SerializeField] string _message;
        string _previewError;

        /// <summary>
        /// 창 종료나 스크립트 리로드 때 임시 자원을 정리합니다.
        /// </summary>
        void OnDisable()
        {
            EditorApplication.update -= Tick;
            if (_session != null)
            {
                _session.Cancel();
                FinishSession();
            }

            ReleasePreview();
            _preview?.Cleanup();
            _preview = null;
            if (_flatMaterial != null)
            {
                UnityEngine.Object.DestroyImmediate(_flatMaterial);
            }
        }

        /// <summary>
        /// 에디터 갱신에 모델별 실행을 연결합니다.
        /// </summary>
        void OnEnable()
        {
            EditorApplication.update -= Tick;
            EditorApplication.update += Tick;
            if (_items.Count == 0 && _output == _outputFolder &&
                !AssetDatabase.IsValidFolder(_outputFolder) && AssetDatabase.IsValidFolder("Assets/Prefabs/Props"))
            {
                _output = "Assets/Prefabs/Props";
            }
        }

        /// <summary>
        /// 대상 목록과 설정 및 실행 결과 화면을 그립니다.
        /// </summary>
        void OnGUI()
        {
            bool running = _session != null;
            using (new EditorGUI.DisabledScope(running))
            {
                EditorGUILayout.BeginHorizontal();
                if (GUILayout.Button("선택 모델 추가"))
                {
                    AddSelection(false);
                }
                if (GUILayout.Button("선택 폴더에서 찾기"))
                {
                    AddSelection(true);
                }
                if (GUILayout.Button("목록 불러오기"))
                {
                    ProfileFile(false);
                }
                if (GUILayout.Button("목록 저장"))
                {
                    ProfileFile(true);
                }
                EditorGUILayout.EndHorizontal();
                EditorGUILayout.BeginHorizontal();
                _output = EditorGUILayout.TextField("출력 폴더 (Assets/…)", _output);
                if (GUILayout.Button("폴더 선택", GUILayout.Width(90f)))
                {
                    ChooseOutputFolder();
                }
                EditorGUILayout.EndHorizontal();
                EditorGUILayout.BeginHorizontal();
                if (GUILayout.Button("전체 선택"))
                {
                    foreach (BatchItem item in _items)
                    {
                        item.Selected = true;
                    }
                }
                if (GUILayout.Button("선택 해제"))
                {
                    foreach (BatchItem item in _items)
                    {
                        item.Selected = false;
                    }
                }
                EditorGUILayout.EndHorizontal();
                EditorGUILayout.BeginHorizontal();
                GUILayout.Label("일괄 설정", EditorStyles.boldLabel, GUILayout.Width(75f));
                GUILayout.Label("Collider", GUILayout.Width(55f));
                _bulkShape = (Shape)EditorGUILayout.EnumPopup(_bulkShape, GUILayout.Width(100f));
                GUILayout.Space(12f);
                GUILayout.Label(new GUIContent("Y 회전 (°)", "체크된 모델에 적용할 Y축 회전값입니다."), GUILayout.Width(75f));
                _bulkYaw = EditorGUILayout.FloatField(_bulkYaw, GUILayout.Width(80f));
                GUILayout.FlexibleSpace();
                EditorGUILayout.EndHorizontal();
                EditorGUILayout.BeginHorizontal();
                if (GUILayout.Button("체크 항목에 적용"))
                {
                    foreach (BatchItem item in _items.Where(item => item.Selected))
                    {
                        item.ColliderShape = _bulkShape.ToString();
                        item.Yaw = _bulkYaw;
                        item.OrientationConfirmed = false;
                    }

                    ReleasePreview();
                }
                if (GUILayout.Button("체크 항목 제거"))
                {
                    _items.RemoveAll(item => item.Selected);
                    _focused = -1;
                    ReleasePreview();
                }
                EditorGUILayout.EndHorizontal();
            }

            _scroll = EditorGUILayout.BeginScrollView(_scroll);
            using (new EditorGUI.DisabledScope(running))
            {
                // 제목과 모델 행이 같은 너비를 사용하여 각 설정을 구분합니다.
                EditorGUILayout.BeginHorizontal();
                GUILayout.Label(new GUIContent("✓", "생성 대상 선택"), GUILayout.Width(20f));
                GUILayout.Label("모델 (눌러서 미리보기)", EditorStyles.boldLabel, GUILayout.MinWidth(200f), GUILayout.ExpandWidth(true));
                GUILayout.Label("Collider", EditorStyles.boldLabel, GUILayout.Width(85f));
                GUILayout.Label(new GUIContent("Y 회전 (°)", "모델별 Y축 회전 보정값입니다. 0은 추가 회전 없음입니다."),
                    EditorStyles.boldLabel, GUILayout.Width(80f));
                GUILayout.Label("방향 확인", EditorStyles.boldLabel, GUILayout.Width(90f));
                GUILayout.Label("묶음 / 상태", EditorStyles.boldLabel, GUILayout.Width(110f));
                EditorGUILayout.EndHorizontal();

                for (int index = 0; index < _items.Count; index++)
                {
                    BatchItem item = _items[index];
                    EditorGUILayout.BeginHorizontal();
                    item.Selected = EditorGUILayout.Toggle(item.Selected, GUILayout.Width(20f));
                    if (GUILayout.Button(new GUIContent(Path.GetFileName(item.AssetPath), item.AssetPath),
                        GUILayout.MinWidth(200f), GUILayout.ExpandWidth(true)))
                    {
                        _focused = index;
                        PreparePreview();
                    }
                    bool validShape = Enum.TryParse(item.ColliderShape, out Shape shape) && Enum.IsDefined(typeof(Shape), shape);
                    EditorGUI.BeginChangeCheck();
                    Shape updatedShape = (Shape)EditorGUILayout.EnumPopup(shape, GUILayout.Width(85f));
                    float updatedYaw = EditorGUILayout.FloatField(item.Yaw, GUILayout.Width(80f));
                    if (EditorGUI.EndChangeCheck())
                    {
                        item.ColliderShape = updatedShape.ToString();
                        item.Yaw = updatedYaw;
                        item.OrientationConfirmed = false;
                        if (_focused == index)
                        {
                            PreparePreview();
                        }
                    }
                    item.OrientationConfirmed = EditorGUILayout.ToggleLeft("방향 확인", item.OrientationConfirmed, GUILayout.Width(90f));
                    string stem = Path.GetFileNameWithoutExtension(item.AssetPath);
                    bool variant = _items.Count(other => string.Equals(Path.GetFileNameWithoutExtension(other.AssetPath),
                        stem, StringComparison.OrdinalIgnoreCase)) > 1;
                    GUILayout.Label(!validShape ? "Collider 설정 오류" : variant ? "동명 모델 검토" : item.Pack, GUILayout.Width(110f));
                    EditorGUILayout.EndHorizontal();
                    if (_focused == index)
                    {
                        EditorGUILayout.LabelField(item.AssetPath);
                        item.OutputName = EditorGUILayout.TextField("출력 이름 (비우면 원본명)", item.OutputName);
                        item.Pack = EditorGUILayout.TextField("묶음", item.Pack);
                        item.ReviewReason = EditorGUILayout.TextField("검토 사유 (비워야 생성)", item.ReviewReason);
                    }
                }
            }

            if (!running)
            {
                if (_previewRoot == null && _focused >= 0 && _focused < _items.Count && _previewError == null)
                {
                    PreparePreview();
                }

                DrawPreview();
            }

            if (_report != null)
            {
                foreach (BatchResult result in _report.Items)
                {
                    EditorGUILayout.BeginHorizontal();
                    GUILayout.Label(Path.GetFileName(result.AssetPath) + " / " + result.Status, GUILayout.Width(280f));
                    if (!string.IsNullOrEmpty(result.PrefabPath) && GUILayout.Button("Prefab 선택", GUILayout.Width(90f)))
                    {
                        Selection.activeObject = AssetDatabase.LoadAssetAtPath<GameObject>(result.PrefabPath);
                        EditorGUIUtility.PingObject(Selection.activeObject);
                    }
                    EditorGUILayout.EndHorizontal();
                    EditorGUILayout.SelectableLabel(result.Reason ?? "", GUILayout.Height(32f));
                }
            }
            EditorGUILayout.EndScrollView();
            EditorGUILayout.HelpBox(_message ?? "회전 보정은 Y축만 지원합니다. 의미상 앞면은 미리보기에서 직접 확인해주세요.", MessageType.Info);
            EditorGUILayout.BeginHorizontal();
            using (new EditorGUI.DisabledScope(running))
            {
                if (GUILayout.Button("선택 항목 사전 검사"))
                {
                    StartSession(true, false);
                }
                if (GUILayout.Button("선택 항목 생성"))
                {
                    StartSession(false, false);
                }
                using (new EditorGUI.DisabledScope(_report == null))
                {
                    if (GUILayout.Button("실패·미처리 재실행"))
                    {
                        StartSession(false, true);
                    }
                    if (GUILayout.Button("결과 저장"))
                    {
                        SaveReport();
                    }
                }
            }
            using (new EditorGUI.DisabledScope(!running))
            {
                if (GUILayout.Button("취소"))
                {
                    _session.Cancel();
                }
            }
            EditorGUILayout.EndHorizontal();
        }


        /// <summary>
        /// 전용 도구 창을 엽니다.
        /// </summary>
        [MenuItem("Tools/Prefab Generator/Open")]
        public static void Open()
        {
            GetWindow<GeneratorWindow>("Prefab Generator").minSize = new Vector2(800f, 650f);
        }

        /// <summary>
        /// 미리보기와 실행 상태를 해제합니다.
        /// </summary>
        void ReleasePreview()
        {
            if (_previewRoot != null)
            {
                UnityEngine.Object.DestroyImmediate(_previewRoot);
            }

            _previewRoot = null;
            if (_hasPreviewScene)
            {
                EditorSceneManager.ClosePreviewScene(_previewScene);
                _hasPreviewScene = false;
            }
        }

        /// <summary>
        /// 진행 중 작업을 종료하고 결과를 보관합니다.
        /// </summary>
        void FinishSession()
        {
            if (_session == null)
            {
                return;
            }

            _report = _session.Report;
            _session.Dispose();
            _session = null;
            _message = $"성공 {_report.Success}, 기존 {_report.Existing}, 검사 완료 {_report.Checked}, " +
                $"검토 {_report.Review}, 실패 {_report.Failed}" +
                (_report.Stopped ? " / " + _report.StopReason : "");
        }

        /// <summary>
        /// 에디터 상태를 확인하고 매 갱신에 모델 하나만 처리합니다.
        /// </summary>
        void Tick()
        {
            if (_session == null)
            {
                return;
            }

            if (EditorApplication.isCompiling || EditorApplication.isPlayingOrWillChangePlaymode)
            {
                _session.Cancel();
            }

            if (!_session.Complete)
            {
                _session.ProcessNext();
                _message = $"처리 {_session.Report.Items.Count}/{_session.Report.Total} / " +
                    _session.Report.Items.LastOrDefault()?.AssetPath;
            }

            if (_session.Complete)
            {
                FinishSession();
            }

            Repaint();
        }


        /// <summary>
        /// FBX와 OBJ만 원본 경로 중복 없이 목록에 추가합니다.
        /// </summary>
        void AddPaths(IEnumerable<string> paths)
        {
            int unsupported = 0;
            foreach (string path in paths.Distinct(StringComparer.OrdinalIgnoreCase))
            {
                string extension = Path.GetExtension(path);
                if (!extension.Equals(".fbx", StringComparison.OrdinalIgnoreCase) &&
                    !extension.Equals(".obj", StringComparison.OrdinalIgnoreCase))
                {
                    unsupported++;
                    continue;
                }

                if (_items.Any(item => string.Equals(item.AssetPath, path, StringComparison.OrdinalIgnoreCase)))
                {
                    continue;
                }

                _items.Add(new BatchItem
                {
                    AssetPath = path,
                    AssetGuid = AssetDatabase.AssetPathToGUID(path),
                    Pack = "사용자 선택",
                    Selected = true
                });
            }

            _message = $"목록 {_items.Count}개 / 지원하지 않는 선택 항목 {unsupported}개 제외";
        }

        /// <summary>
        /// Project 창 선택 또는 선택 폴더에서 모델 후보를 가져옵니다.
        /// </summary>
        void AddSelection(bool folders)
        {
            List<string> paths = new List<string>();
            foreach (UnityEngine.Object asset in Selection.objects)
            {
                string path = AssetDatabase.GetAssetPath(asset);
                if (folders && AssetDatabase.IsValidFolder(path))
                {
                    paths.AddRange(AssetDatabase.FindAssets("t:Model", new[] { path })
                        .Select(AssetDatabase.GUIDToAssetPath));
                }
                else if (!folders)
                {
                    paths.Add(path);
                }
            }

            AddPaths(paths);
        }

        /// <summary>
        /// 프로젝트 내부의 출력 폴더를 선택합니다.
        /// </summary>
        void ChooseOutputFolder()
        {
            string selected = EditorUtility.OpenFolderPanel("출력 폴더 선택", Application.dataPath, "");
            if (string.IsNullOrEmpty(selected))
            {
                return;
            }

            string assets = Path.GetFullPath(Application.dataPath).Replace('\\', '/').TrimEnd('/');
            string normalized = Path.GetFullPath(selected).Replace('\\', '/').TrimEnd('/');
            if (!normalized.StartsWith(assets + "/", StringComparison.OrdinalIgnoreCase))
            {
                _message = "Assets 내부의 하위 폴더를 선택해주세요.";
                return;
            }

            _output = "Assets/" + normalized.Substring(assets.Length + 1);
        }

        /// <summary>
        /// 작업 목록을 저장하거나 불러오며 기존 확정 목록도 지원합니다.
        /// </summary>
        void ProfileFile(bool save)
        {
            string path = save
                ? EditorUtility.SaveFilePanel("작업 목록 저장", Application.dataPath, "Prefab-profile", "json")
                : EditorUtility.OpenFilePanel("작업 목록 불러오기", Path.GetDirectoryName(Application.dataPath), "json");
            if (string.IsNullOrEmpty(path))
            {
                return;
            }

            try
            {
                if (save)
                {
                    SaveProfile(path, new BatchManifest { OutputFolder = _output, Items = _items.ToArray() });
                }
                else
                {
                    BatchManifest profile = LoadProfile(path);
                    _items = new List<BatchItem>(profile.Items);
                    _output = profile.OutputFolder;
                    _focused = -1;
                    _report = null;
                    ReleasePreview();
                }

                _message = save ? "작업 목록 저장 완료" : "작업 목록 불러오기 완료";
            }
            catch (Exception exception)
            {
                _message = exception.Message;
            }
        }

        /// <summary>
        /// 체크된 항목만 독립 복사하여 검사 또는 생성 작업을 시작합니다.
        /// </summary>
        void StartSession(bool checkOnly, bool retryOnly)
        {
            try
            {
                if (EditorApplication.isCompiling || EditorApplication.isPlayingOrWillChangePlaymode)
                {
                    throw new InvalidOperationException("편집 모드에서 컴파일이 끝난 뒤 실행해주세요.");
                }

                IEnumerable<BatchItem> selected = _items.Where(item => item.Selected);
                if (retryOnly && _report != null)
                {
                    HashSet<string> retry = new HashSet<string>(_report.Items
                        .Where(result => result.Status == "Failed" || result.Status == "Pending")
                        .Select(result => result.AssetPath), StringComparer.OrdinalIgnoreCase);
                    selected = selected.Where(item => retry.Contains(item.AssetPath));
                }

                BatchItem[] snapshot = selected.Select(item =>
                    JsonUtility.FromJson<BatchItem>(JsonUtility.ToJson(item))).ToArray();
                if (snapshot.Length == 0)
                {
                    throw new InvalidOperationException("실행할 선택 항목이 없습니다.");
                }

                ReleasePreview();
                _session = new GenerationSession(snapshot, _output, checkOnly);
                _report = _session.Report;
                _message = checkOnly ? "사전 검사 중" : "생성 중";
            }
            catch (Exception exception)
            {
                _message = exception.Message;
            }
        }

        /// <summary>
        /// 선택 모델에 기존 생성 규칙을 적용한 임시 미리보기를 준비합니다.
        /// </summary>
        void PreparePreview()
        {
            ReleasePreview();
            _previewError = null;
            try
            {
                _previewScene = EditorSceneManager.NewPreviewScene();
                _hasPreviewScene = true;
                _previewRoot = CreateRoot(GetBatchSpec(_items[_focused]), _previewScene);
                Validate(_previewRoot, GetBatchSpec(_items[_focused]));
            }
            catch (Exception exception)
            {
                _previewError = exception.Message;
                ReleasePreview();
            }
        }

        /// <summary>
        /// 화면에 투영한 선으로 바닥과 방향 및 콜라이더를 표시합니다.
        /// </summary>
        void DrawLine(Rect rect, Vector3 a, Vector3 b, Color color)
        {
            Vector3 pa = _preview.camera.WorldToViewportPoint(a);
            Vector3 pb = _preview.camera.WorldToViewportPoint(b);
            if (pa.z <= 0f || pb.z <= 0f)
            {
                return;
            }

            Handles.color = color;
            Handles.DrawAAPolyLine(2f,
                new Vector3(rect.x + pa.x * rect.width, rect.yMax - pa.y * rect.height),
                new Vector3(rect.x + pb.x * rect.width, rect.yMax - pb.y * rect.height));
        }

        /// <summary>
        /// 기본 콜라이더의 외곽선과 바닥 기준축을 표시합니다.
        /// </summary>
        void DrawGuides(Rect rect, Bounds bounds)
        {
            float length = Mathf.Max(bounds.size.magnitude * 0.35f, 0.001f);
            Handles.BeginGUI();
            Color previousColor = Handles.color;
            DrawLine(rect, Vector3.zero, Vector3.up * length, Color.green);
            DrawLine(rect, Vector3.zero, Vector3.forward * length, Color.cyan);
            Vector3[] floor = { new Vector3(-length, 0f, -length), new Vector3(length, 0f, -length),
                new Vector3(length, 0f, length), new Vector3(-length, 0f, length) };
            for (int index = 0; index < 4; index++)
            {
                DrawLine(rect, floor[index], floor[(index + 1) % 4], Color.gray);
            }

            Collider collider = _previewRoot.GetComponentInChildren<Collider>();
            Transform basis = collider.transform;
            if (collider is BoxCollider box)
            {
                Vector3[] corners = new Vector3[8];
                for (int index = 0; index < 8; index++)
                {
                    corners[index] = basis.TransformPoint(box.center + Vector3.Scale(box.size * 0.5f,
                        new Vector3((index & 1) == 0 ? -1 : 1, (index & 2) == 0 ? -1 : 1, (index & 4) == 0 ? -1 : 1)));
                }

                for (int index = 0; index < 8; index++)
                {
                    foreach (int bit in new[] { 1, 2, 4 })
                    {
                        if ((index & bit) == 0)
                        {
                            DrawLine(rect, corners[index], corners[index | bit], Color.yellow);
                        }
                    }
                }
            }
            else
            {
                bool capsule = collider is CapsuleCollider;
                Vector3 center = capsule ? ((CapsuleCollider)collider).center : ((SphereCollider)collider).center;
                float radius = capsule ? ((CapsuleCollider)collider).radius : ((SphereCollider)collider).radius;
                float half = capsule ? Mathf.Max(0f, ((CapsuleCollider)collider).height * 0.5f - radius) : 0f;
                for (int plane = 0; plane < 3; plane++)
                {
                    for (int index = 0; index < 64; index++)
                    {
                        float a = index * Mathf.PI * 2f / 64f;
                        float b = (index + 1) * Mathf.PI * 2f / 64f;
                        Vector3 pa = plane == 0 ? new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)) :
                            plane == 1 ? new Vector3(Mathf.Cos(a), Mathf.Sin(a), 0f) : new Vector3(0f, Mathf.Sin(a), Mathf.Cos(a));
                        Vector3 pb = plane == 0 ? new Vector3(Mathf.Cos(b), 0f, Mathf.Sin(b)) :
                            plane == 1 ? new Vector3(Mathf.Cos(b), Mathf.Sin(b), 0f) : new Vector3(0f, Mathf.Sin(b), Mathf.Cos(b));
                        if (capsule && plane == 0)
                        {
                            foreach (int sign in new[] { -1, 1 })
                            {
                                DrawLine(rect, basis.TransformPoint(center + pa * radius + Vector3.up * half * sign),
                                    basis.TransformPoint(center + pb * radius + Vector3.up * half * sign), Color.yellow);
                            }
                        }
                        else
                        {
                            DrawLine(rect, basis.TransformPoint(center + pa * radius + Vector3.up * Mathf.Sign(pa.y) * half),
                                basis.TransformPoint(center + pb * radius + Vector3.up * Mathf.Sign(pb.y) * half), Color.yellow);
                        }
                    }
                }
            }

            Handles.color = previousColor;
            Handles.EndGUI();
        }

        /// <summary>
        /// 원본 재질 또는 단색으로 선택 모델을 다섯 방향에서 보여줍니다.
        /// </summary>
        void DrawPreview()
        {
            if (_previewRoot == null)
            {
                EditorGUILayout.HelpBox(_previewError ?? "목록의 모델명을 눌러 미리보기를 선택해주세요.", MessageType.Info);
                return;
            }

            _flat = EditorGUILayout.Toggle("단색 형태 보기", _flat);
            _view = GUILayout.Toolbar(_view, new[] { "앞 (+Z)", "뒤", "좌", "우", "위" });
            Rect rect = GUILayoutUtility.GetRect(100f, 240f, GUILayout.ExpandWidth(true));
            if (Event.current.type != EventType.Repaint)
            {
                return;
            }

            List<Mesh> bakedMeshes = new List<Mesh>();
            try
            {
                if (_preview == null)
                {
                    _preview = new PreviewRenderUtility();
                }

                if (_flatMaterial == null)
                {
                    Shader shader = Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Unlit/Color");
                    if (shader == null)
                    {
                        throw new InvalidOperationException("미리보기용 셰이더를 찾을 수 없습니다.");
                    }

                    _flatMaterial = new Material(shader) { hideFlags = HideFlags.HideAndDontSave };
                    if (_flatMaterial.HasProperty("_BaseColor"))
                    {
                        _flatMaterial.SetColor("_BaseColor", new Color(0.75f, 0.8f, 0.85f));
                    }
                }

                Bounds bounds = GetBounds(_previewRoot, _previewRoot.transform);
                Vector3[] directions = { new Vector3(0.35f, 0.25f, 1f), new Vector3(-0.35f, 0.25f, -1f),
                    Vector3.left, Vector3.right, Vector3.up };
                _preview.BeginPreview(rect, GUIStyle.none);
                Camera camera = _preview.camera;
                camera.orthographic = true;
                camera.orthographicSize = bounds.size.magnitude * 0.65f;
                camera.nearClipPlane = 0.001f;
                camera.farClipPlane = Mathf.Max(10f, bounds.size.magnitude * 10f);
                camera.transform.position = bounds.center + directions[_view] * bounds.size.magnitude * 2f;
                camera.transform.LookAt(bounds.center, _view == 4 ? Vector3.forward : Vector3.up);
                foreach (MeshFilter filter in _previewRoot.GetComponentsInChildren<MeshFilter>())
                {
                    Mesh mesh = filter.sharedMesh;
                    MeshRenderer renderer = filter.GetComponent<MeshRenderer>();
                    if (mesh == null || renderer == null)
                    {
                        continue;
                    }

                    Material[] materials = renderer.sharedMaterials;
                    for (int index = 0; index < mesh.subMeshCount; index++)
                    {
                        Material material = !_flat && index < materials.Length && materials[index] != null
                            ? materials[index] : _flatMaterial;
                        _preview.DrawMesh(mesh, filter.transform.localToWorldMatrix, material, index);
                    }
                }

                foreach (SkinnedMeshRenderer renderer in _previewRoot.GetComponentsInChildren<SkinnedMeshRenderer>())
                {
                    Mesh mesh = new Mesh { hideFlags = HideFlags.HideAndDontSave };
                    bakedMeshes.Add(mesh);
                    renderer.BakeMesh(mesh);
                    Material[] materials = renderer.sharedMaterials;
                    for (int index = 0; index < mesh.subMeshCount; index++)
                    {
                        Material material = !_flat && index < materials.Length && materials[index] != null
                            ? materials[index] : _flatMaterial;
                        _preview.DrawMesh(mesh, renderer.transform.localToWorldMatrix, material, index);
                    }
                }

                _preview.Render(true);
                GUI.DrawTexture(rect, _preview.EndPreview(), ScaleMode.StretchToFill, false);
                DrawGuides(rect, bounds);
                GUI.Label(new Rect(rect.x + 6f, rect.y + 6f, rect.width - 12f, 40f),
                    "초록 +Y / 청록 +Z / 노랑 Collider / 축 시작점 Pivot\n크기: " + bounds.size.ToString("F3"));
            }
            catch (Exception exception)
            {
                _preview?.Cleanup();
                _preview = null;
                _previewError = exception.Message;
                _message = "미리보기 오류: " + exception.Message;
            }
            finally
            {
                foreach (Mesh mesh in bakedMeshes)
                {
                    UnityEngine.Object.DestroyImmediate(mesh);
                }
            }
        }

        /// <summary>
        /// 결과 보고서를 저장합니다.
        /// </summary>
        void SaveReport()
        {
            string path = EditorUtility.SaveFilePanel("결과 저장", Path.GetDirectoryName(Application.dataPath),
                "Prefab-results", "json");
            if (!string.IsNullOrEmpty(path))
            {
                try
                {
                    WriteJson(path, _report);
                }
                catch (Exception exception)
                {
                    _message = exception.Message;
                }
            }
        }

    }
}

