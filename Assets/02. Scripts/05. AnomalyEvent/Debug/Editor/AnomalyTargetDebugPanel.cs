using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

/// <summary>
/// 등록 대상 진단 화면 - 기능 구성과 현재 상태 조회, 검색과 필터
/// </summary>
public class AnomalyTargetDebugPanel
{
    /// <summary>
    /// 주기적으로 복사하는 대상 표시 정보
    /// </summary>
    sealed class Row
    {
        public AnomalyTarget Target;     //찾기와 선택에 사용할 대상
        public string Name;     //검색할 대상 이름
        public string Features;     //붙어 있는 기능과 활성 상태
        public string State;     //잠금, 보유와 정상 기준 정보
        public bool Locked;     //정상 기준 잠금 필터
        public bool Held;     //보유 중 필터
        public bool Pending;     //정상 기준 갱신 대기 필터
        public bool MissingNormal;     //정상 기준 없음 필터
        public bool HasFeature;     //선택 사건의 활성 기능 존재 여부
    }

    readonly List<Row> _rows = new( );     //마지막 갱신에서 수집한 대상 정보
    readonly List<string> _features = new( );     //대상 하나의 기능 설명 구성용 목록
    readonly string[] _filters = { "전체", "잠금", "보유", "기준 갱신 대기", "정상 기준 없음", "선택 사건 기능 없음" };
    string _search = string.Empty;
    int _filter;
    bool _expanded;


    #region ----- 조회 -----

    /// <summary>
    /// 사건에 대응하는 일반 기능의 컴포넌트 형식 조회
    /// </summary>
    /// <param name="type">조회할 사건 종류</param>
    Type GetFeatureType ( AnomalyEventType type )
    {
        return type switch
        {
            AnomalyEventType.Drop => typeof( ItemCarry ),
            AnomalyEventType.Displace => typeof( ItemCarry ),
            AnomalyEventType.Swap => typeof( ItemCarry ),
            AnomalyEventType.OpenClose => typeof( OpenCloseController ),
            AnomalyEventType.Rotate => typeof( RotatePresenter ),
            AnomalyEventType.Shake => typeof( ShakePresenter ),
            AnomalyEventType.Add => typeof( PresenceController ),
            AnomalyEventType.Remove => typeof( PresenceController ),
            AnomalyEventType.Replace => typeof( ReplaceController ),
            _ => null
        };
    }

    /// <summary>
    /// 붙어 있는 기능의 이름과 활성 여부 추가
    /// </summary>
    /// <typeparam name="T">조회할 기능 컴포넌트</typeparam>
    /// <param name="target">조회할 대상</param>
    /// <param name="name">표시할 기능 이름</param>
    void AddFeature<T> ( AnomalyTarget target, string name ) where T : Behaviour
    {
        T feature = target.GetComponent<T>( );
        if ( feature != null )
            _features.Add( feature.isActiveAndEnabled ? name : name + "(비활성)" );
    }

    /// <summary>
    /// 등록 대상의 현재 상태를 표시용으로 복사
    /// </summary>
    /// <param name="type">현재 인스펙터의 사건 종류</param>
    public void Refresh ( AnomalyEventType type )
    {
        _rows.Clear( );
        if ( _expanded == false )
            return;

        Type featureType = GetFeatureType( type );
        foreach ( AnomalyTarget target in AnomalyRegister.Targets )
        {
            if ( target == null )
                continue;

            _features.Clear( );
            AddFeature<ItemCarry>( target, "운반" );
            AddFeature<OpenCloseController>( target, "개폐" );
            AddFeature<RotatePresenter>( target, "회전" );
            AddFeature<ShakePresenter>( target, "흔들림" );
            AddFeature<PresenceController>( target, "존재" );
            AddFeature<ReplaceController>( target, "대체" );
            Behaviour feature = featureType != null ? target.GetComponent( featureType ) as Behaviour : null;
            PlacementTarget placement = target.NormalState?.PlacementTarget;
            Row row = new( );
            row.Target = target;
            row.Name = target.name;
            row.Features = _features.Count > 0 ? string.Join( " / ", _features ) : "관련 기능 없음";
            row.Locked = target.NormalLocked;
            row.Held = target.IsHeld;
            row.Pending = target.NormalRefreshPending;
            row.MissingNormal = target.NormalState == null;
            //기능 존재 여부만 조회하며 공간과 후보 조건을 포함한 발생 가능 판정은 하지 않음
            row.HasFeature = feature != null && feature.isActiveAndEnabled;
            row.State = $"씬: {target.gameObject.scene.name}\n잠금: {row.Locked} / 보유: {row.Held}" +
                $"\n정상 기준: {( row.MissingNormal ? "없음" : "있음" )} / 정상 배치: {( placement != null ? placement.name : "없음" )}" +
                $"\n기준 갱신: {( row.Pending ? target.NormalRefreshReason : "대기 없음" )}" +
                $"\n선택 사건 기능: {( row.HasFeature ? "활성 컴포넌트 있음" : "없음 또는 비활성" )}";
            _rows.Add( row );
        }

        _rows.Sort( ( a, b ) => string.Compare( a.Name, b.Name, StringComparison.OrdinalIgnoreCase ) );
    }

