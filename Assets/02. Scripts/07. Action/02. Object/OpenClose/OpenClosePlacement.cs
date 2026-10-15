using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 개폐 배치 모듈 - 뚜껑 위치/회전 계산과 적용
/// </summary>
public class OpenClosePlacement : MonoBehaviour
{
    [Header( "----- 배치 설정 -----" )]
    [SerializeField] Transform _lidPivot;		//뚜껑 또는 뚜껑만 포함한 부모
    [SerializeField] LayerMask _floorMask;		//바닥 레이어
    [SerializeField, Range( 5f, 30f )] float _leanAngle = 15f;		//수직 기준 기울기
    [SerializeField, Min( 0f )] float _gap = 0.01f;		//표면 여유 간격
    [SerializeField, Min( 0.1f )] float _floorSearchDist = 5f;		//바닥 탐색 거리

    Vector3 _closedPosition;		//닫힌 로컬 위치
    Quaternion _closedRotation;		//닫힌 로컬 회전

    Vector3 _openPosition;		//계산된 열린 로컬 위치
    Quaternion _openRotation;		//계산된 열린 로컬 회전

    Vector3 _closedRootPosition;		//상자 기준 닫힌 뚜껑 피벗 위치
    Quaternion _closedRootRotation;		//상자 기준 닫힌 뚜껑 회전

    Bounds _bodyBounds;		//상자 로컬 좌표의 본체 범위
    Vector3 [ ] _lidPoints;		//상자 로컬 좌표의 닫힌 뚜껑 모서리
    bool _isInitialized;		//초기화 여부

    /// <summary>
    /// 개폐 배치 가능 여부
    /// </summary>
    public bool IsReady => _isInitialized && _lidPivot != null;

    #region ----- 초기화 -----

    /// <summary>
    /// 닫힌 위치, 회전과 본체 및 뚜껑 크기 저장
    /// </summary>
    /// <returns>초기화 성공 여부</returns>
    public bool Init ()
    {
        if ( _isInitialized == true )
            return IsReady;

        if ( _lidPivot == null || _lidPivot == transform ||
             _lidPivot.IsChildOf( transform ) == false )
        {
            Debug.LogWarning( "상자 자식의 뚜껑을 연결해주세요.", this );
            return false;
        }

        if ( _floorMask.value == 0 )
        {
            Debug.LogWarning( "Floor Mask가 지정되지 않았습니다.", this );
            return false;
        }

        Vector3 [ ] bodyPoints = GetShapePoints( false );
        _lidPoints = GetShapePoints( true );

        if ( bodyPoints.Length == 0 || _lidPoints.Length == 0 )
        {
            Debug.LogWarning( "본체와 뚜껑의 Renderer를 확인해주세요.", this );
            return false;
        }

        _bodyBounds = new Bounds( bodyPoints [ 0 ], Vector3.zero );

        foreach ( Vector3 point in bodyPoints )
            _bodyBounds.Encapsulate( point );		//닫힘 복원은 부모 좌표, 열림 계산은 상자 좌표 사용
        _closedPosition = _lidPivot.localPosition;
        _closedRotation = _lidPivot.localRotation;

        _closedRootPosition =
            transform.InverseTransformPoint( _lidPivot.position );

        _closedRootRotation =
            Quaternion.Inverse( transform.rotation ) * _lidPivot.rotation;

        _isInitialized = true;
        return true;
    }


    /// <summary>
    /// 본체/뚜껑의 경계 꼭짓점 수집
    /// </summary>
    /// <param name="isLid">뚜껑 여부</param>
    /// <returns>상자 로컬 좌표의 모서리 목록</returns>
    Vector3 [ ] GetShapePoints ( bool isLid )
    {
        List<Vector3> points = new( );

        foreach ( Renderer target in GetComponentsInChildren<Renderer>( ) )
        {
            bool belongsToLid =
                target.transform == _lidPivot || target.transform.IsChildOf( _lidPivot );

            //표시하지 않거나 요청한 본체/뚜껑 구분과 다르면 제외
            if ( target.enabled == false || belongsToLid != isLid )
                continue;

            Bounds bounds = target.localBounds;

            for ( int i = 0; i < 8; i++ )
            {
                //각 축의 최소/최대 조합으로 경계 꼭짓점 8개 계산
                Vector3 corner = bounds.center + new Vector3(
                    ( i & 1 ) == 0 ? -bounds.extents.x : bounds.extents.x,
                    ( i & 2 ) == 0 ? -bounds.extents.y : bounds.extents.y,
                    ( i & 4 ) == 0 ? -bounds.extents.z : bounds.extents.z );

                //Renderer마다 다른 로컬 좌표를 상자 기준으로 통일
                Vector3 worldPoint = target.transform.TransformPoint( corner );
                points.Add( transform.InverseTransformPoint( worldPoint ) );
            }
        }

        return points.ToArray( );
    }


