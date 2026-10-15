using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// 에디터와 명령행에서 같은 생성 규칙을 실행합니다.
/// </summary>
public static partial class PrefabGenerator
{
    /// <summary>
    /// 안전한 출력 폴더를 검사하고 필요한 폴더만 생성합니다.
    /// </summary>
    static void EnsureOutputFolder(string path, bool create = true)
    {
        if (string.IsNullOrWhiteSpace(path) || !path.StartsWith("Assets/", StringComparison.Ordinal) ||
            path.Contains("..") || path.Contains("\\") || path.EndsWith("/", StringComparison.Ordinal) ||
            path.Split('/').Any(segment => string.IsNullOrWhiteSpace(segment) || segment.EndsWith(".", StringComparison.Ordinal) || segment.EndsWith(" ", StringComparison.Ordinal)) ||
            path.IndexOfAny(new[] { ':', '*', '?', '"', '<', '>', '|' }) >= 0)
        {
            throw new InvalidOperationException("Assets 내부의 정상적인 출력 폴더가 필요합니다.");
        }

        if (!create)
        {
            return;
        }

        string parent = "Assets";
        foreach (string segment in path.Substring(7).Split('/'))
        {
            if (string.IsNullOrWhiteSpace(segment))
            {
                throw new InvalidOperationException("빈 출력 폴더 이름입니다.");
            }

            string child = parent + "/" + segment;
            if (!AssetDatabase.IsValidFolder(child))
            {
                if (File.Exists(child) || string.IsNullOrEmpty(AssetDatabase.CreateFolder(parent, segment)))
                {
                    throw new IOException("출력 폴더 생성 실패: " + child);
                }
            }

            parent = child;
        }
    }

    /// <summary>
    /// 프로젝트 내 같은 이름의 프리팹 경로를 실행당 한 번 수집합니다.
    /// </summary>
    static Dictionary<string, List<string>> IndexPrefabs()
    {
        Dictionary<string, List<string>> index = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);
        foreach (string guid in AssetDatabase.FindAssets("t:Prefab", new[] { "Assets" }))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            string name = Path.GetFileNameWithoutExtension(path);
            if (!index.TryGetValue(name, out List<string> paths))
            {
                paths = new List<string>();
                index.Add(name, paths);
            }

