using UnityEngine;

/// <summary>
/// 투척 대상 인터페이스 - 시작 위치, 검사 반경과 투척 요청 제공
/// </summary>
public interface IThrowable
{
    /// <summary>
    /// 투척 궤적을 시작할 월드 위치
    /// </summary>
    Vector3 ThrowPosition { get; }

    /// <summary>
    /// 궤적 충돌 검사용 반경, 월드 단위
    /// </summary>
    float ThrowRadius { get; }

    /// <summary>
    /// 아이템 투척
    /// </summary>
    /// <param name="velocity">월드 방향의 초기 이동 속도, 초당 월드 단위</param>
    /// <param name="ownerColliders">던진 플레이어의 콜라이더</param>
    /// <returns>투척 성공 여부</returns>
    bool TryThrow ( Vector3 velocity, Collider [ ] ownerColliders );
}
