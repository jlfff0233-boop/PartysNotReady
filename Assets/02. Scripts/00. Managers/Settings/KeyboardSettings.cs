using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// 키보드 설정 데이터 - 바인딩 식별자와 키 경로
/// </summary>
[Serializable]
public struct KeyboardBindingData
{
    [SerializeField] string _id;		//Input Action 바인딩 ID
    [SerializeField] string _path;		//키 경로

    /// <summary>
    /// 바인딩 ID
    /// </summary>
    public string Id => _id;
    /// <summary>
    /// 키 경로
    /// </summary>
    public string Path => _path;


    /// <summary>
    /// 바인딩 ID와 키 경로 설정
    /// </summary>
    /// <param name="id">바인딩 ID</param>
    /// <param name="path">키 경로</param>
    public KeyboardBindingData ( string id, string path )
    {
        _id = id;
        _path = path;
    }
}

/// <summary>
/// 키보드 설정 - 바인딩 검사, 적용과 원래 설정 복원
/// </summary>
public class KeyboardSettings
{
    InputActionMap _map;		//설정을 적용할 액션 맵
    string [ ] _originalPaths;		//초기 연결 시점의 재지정 경로

    /// <summary>
    /// 키보드 바인딩 인덱스 목록
    /// </summary>
    List<int> _indices = new( );

    /// <summary>
    /// 키보드에서 사용할 수 있는 개별 키 경로
    /// </summary>
    HashSet<string> _keyPaths = new( StringComparer.OrdinalIgnoreCase );


    /// <summary>
    /// 키보드 바인딩 위치와 초기 설정 보관
    /// </summary>
    /// <param name="map">설정을 적용할 액션 맵</param>
    public KeyboardSettings ( InputActionMap map )
    {
        _map = map;

        //기기 연결 여부와 무관하게 유효한 키 경로 준비
        var layout = InputSystem.LoadLayout( "Keyboard" );

        foreach ( var control in layout.controls )
        {
            if ( control.layout.ToString( ) == "Key" )
                _keyPaths.Add( $"<Keyboard>/{control.name}" );
        }

        for ( int i = 0; i < _map.bindings.Count; i++ )
        {
            InputBinding binding = _map.bindings [ i ];

            //이동 Composite의 각 방향 키도 개별 바인딩으로 등록
            if ( binding.isComposite == false &&
                _keyPaths.Contains( binding.path ) == true )
            {
                _indices.Add( i );
            }
        }

        _originalPaths = new string [ _indices.Count ];

        for ( int i = 0; i < _indices.Count; i++ )
            _originalPaths [ i ] = _map.bindings [ _indices [ i ] ].overridePath;
    }

    /// <summary>
    /// 바인딩 ID에 해당하는 설정 위치 조회
    /// </summary>
    /// <param name="id">바인딩 ID</param>
    /// <returns>설정 위치, 없으면 -1</returns>
    int FindIndex ( string id )
    {
        for ( int i = 0; i < _indices.Count; i++ )
        {
            string bindingId =
                _map.bindings [ _indices [ i ] ].id.ToString( );

            if ( string.Equals(
                bindingId, id, StringComparison.OrdinalIgnoreCase ) == true )
            {
                return i;
            }
        }

        return -1;
    }

    /// <summary>
    /// 현재 키보드 설정 복사
    /// </summary>
    /// <returns>바인딩별 현재 키 경로</returns>
    public KeyboardBindingData [ ] GetBindings ()
    {
        KeyboardBindingData [ ] data =
            new KeyboardBindingData [ _indices.Count ];

        for ( int i = 0; i < _indices.Count; i++ )
        {
            InputBinding binding = _map.bindings [ _indices [ i ] ];

            data [ i ] = new KeyboardBindingData(
                binding.id.ToString( ), binding.effectivePath );
        }

        return data;
    }

    /// <summary>
    /// 바인딩의 행동과 방향 이름 조회
    /// </summary>
    /// <param name="id">바인딩 ID</param>
    /// <returns>행동과 방향 이름, 없으면 빈 문자열</returns>
    public string GetBindingName ( string id )
    {
        int index = FindIndex( id );

        if ( index < 0 )
            return string.Empty;

        InputBinding binding = _map.bindings [ _indices [ index ] ];
        InputAction action = _map.FindAction( binding.action );

        return binding.isPartOfComposite == true
            ? $"{action.name} / {binding.name}"
            : action.name;
    }

    /// <summary>
    /// 이동 방향의 첫 키보드 바인딩 ID 조회
    /// </summary>
    /// <param name="direction">이동 Composite 방향 이름</param>
    /// <returns>바인딩 ID, 없으면 빈 문자열</returns>
    public string GetMoveBindingId ( string direction )
    {
        InputAction move = _map.FindAction( "Move", true );		//이동 액션

        foreach ( int index in _indices )
        {
            InputBinding binding = _map.bindings [ index ];		//키보드 바인딩

            if ( binding.isPartOfComposite == true && binding.action == move.name &&
                 string.Equals( binding.name, direction, StringComparison.OrdinalIgnoreCase ) == true )
                return binding.id.ToString( );
        }

        return string.Empty;
    }