            paths.Add(path);
        }

        return index;
    }

    /// <summary>
    /// 설정이 일치하는 기존 프리팹을 검증하고 충돌은 보호합니다.
    /// </summary>
    static string CheckExisting(ModelSpec spec, Dictionary<string, List<string>> index)
    {
        if (!index.TryGetValue(spec.Name, out List<string> paths))
        {
            return null;
        }

        if (paths.Count != 1)
        {
            throw new InvalidOperationException("이름 충돌: 같은 이름의 프리팹이 여러 개입니다.");
        }

        string path = paths[0];
        GameObject loaded = PrefabUtility.LoadPrefabContents(path);
        try
        {
            if (loaded.transform.childCount != 1 || loaded.transform.GetChild(0).name != "Renderer" ||
                loaded.transform.GetChild(0).childCount != 1)
            {
                throw new InvalidOperationException("이름 충돌: 기존 프리팹 구조가 다릅니다.");
            }

            GameObject source = PrefabUtility.GetCorrespondingObjectFromOriginalSource(
                loaded.transform.GetChild(0).GetChild(0).gameObject);
            if (source == null || AssetDatabase.GetAssetPath(source) != spec.AssetPath)
            {
                throw new InvalidOperationException("이름 충돌: 다른 원본의 프리팹입니다.");
            }

            try
            {
                Validate(loaded, spec);
            }
            catch (InvalidOperationException exception)
            {
                throw new InvalidOperationException("기존 설정 불일치: " + exception.Message, exception);
            }
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(loaded);
        }

        return path;
    }

    /// <summary>
    /// 한 모델씩 실행하여 취소와 개별 실패를 지원하는 공통 작업입니다.
    /// </summary>
    sealed class GenerationSession : IDisposable
    {
        readonly BatchItem[] _items;
        readonly string _output;
        readonly bool _checkOnly;
        readonly string _previewDirectory;
        readonly Scene _scene;
        readonly Dictionary<string, List<string>> _prefabs;
        readonly Dictionary<string, PackReport> _packs = new Dictionary<string, PackReport>();
        readonly HashSet<string> _names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        readonly HashSet<string> _sources = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        string _previousFailure;
        int _consecutiveFailures;
        int _next;
        bool _disposed;
        public readonly BatchReport Report = new BatchReport();

        public bool Complete => _next >= _items.Length || Report.Stopped;

        /// <summary>
        /// 실행 설정과 프리팹 검색 목록을 준비합니다.
        /// </summary>
        public GenerationSession(BatchItem[] items, string output, bool checkOnly, string previewDirectory = null)
        {
            EnsureOutputFolder(output, false);
            _items = items ?? throw new ArgumentNullException(nameof(items));
            _output = output;
            _checkOnly = checkOnly;
            _previewDirectory = previewDirectory;
            _prefabs = IndexPrefabs();
            Report.Total = items.Length;
            foreach (BatchItem item in items)
            {
                string packName = string.IsNullOrWhiteSpace(item?.Pack) ? "기타" : item.Pack;
                if (!_packs.TryGetValue(packName, out PackReport pack))
                {
                    pack = new PackReport { Name = packName };
                    _packs.Add(packName, pack);
                    Report.Packs.Add(pack);
                }

                pack.Total++;
            }

            _scene = EditorSceneManager.NewPreviewScene();
        }

        /// <summary>
        /// 남은 대상은 미처리로 보존하고 다음 모델 실행을 막습니다.
        /// </summary>
        public void Cancel()
        {
            Report.Stopped = true;
            Report.StopReason = "사용자 취소 또는 에디터 상태 변경";
        }

        /// <summary>
        /// 정상 처리 뒤 연속 오류 판정을 초기화합니다.
        /// </summary>
        void ResetFailure()
        {
            _previousFailure = null;
            _consecutiveFailures = 0;
        }

        /// <summary>
        /// 모델 하나를 검사하거나 생성하고 결과와 묶음 집계를 갱신합니다.
        /// </summary>
        public void ProcessNext()
        {
            if (Complete)
            {
                return;
            }

            BatchItem item = _items[_next++];
            PackReport pack = _packs[string.IsNullOrWhiteSpace(item?.Pack) ? "기타" : item.Pack];
            BatchResult result = new BatchResult { AssetPath = item?.AssetPath };
            Report.Items.Add(result);
            GameObject root = null;
            string phase = "설정";
            bool validating = false;
            try
            {
                ModelSpec spec = GetBatchSpec(item);
                if (!_sources.Add(spec.AssetPath))
                {
                    result.Status = "Skipped";
                    result.Reason = "동일 원본 중복";
                    Report.Skipped++;
                    pack.Skipped++;
                    ResetFailure();
                    return;
                }

                if (!_names.Add(spec.Name))
                {
                    result.Status = "Conflict";
                    result.Reason = "목록 내 출력 이름 중복: 다른 이름을 지정해주세요.";
                    Report.Review++;
                    pack.Review++;
                    ResetFailure();
                    return;
                }

                phase = "기존 확인";
                string existing = CheckExisting(spec, _prefabs);
                if (existing != null)
                {
                    result.Status = "Existing";
                    result.PrefabPath = existing;
                    result.Reason = "기존 정상 프리팹 보존";
                    Report.Existing++;
                    Report.Skipped++;
                    pack.Skipped++;
                    ResetFailure();
                    return;
                }

                phase = "모델 생성";
                root = CreateRoot(spec, _scene);
                phase = "구조 검증";
                validating = true;
                result.Reason = Validate(root, spec);
                validating = false;
                if (_previewDirectory != null)
                {
                    phase = "미리보기 저장";
                    SaveBatchPreview(root, Path.Combine(_previewDirectory, spec.Name + ".png"));
                    SaveBatchPreview(root, Path.Combine(_previewDirectory, spec.Name + "-back.png"), -1f);
                }

                if (!item.OrientationConfirmed || !string.IsNullOrWhiteSpace(item.ReviewReason))
                {
                    result.Status = "Review";
                    result.Reason = string.IsNullOrWhiteSpace(item.ReviewReason) ? "+Y / +Z 방향 확인 필요" : item.ReviewReason;
                    Report.Review++;
                    pack.Review++;
                    ResetFailure();
                    return;
                }

                if (File.Exists(_output + "/" + spec.Name + ".prefab"))
                {
                    result.Status = "Conflict";
                    result.Reason = "저장 경로의 기존 파일을 보호합니다.";
                    Report.Review++;
                    pack.Review++;
                    ResetFailure();
                    return;
                }

                if (_checkOnly)
                {
                    result.Status = "Ready";
                    Report.Checked++;
                    pack.Checked++;
                    ResetFailure();
                    return;
                }

                phase = "저장";
                EnsureOutputFolder(_output);
                string path = _output + "/" + spec.Name + ".prefab";
                // 실행 중 외부에서 만들어진 파일도 저장 직전 확인하여 보호합니다.
                if (File.Exists(path) || AssetDatabase.LoadAssetAtPath<GameObject>(path) != null)
                {
                    result.Status = "Conflict";
                    result.Reason = "저장 경로 충돌: 기존 파일을 보호합니다.";
                    Report.Review++;
                    pack.Review++;
                    ResetFailure();
                    return;
                }

                // 같은 이름이 새로 생성되었는지 인덱스도 확인합니다.
                if (_prefabs.ContainsKey(spec.Name))
                {
                    throw new InvalidOperationException("이름 충돌: 기존 프리팹을 보호합니다.");
                }

                result.PrefabPath = path;
                PrefabUtility.SaveAsPrefabAsset(root, path, out bool saved);
                if (!saved)
                {
                    throw new IOException("프리팹 저장 실패");
                }

                _prefabs[spec.Name] = new List<string> { path };
                phase = "저장 후 검증";
                validating = true;
                GameObject loaded = PrefabUtility.LoadPrefabContents(path);
                try
                {
                    result.Reason = Validate(loaded, spec);
                }
                finally
                {
                    PrefabUtility.UnloadPrefabContents(loaded);
                }

                validating = false;
                result.Status = "Success";
                Report.Success++;
                pack.Success++;
                ResetFailure();
            }
            catch (Exception exception)
            {
                bool conflict = phase == "기존 확인" && exception is InvalidOperationException;
                result.Status = conflict ? "Conflict" : "Failed";
                result.Reason = phase + ": " + exception.Message;
                if (conflict)
                {
                    Report.Review++;
                    pack.Review++;
                    ResetFailure();
                }
                else
                {
                    Report.Failed++;
                    pack.Failed++;
                    pack.Failures.Add(result.AssetPath + ": " + result.Reason);
                    if (validating)
                    {
                        Report.ValidationFailed++;
                        pack.ValidationFailed++;
                    }

                    // 오류 종류와 단계가 같으면 공통 문제일 수 있으므로 보수적으로 중단합니다.
                    string key = phase + ":" + exception.GetType().FullName;
                    _consecutiveFailures = key == _previousFailure ? _consecutiveFailures + 1 : 1;
                    _previousFailure = key;
                    if (_consecutiveFailures >= 3)
                    {
                        Report.Stopped = true;
                        Report.StopReason = "같은 단계·오류 종류 연속 3회: " + key;
                    }
                }
            }
            finally
            {
                if (root != null)
                {
                    UnityEngine.Object.DestroyImmediate(root);
                }

                if (_next == _items.Length ||
                    (string.IsNullOrWhiteSpace(_items[_next]?.Pack) ? "기타" : _items[_next].Pack) != pack.Name)
                {
                    LogPack(pack);
                }
            }
        }

        /// <summary>
        /// 미처리 항목까지 결과에 남기고 임시 장면을 해제합니다.
        /// </summary>
        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            for (int index = _next; index < _items.Length; index++)
            {
                Report.Items.Add(new BatchResult
                {
                    AssetPath = _items[index]?.AssetPath,
                    Status = "Pending",
                    Reason = Report.StopReason
                });
            }

            foreach (PackReport pack in Report.Packs)
            {
                if (pack.Total > pack.Success + pack.Skipped + pack.Failed + pack.Review + pack.Checked)
                {
                    LogPack(pack);
                }
            }

            EditorSceneManager.ClosePreviewScene(_scene);
        }
    }
}
