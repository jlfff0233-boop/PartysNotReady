using System;

/// <summary>
/// 저항 모델 - 연타 진행도와 성공 판정
/// </summary>
public class ResistModel
{
    int _requiredCount;     //성공에 필요한 진행도
    float _decreaseSpeed;       //초당 진행도 감소량

    float _progress;        //현재 진행도
    bool _isCompleted;      //성공 확정 여부
    float _timeLimit;       //제한시간, 0이면 시간 제한 없음
    float _elapsedTime;     //이번 저항의 경과시간

    /// <summary>
    /// 현재 진행도
    /// </summary>
    public float Progress => _progress;

    /// <summary>
    /// 성공에 필요한 진행도
    /// </summary>
    public int RequiredCount => _requiredCount;

    /// <summary>
    /// 진행 비율
    /// </summary>
    public float ProgressRatio => _progress / _requiredCount;

    /// <summary>
    /// 성공 확정 여부
    /// </summary>
    public bool IsCompleted => _isCompleted;

    /// <summary>
    /// 저항 제한시간 초과 여부
    /// </summary>
    public bool IsExpired =>
        _timeLimit > 0f && _elapsedTime >= _timeLimit;

    /// <summary>
    /// 저항 설정 저장
    /// </summary>
    /// <param name="requiredCount">성공에 필요한 진행도</param>
    /// <param name="decreaseSpeed">초당 진행도 감소량</param>
    public ResistModel ( int requiredCount , float decreaseSpeed )
    {
        _requiredCount = Math.Max ( 1 , requiredCount );
        _decreaseSpeed = Math.Max ( 0f , decreaseSpeed );
    }


    /// <summary>
    /// 진행도와 성공 상태 초기화
    /// </summary>
    void ResetState ( )
    {
        _progress = 0f;
        _isCompleted = false;
        _elapsedTime = 0f;
    }

    /// <summary>
    /// 저항 시작과 제한 시간 설정
    /// </summary>
    /// <param name="timeLimit">제한 시간(초), 0이면 제한 없음</param>
    public void Begin ( float timeLimit = 0f )
    {
        ResetState ( );
        //시간 제한이 없어도 진행도 감소는 계속 적용
        _timeLimit = Math.Max ( 0f , timeLimit );
    }

    /// <summary>
    /// 경과 시간과 진행도 감소 반영
    /// </summary>
    /// <param name="deltaTime">경과 시간(초)</param>
    public void UpdateProgress ( float deltaTime )
    {
        if ( IsCompleted == true || IsExpired == true )
            return;

        float elapsed = Math.Max ( 0f , deltaTime );
        _elapsedTime += elapsed;

        //입력이 없어도 진행도는 계속 감소
        _progress = Math.Max ( 0f , _progress - _decreaseSpeed * elapsed );
    }

    /// <summary>
    /// 저항 입력 반영과 성공 판정
    /// </summary>
    /// <param name="progressAmount">입력 진행량</param>
    /// <returns>이번 입력으로 저항에 성공했는지 여부</returns>
    public bool TryRescue ( float progressAmount = 1f )
    {
        //성공 확정 또는 시간 초과 이후의 추가 입력 차단
        if ( IsCompleted == true || IsExpired == true ||
             float.IsNaN( progressAmount ) == true || float.IsInfinity( progressAmount ) == true ||
             progressAmount <= 0f )
            return false;

        //목표 진행도를 넘지 않도록 제한
        _progress = Math.Min ( _requiredCount , _progress + progressAmount );

        if ( _progress < _requiredCount )
            return false;

        _isCompleted = true;
        return true;
    }


}