    #endregion

    #region ----- 개폐 -----

    /// <summary>
    /// 계산된 열림/닫힘 적용
    /// </summary>
    /// <param name="isOpen">표시할 열림 여부</param>
    public void SetOpen ( bool isOpen )
    {
        if ( IsReady == false )
            return;

        _lidPivot.localPosition =
            isOpen ? _openPosition : _closedPosition;

        _lidPivot.localRotation =
            isOpen ? _openRotation : _closedRotation;
    }

    /// <summary>
    /// 열린 뚜껑 위치와 회전 계산
    /// </summary>
    /// <returns>열린 위치와 회전 계산 성공 여부</returns>
    public bool TryCalculateOpenPose ()
    {
        if ( IsReady == false )
            return false;

        if ( Vector3.Dot( transform.up, Vector3.up ) < 0.999f )
        {
            Debug.LogWarning( "상자가 기울어져 있어 개폐 위치를 계산할 수 없습니다.", this );
            return false;
        }

        Vector3 scale = _lidPivot.lossyScale;

        if ( scale.x <= 0f || scale.y <= 0f || scale.z <= 0f ||
             Mathf.Abs( scale.x - scale.y ) > 0.001f ||
             Mathf.Abs( scale.y - scale.z ) > 0.001f )
        {
            Debug.LogWarning(
                $"뚜껑 회전축의 월드 배율이 균일하지 않습니다: {scale}", this );
            return false;
        }

        Vector3 right = transform.right;
        Vector3 forward = transform.forward;

        //뚜껑 윗부분이 상자 방향으로 기울어지도록 회전
        Quaternion tilt = Quaternion.AngleAxis( -90f + _leanAngle, forward );

        Quaternion worldRotation =
            tilt * transform.rotation * _closedRootRotation;

        Vector3 [ ] offsets = new Vector3 [ _lidPoints.Length ];

        float minRight = float.PositiveInfinity;
        float minUp = float.PositiveInfinity;

        float minForward = float.PositiveInfinity;
        float maxForward = float.NegativeInfinity;

        Vector3 bottomOffset = Vector3.zero;

        //회전된 뚜껑의 범위와 최저점을 구해 본체 옆면과 바닥에 맞춤
        for ( int i = 0; i < _lidPoints.Length; i++ )
        {
            //닫힌 피벗 기준의 모서리에 현재 크기와 열림 회전 적용
            Vector3 offset = tilt * transform.TransformVector( _lidPoints [ i ] - _closedRootPosition );

            offsets [ i ] = offset;

            minRight = Mathf.Min( minRight, Vector3.Dot( offset, right ) );

            float depth = Vector3.Dot( offset, forward );
            minForward = Mathf.Min( minForward, depth );
            maxForward = Mathf.Max( maxForward, depth );

            if ( offset.y < minUp )
            {
                minUp = offset.y;
                bottomOffset = offset;
            }
        }

        Vector3 sideCenter = transform.TransformPoint( new Vector3(
            _bodyBounds.max.x, _bodyBounds.center.y, _bodyBounds.center.z ) );

        Vector3 bodyTop = transform.TransformPoint( new Vector3(
            _bodyBounds.center.x, _bodyBounds.max.y, _bodyBounds.center.z ) );

        //앞뒤 중앙을 맞춘 기준 위치
        Vector3 basePosition = sideCenter - forward * ( ( minForward + maxForward ) * 0.5f );

        //먼저 본체 바깥에서 바닥 높이 확인
        Vector3 pivotPosition = basePosition + right * ( _gap - minRight );

        if ( TryGetFloor(
            pivotPosition + bottomOffset, bodyTop.y, out float floorY ) == false )
            return false;

        //뚜껑의 가장 아래쪽을 바닥에 맞춤
        pivotPosition.y = floorY + _gap - minUp;

        //피벗 기준으로 본체 상단까지의 높이 계산
        float contactHeight = bodyTop.y + _gap - pivotPosition.y;

        if ( TryGetContactSide(
            offsets, right, contactHeight, out float contactSide ) == false )
        {
            Debug.LogWarning(
                "바닥 높이와 상자 위치가 맞지 않아 " +
                "접촉 위치를 계산할 수 없습니다.", this );
            return false;
        }

        //본체 높이 안에 있는 뚜껑 부분을 상자 옆면에 맞춤
        Vector3 contactPosition =
            basePosition + right * ( _gap - contactSide );

        pivotPosition.x = contactPosition.x;
        pivotPosition.z = contactPosition.z;

        //옆 위치 보정 후 실제 뚜껑 밑부분의 바닥을 다시 확인
        if ( TryGetFloor(
            pivotPosition + bottomOffset, bodyTop.y, out float finalFloorY ) == false )
            return false;

        if ( Mathf.Abs( finalFloorY - floorY ) > 0.01f )
        {
            Debug.LogWarning(
                "뚜껑을 놓는 위치에 바닥 높이 차이가 있습니다.", this );
            return false;
        }

        pivotPosition.y = finalFloorY + _gap - minUp;

        //계산한 월드 위치와 회전을 뚜껑 부모의 로컬 좌표로 저장
        _openPosition =
            _lidPivot.parent.InverseTransformPoint( pivotPosition );

        _openRotation =
            Quaternion.Inverse( _lidPivot.parent.rotation ) * worldRotation;

        return true;
    }

