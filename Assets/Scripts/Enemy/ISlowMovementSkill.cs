namespace WesternLemegeton
{
    // 스킬은 사용 상태에 맞는 배율만 제공하고, Brain이 공통 이동 모듈에 감속을 적용합니다.
    // 사용 중이 아니면 1을 반환합니다. 공격 자체가 이동을 막는 경우에는 이 규격보다 정지가 우선합니다.
    public interface ISlowMovementSkill
    {
        float SkillMovementMultiplier { get; }
    }
}