    /// <summary>
    /// 플레이 전환 시 이전 대상 참조와 필터 해제
    /// </summary>
    public void Clear ()
    {
        _rows.Clear( );
        _features.Clear( );
        _search = string.Empty;
        _filter = 0;
    }

    #endregion

    #region ----- 화면 -----

    /// <summary>
    /// 등록 대상 조회와 검색, 상태 필터 표시
    /// </summary>
    /// <param name="presenter">설정과 사건 종류를 조회할 관리자</param>
    public void Draw ( AnomalyEventSystem presenter )
    {
        EditorGUILayout.Space( );
        bool expanded = EditorGUILayout.Foldout( _expanded, "등록 대상 조회 / 발생 진단", true );
        if ( expanded != _expanded )
        {
            _expanded = expanded;
            Refresh( presenter.EventType );
        }
        if ( _expanded == false )
            return;

        EditorGUILayout.HelpBox( "기능 구성과 현재 상태만 조회합니다. 공간, 후보 데이터와 교체 상대 조건은 실제 발생 요청에서 검사합니다. 기능이 있다는 표시가 발생 가능을 보장하지는 않습니다.", MessageType.Info );
        if ( presenter.CheckDebugSettings( out string reason ) == false )
            EditorGUILayout.HelpBox( reason, MessageType.Warning );
        if ( presenter.isActiveAndEnabled == false )
            EditorGUILayout.HelpBox( "사건 관리자가 비활성 상태입니다.", MessageType.Warning );

        _search = EditorGUILayout.TextField( "이름 검색", _search );
        _filter = EditorGUILayout.Popup( "상태 필터", _filter, _filters );
        int shown = 0;
        foreach ( Row row in _rows )
        {
            if ( Matches( row ) == false )
                continue;
            shown++;
            using ( new EditorGUILayout.VerticalScope( EditorStyles.helpBox ) )
            {
                using ( new EditorGUI.DisabledScope( true ) )
                    EditorGUILayout.ObjectField( row.Target, typeof( AnomalyTarget ), true );
                EditorGUILayout.LabelField( row.Features, EditorStyles.wordWrappedLabel );
                EditorGUILayout.LabelField( row.State, EditorStyles.wordWrappedLabel );
                using ( new EditorGUILayout.HorizontalScope( ) )
                {
                    if ( GUILayout.Button( "찾기" ) )
                        EditorGUIUtility.PingObject( row.Target );
                    if ( GUILayout.Button( "선택" ) )
                        Selection.activeObject = row.Target;
                }
            }
        }
        EditorGUILayout.LabelField( $"표시 {shown} / 등록 {_rows.Count}" );
    }

    #endregion


    /// <summary>
    /// 검색 이름과 선택한 상태 필터 일치 확인
    /// </summary>
    /// <param name="row">검사할 표시 정보</param>
    bool Matches ( Row row )
    {
        if ( row.Target == null || row.Name.IndexOf( _search, StringComparison.OrdinalIgnoreCase ) < 0 )
            return false;
        return _filter switch
        {
            1 => row.Locked,
            2 => row.Held,
            3 => row.Pending,
            4 => row.MissingNormal,
            5 => row.HasFeature == false,
            _ => true
        };
    }


}