    /// <summary>
    /// 본체 높이 안의 뚜껑 접촉 위치 계산
    /// </summary>
    /// <param name="offsets">회전된 뚜껑의 피벗 기준 모서리 위치</param>
    /// <param name="right">상자 오른쪽 방향</param>
    /// <param name="height">피벗 기준 본체 상단 높이</param>
    /// <param name="contactSide">오른쪽 방향으로 측정한 접촉 위치</param>
    /// <returns>접촉 위치 계산 성공 여부</returns>
    bool TryGetContactSide (
        Vector3 [ ] offsets, Vector3 right, float height, out float contactSide )
    {
        contactSide = float.PositiveInfinity;

        //GetShapePoints에서 Renderer마다 저장한 모서리 8개씩 확인
        for ( int start = 0; start < offsets.Length; start += 8 )
        {
            for ( int i = 0; i < 8; i++ )
            {
                Vector3 point = offsets [ start + i ];

                //상자 상단보다 낮은 점은 옆면 접촉 후보
                if ( point.y <= height )
                    contactSide = Mathf.Min( contactSide, Vector3.Dot( point, right ) );

                //모서리 번호의 한 축만 바꾸면 연결된 이웃 점이 됨
                for ( int axis = 1; axis <= 4; axis <<= 1 )
                {
                    int next = i ^ axis;

                    //같은 변을 반대 방향으로 다시 검사하지 않음
                    if ( next <= i )
                        continue;

                    Vector3 other = offsets [ start + next ];

                    //두 점이 상단 높이의 같은 쪽이면 교차하지 않음
                    if ( ( point.y <= height ) == ( other.y <= height ) )
                        continue;

                    //테두리와 본체 상단 높이가 만나는 지점
                    float t = ( height - point.y ) / ( other.y - point.y );
                    Vector3 crossing = Vector3.Lerp( point, other, t );

                    contactSide = Mathf.Min(
                        contactSide, Vector3.Dot( crossing, right ) );
                }
            }
        }

        return float.IsPositiveInfinity( contactSide ) == false;
    }

    /// <summary>
    /// 뚜껑 배치 위치 아래의 수평 바닥 확인
    /// </summary>
    /// <param name="position">바닥을 확인할 월드 위치</param>
    /// <param name="bodyTopY">상자 상단의 월드 높이</param>
    /// <param name="floorY">확인한 바닥 높이</param>
    /// <returns>수평 바닥 확인 여부</returns>
    bool TryGetFloor (
        Vector3 position, float bodyTopY, out float floorY )
    {
        floorY = 0f;

        Vector3 origin = position;
        origin.y = bodyTopY + _gap + 0.1f;

        Debug.DrawRay(
            origin, Vector3.down * _floorSearchDist, Color.yellow, 3f );

        if ( Physics.Raycast(
            origin, Vector3.down, out RaycastHit hit, _floorSearchDist, _floorMask,
            QueryTriggerInteraction.Ignore ) == false )
        {
            Debug.LogWarning(
                $"뚜껑 배치 위치 아래에 바닥이 없습니다." +
                $" 시작점: {origin}, 탐색 거리: {_floorSearchDist}", this );
            return false;
        }

        if ( Vector3.Dot( hit.normal, Vector3.up ) < 0.999f )
        {
            Debug.LogWarning(
                $"뚜껑 배치 위치의 바닥이" +
                $" 기울어져 있습니다: {hit.collider.name}", this );
            return false;
        }

        floorY = hit.point.y;
        return true;
    }

    #endregion

}
