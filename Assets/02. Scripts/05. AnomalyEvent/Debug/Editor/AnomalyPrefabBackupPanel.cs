using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

/// <summary>
/// 원본 복구 화면 - 작업 묶음 선택과 원본별 복구 미리보기
/// </summary>
public class AnomalyPrefabBackupPanel
{
    /// <summary>
    /// 보관한 작업 묶음
    /// </summary>
    List<AnomalyPrefabBackupBatch> _batches = new( );
    /// <summary>
    /// 현재 묶음에서 선택한 원본 식별자
    /// </summary>
    readonly HashSet<string> _selected = new( );
    /// <summary>
    /// 복구 미리보기 결과
    /// </summary>
    readonly List<string> _results = new( );
    int _index;		//선택한 작업 묶음
    bool _expanded;		//복구 화면 펼침
    Vector2 _scroll;		//복구 대상 목록 스크롤

    /// <summary>
    /// 저장된 작업 묶음 새로고침
    /// </summary>
    public void Reload ()
    {
        _batches = AnomalyPrefabBackupUtility.LoadBatches( );
        _index = 0;
        _selected.Clear( );
        _results.Clear( );
    }

    /// <summary>
    /// 현재 묶음에서 선택한 복구 기록 조회
    /// </summary>
    /// <param name="batch">선택한 작업 묶음</param>
    /// <returns>목록에서 체크한 원본 기록</returns>
    List<AnomalyPrefabBackupEntry> GetSelected ( AnomalyPrefabBackupBatch batch )
    {
        if ( batch.LinkedAssets == true && _selected.Count > 0 )
            return batch.Entries;
        return batch.Entries.FindAll( entry => _selected.Contains( entry.Guid ) == true );
    }

    /// <summary>
    /// 선택 원본 복구 가능 여부 미리보기
    /// </summary>
    /// <param name="batch">선택한 작업 묶음</param>
    void Preview ( AnomalyPrefabBackupBatch batch )
    {
        _results.Clear( );
        int available = 0;
        List<AnomalyPrefabBackupEntry> selected = GetSelected( batch );
        foreach ( AnomalyPrefabBackupEntry entry in selected )
        {
            if ( AnomalyPrefabBackupUtility.CheckRestore( batch, entry, _results ) == true )
                available++;
        }
        _results.Add( $"선택 에셋 {selected.Count}개 / 복구 가능 {available}개" );
    }

    /// <summary>
    /// 작업 묶음과 복구 대상 선택, 결과 표시
    /// </summary>
    public void Draw ()
    {
        _expanded = EditorGUILayout.Foldout( _expanded, "일괄 적용 복구", true );
        if ( _expanded == false )
            return;
        if ( GUILayout.Button( "복구 기록 새로고침" ) )
            Reload( );
        if ( _batches.Count == 0 )
        {
            EditorGUILayout.LabelField( "이 기능으로 적용한 복구 기록이 없습니다." );
            return;
        }
        int next = EditorGUILayout.Popup( "적용 작업", _index,
            _batches.ConvertAll( batch => $"{batch.Time} / 에셋 {batch.Entries.Count}개" ).ToArray( ) );
        if ( next != _index )
        {
            _index = next;
            _selected.Clear( );
            _results.Clear( );
        }
        AnomalyPrefabBackupBatch current = _batches[_index];
        if ( current.LinkedAssets == true )
            EditorGUILayout.HelpBox( "원본과 맵이 연결된 작업입니다. 항목 하나를 선택해도 이번 작업의 에셋 전체를 함께 복구합니다.", MessageType.Info );
        using ( new EditorGUILayout.HorizontalScope( ) )
        {
            if ( GUILayout.Button( "이번 작업 전체 선택" ) )
            {
                foreach ( AnomalyPrefabBackupEntry entry in current.Entries )
                    _selected.Add( entry.Guid );
                _results.Clear( );
            }
            if ( GUILayout.Button( "복구 선택 해제" ) )
            {
                _selected.Clear( );
                _results.Clear( );
            }
        }
        using ( var scroll = new EditorGUILayout.ScrollViewScope(
            _scroll, GUILayout.MinWidth( 0f ), GUILayout.MinHeight( 0f ), GUILayout.MaxHeight( 180f ) ) )
        {
            _scroll = scroll.scrollPosition;
            foreach ( AnomalyPrefabBackupEntry entry in current.Entries )
            {
                bool selected = _selected.Contains( entry.Guid );
                bool checkedNow = EditorGUILayout.ToggleLeft(
                    Path.GetFileNameWithoutExtension( entry.Path ) + ( entry.Restored == true ? " (복구됨)" : "" ), selected );
                if ( checkedNow == selected )
                    continue;
                if ( checkedNow == true ) _selected.Add( entry.Guid ); else _selected.Remove( entry.Guid );
                _results.Clear( );
            }
        }
        EditorGUILayout.HelpBox( "적용 이후 추가 수정되거나 이동한 에셋은 덮어쓰지 않습니다. 연결된 작업은 일부 에셋이 복구 불가하면 묶음 전체를 중단합니다. 복구 패키지는 UserSettings/AnomalyPrefabBackups, 원본 복제본은 Assets/Editor/AnomalyBackups에 보관합니다. 폴더 이동 작업은 복구 대상이 아닙니다.", MessageType.Info );
        using ( new EditorGUI.DisabledScope( _selected.Count == 0 || EditorApplication.isPlayingOrWillChangePlaymode ) )
        {
            if ( GUILayout.Button( "선택 원본 복구 미리보기" ) )
                Preview( current );
            using ( new EditorGUI.DisabledScope( _results.Count == 0 ) )
            {
                if ( GUILayout.Button( "선택 원본 적용 전 상태로 복구" ) )
                {
                    AnomalyPrefabBackupUtility.Restore( current, GetSelected( current ) );
                    _selected.Clear( );
                    _results.Clear( );
                    GUIUtility.ExitGUI( );
                }
            }
        }
        foreach ( string result in _results )
            EditorGUILayout.LabelField( result, EditorStyles.wordWrappedLabel );
        foreach ( string result in AnomalyPrefabBackupUtility.Results )
            EditorGUILayout.LabelField( result, EditorStyles.wordWrappedLabel );
    }
}