    /// <summary>
    /// 적용된 바인딩의 키 표시 이름 조회
    /// </summary>
    /// <param name="id">바인딩 ID</param>
    /// <returns>키 표시 이름, 없으면 빈 문자열</returns>
    public string GetKeyName ( string id )
    {
        int index = FindIndex( id );		//키보드 바인딩 위치

        if ( index < 0 )
            return string.Empty;

        return InputControlPath.ToHumanReadableString(
            _map.bindings [ _indices [ index ] ].effectivePath,
            InputControlPath.HumanReadableStringOptions.OmitDevice );
    }

    /// <summary>
    /// 기본 설정과 새 설정을 합쳐 적용 가능 여부 확인
    /// </summary>
    /// <param name="data">적용할 키 설정</param>
    /// <param name="paths">검사를 마친 재지정 경로</param>
    /// <param name="reason">실패 사유</param>
    /// <returns>적용 준비 성공 여부</returns>
    public bool TryPrepare (
        IReadOnlyList<KeyboardBindingData> data,
        out string [ ] paths,
        out string reason )
    {
        //null 경로는 원본 바인딩 사용을 의미
        paths = new string [ _indices.Count ];
        reason = string.Empty;

        HashSet<int> assigned = new( );

        foreach ( KeyboardBindingData item in data )
        {
            int index = FindIndex( item.Id );

            if ( index < 0 )
            {
                reason = $"존재하지 않는 키 바인딩입니다: {item.Id}";
                return false;
            }

            if ( assigned.Add( index ) == false )
            {
                reason = $"키 바인딩이 중복 등록되었습니다: {item.Id}";
                return false;
            }

            if ( _keyPaths.Contains( item.Path ) == false )
            {
                reason = $"사용할 수 없는 키 경로입니다: {item.Path}";
                return false;
            }

            string original = _map.bindings [ _indices [ index ] ].path;

            paths [ index ] = string.Equals(
                original, item.Path, StringComparison.OrdinalIgnoreCase )
                ? null
                : item.Path;
        }

        //전체 결과를 검사하므로 두 키를 서로 교환하는 설정도 처리
        for ( int i = 0; i < _indices.Count; i++ )
        {
            string firstDefault = _map.bindings [ _indices [ i ] ].path;
            string first = paths [ i ] ?? firstDefault;

            for ( int j = i + 1; j < _indices.Count; j++ )
            {
                string secondDefault = _map.bindings [ _indices [ j ] ].path;
                string second = paths [ j ] ?? secondDefault;

                if ( string.Equals(
                    first, second, StringComparison.OrdinalIgnoreCase ) == false )
                {
                    continue;
                }

                //프로젝트 원본에 이미 존재하는 기본 중복은 유지
                if ( paths [ i ] == null && paths [ j ] == null )
                    continue;

                string firstName = GetBindingName(
                    _map.bindings [ _indices [ i ] ].id.ToString( ) );

                string secondName = GetBindingName(
                    _map.bindings [ _indices [ j ] ].id.ToString( ) );

                reason = $"{firstName} / {secondName}: 같은 키를 사용할 수 없습니다.";
                return false;
            }
        }

        return true;
    }

    /// <summary>
    /// 재지정 경로 적용과 기존 액션 활성 상태 복원
    /// </summary>
    /// <param name="paths">바인딩별 재지정 경로</param>
    void SetPaths ( string [ ] paths )
    {
        bool changed = false;		//실제 바인딩 변경 여부

        for ( int i = 0; i < _indices.Count; i++ )
        {
            string current = _map.bindings [ _indices [ i ] ].overridePath;

            if ( string.Equals(
                current, paths [ i ], StringComparison.OrdinalIgnoreCase ) == false )
            {
                changed = true;
                break;
            }
        }

        if ( changed == false )
            return;

        List<InputAction> enabled = new( );

        foreach ( InputAction action in _map.actions )
        {
            if ( action.enabled == true )
                enabled.Add( action );
        }

        try
        {
            //여러 바인딩을 변경하는 동안 입력 처리 중지
            _map.Disable( );

            for ( int i = 0; i < _indices.Count; i++ )
            {
                int index = _indices [ i ];
                InputBinding binding = _map.bindings [ index ];

                //기존 Hold 등 Interaction과 Processor 설정은 유지
                binding.overridePath = paths [ i ];
                _map.ApplyBindingOverride( index, binding );
            }
        }
        finally
        {
            //원래 켜져 있던 액션만 다시 활성화
            foreach ( InputAction action in enabled )
                action.Enable( );
        }
    }

    /// <summary>
    /// 검사를 마친 키 설정 적용
    /// </summary>
    /// <param name="paths">적용 준비를 마친 재지정 경로</param>
    public void Apply ( string [ ] paths )
    {
        SetPaths( paths );
    }

    /// <summary>
    /// 초기 연결 시점의 키 설정 복원
    /// </summary>
    public void Restore ()
    {
        SetPaths( _originalPaths );
    }
}
