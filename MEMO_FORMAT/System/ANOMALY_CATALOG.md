# Rabbit Party Anomaly Catalog

## PURPOSE

Rabbit Party의 변칙 사건(Anomaly) 유형, 대표 사건 후보, 공통 발생 / 해결 규칙, 구현 우선순위를 한곳에서 관리합니다.

이 문서는 기존 `ANOMALY.md`의 시스템 책임 정의를 대체하지 않습니다.

유형과 방별 후보, 대응 예시 및 권장 순서를 관리합니다. 후보와 권장 수치는 구현 완료나 최종 밸런스 확정과 구분합니다. 실제 완료와 사용자 검증은 [DONE.md](../DONE.md), 현재 우선순위는 [TODO.md](../TODO.md), 공통 책임은 [ANOMALY.md](ANOMALY.md)를 따릅니다. 재료 재보충은 [PARTY_PREPARATION.md](PARTY_PREPARATION.md)의 INGREDIENT_REFILL에서 관리합니다.

목적은 다음과 같습니다.

- 변칙 사건 종류를 빠뜨리지 않고 관리
- 기획상의 `AnomalyType`과 실제 구현 클래스 구분
- 방별 사건 후보와 우선순위 관리
- 공통 대응 Action과 사건 유형 분리
- 현재 구현된 Drop 사건 이후 확장 순서 명시
- 담비/여우와 이미 연결된 현재 구현 상태를 중복 설계하지 않도록 기록

---

## 1. ANOMALY 기본 정의

물건의 위치 / 회전 / 상태 / 구성 / 행동 / 표현이 정상 기준에서 벗어난 월드 사건입니다. 발견 → 원인 추론 → 행동 → 결과 확인 → 위협 대응의 흐름을 사용합니다. 시스템 정의와 상태 계약은 [ANOMALY](ANOMALY.md#definition)를 따릅니다.

## 2. 기획상 공통 변칙 유형

`AnomalyType`은 **무엇이 이상해졌는가**를 나타냅니다.

이 분류가 곧 실제 C# 클래스 수를 의미하지는 않습니다.

실제 구현 클래스는 동작 방식과 책임에 따라 더 세분화하거나 공통화할 수 있습니다.

| 유형 / 상세 | 변화 기준 |
| --- | --- |
| [Drop](#21-drop) | 정상적으로 배치되어 있던 아이템이 바닥으로 떨어지는 사건입니다. |
| [Displacement](#22-displacement) | 대상이 정상 위치에서 다른 위치로 이동한 사건입니다. |
| [OrientationChange](#23-orientationchange) | 대상의 위치는 유지되지만 회전 또는 방향이 비정상적으로 변하는 사건입니다. |
| [StateChange](#24-statechange) | 대상의 명시적인 논리 상태가 정상 상태에서 달라지는 사건입니다. |
| [CompositionChange](#25-compositionchange) | 정상적인 구성의 수량, 종류, 조합 또는 구성원이 변하는 사건입니다. |
| [BehaviorChange](#26-behaviorchange) | 대상이 정상적으로 하지 않던 지속 행동, 움직임, 표현을 보이는 사건입니다. |
| [Distortion](#27-distortion) | Hard 전용 기획 유형이며 아직 구현되지 않았습니다. |

### 2.1 Drop

대표 예시:

- 음식 낙하
- 접시 낙하
- 준비물 낙하
- 의상 낙하

특징:

- Rigidbody / ItemCarry / PlacementTarget 등 기존 아이템 시스템 활용
- 단순 위치 이동 사건과 분리
- 바닥에 떨어진 상태가 핵심

현재 상태:

- `ItemDropEvent`가 1차 구현되어 있음
- 음식은 테스트 대상일 뿐 음식 전용 사건이 아님
- 담비와 여우가 기존 Drop 사건을 실제 연계에 사용 중

---

### 2.2 Displacement

현재 코드: DisplaceAnomalyEvent / DisplaceOccurrence / DisplaceAnomalySystem(StateChangeEvent/Displace). 첫 대상은 운반 소품이며 환경 대상 전체를 구현한 것은 아닙니다. 세부 계약과 확인 범위는 ANOMALY / DONE을 따릅니다.

대표 예시:

- 상자 이동
- 옷더미 이동
- 의자 이동
- 조리도구 이동

특징:

- 반드시 바닥으로 떨어질 필요 없음
- Drop과 별개 유형
- Carry 가능한 아이템뿐 아니라 환경 오브젝트에도 적용 가능

---

### 2.3 OrientationChange

대표 예시:

- 액자 회전
- 벽 장식 회전
- 의자 방향 변화
- 옷걸이 방향 변화
- 스피커 방향 변화

초기 권장 규칙:

아래 30도 단위와 2~4 Step은 초기 제안이며 확정 수치나 현재 구현 동작이 아닙니다.

```text
회전 단위
→ 30도

발생 시
→ 정상 Rotation에서 2~4 Step 변화

플레이어 상호작용
→ 한 번에 30도씩 회전

정상 Rotation 도달
→ Resolve
```

---

### 2.4 StateChange

`State` 단독 명칭은 사용하지 않습니다.

프로젝트에 상태 머신 개념이 이미 있으므로, 변칙 사건에서는 반드시 **상태 변화 사건이라는 의미가 드러나는 `StateChange` 명칭**을 사용합니다.

대표 예시:

- 상자 열림 ↔ 닫힘 / 정상 상태의 반대로 전환
- 옷장 닫힘 → 열림
- 창문 닫힘 → 열림
- 조명 켜짐 → 꺼짐
- 냄비 뚜껑 닫힘 → 열림

특징:

- Transform 변경 없이도 발생 가능
- Interact 기반 Resolve 검증에 적합
- Drop 다음 두 번째 실제 사건으로 구현됨

---

### 2.5 CompositionChange

대표 예시:

- 풍선 증가
- 풍선 감소
- 준비물 소실
- 없던 물건 추가
- 두 물건의 자리 교체 (Swap / 교체 사건)
- 정상 물건의 색 / 외형 대체 (Replace / 대체 사건)
- 그림 내용이나 실루엣 형태 자체가 다른 것으로 교체 (Replace / 대체 사건)

개념상 하나의 유형이지만 실제 구현은 필요에 따라 나눌 수 있습니다.

예:

```text
CompositionChange
├─ Add
├─ Remove
├─ Replace / 대체
└─ Swap / 교체
```

기획상 하나의 상위 유형으로 관리하되, 구현 클래스를 하나로 몰아넣지 않습니다.

---

### 2.6 BehaviorChange

대표 예시:

- 자루 흔들림
- 커튼 흔들림
- NPC가 비정상적으로 한 장소를 계속 응시
- 반복적인 이상 움직임

특징:

- 단순 Position 변경과 분리
- “현재 계속 움직이고 있음”이 핵심
- Investigate 같은 Action과 연결하기 좋음

---

### 2.7 Distortion

원본 월드 상태 / 콘텐츠는 유지하고 같은 콘텐츠를 보고 듣는 방식만 교란합니다. 월드 내부 표현으로 제한하며 UI / 입력 / 카메라 방해와 강한 호러 연출은 제외합니다.

| 대표 사례 | 대응 유형 |
| --- | --- |
| 음악 출력 왜곡 | 문제 출력 위치를 찾는 청각 탐색형 |
| 표면 / 텍스처 좌표 왜곡 | 짧은 커서 퍼즐의 직접 보정형 |
| 조명 출력 왜곡 | 제어 장치의 원인 제거형 |
| 거울 반사 동기 왜곡 | 담요 / 천으로 반사를 막는 봉쇄형 |
| 공간 음향 방향 왜곡 | 원인 제거형 / 플레이테스트 후 채택할 실험 후보 |

그림 내용 자체나 실루엣 형태가 다른 것으로 교체되면 Replace / 대체로 분류합니다. 상세 효과 / 대응 / 결과 계약과 미정 사항은 [ANOMALY](ANOMALY.md#6-distortion--표현-왜곡)에서만 관리합니다.

---

## 3. ANOMALY TYPE과 ACTION 분리

AnomalyType은 이상해진 상태, Action / ResolveAction은 플레이어의 대응입니다. Investigate는 유형이 아니라 해결 행동입니다.

| 사건 | Type | Action |
| --- | --- | --- |
| 자루 흔들림 | BehaviorChange | Investigate |
| 액자 회전 | OrientationChange | Interact |
| 음식 낙하 | Drop | Carry + Place |

자루 흔들림은 IInvestigatable과 E 홀딩 조사를 사용합니다. 대상 / ID 검증은 [PLAYER_INTERACTION](PLAYER_INTERACTION.md#investigation), 사건 계약은 [ANOMALY](ANOMALY.md#event_design)를 따릅니다. 나머지 유형 후보는 위 목록에서 관리합니다.

## 4. 아이템과 변칙의 관계

운반 아이템과 비운반 환경 모두 대상이 될 수 있습니다. 실제 AnomalyTarget / 개폐 연결은 [공통 대상 계약](ANOMALY.md#common_target_and_normal_state)을 따릅니다.

후속 데이터 후보는 대상별 지원 유형 / 태그 / 카테고리입니다. Plate의 Drop / Displacement, Picture의 OrientationChange, Box의 Displacement / StateChange, Curtain의 BehaviorChange처럼 구분할 수 있습니다. 현재 구현된 범용 타입 선택 기능으로 해석하거나 아이템마다 별도 시스템을 만들지 않습니다.

## 5. NORMAL STATE 기본 규칙

정상 기준의 저장 / 갱신과 잠금은 [ANOMALY의 공통 대상 계약](ANOMALY.md#common_target_and_normal_state)을 따릅니다. 배치 / 개폐 완료로 기준을 갱신하며 진행 중이거나 실패 후 복구 대기 중에는 기존 기준을 덮어쓰지 않습니다.

## 6. RESOLVE / FAIL / RECOVER

제한시간 내 해결은 Resolve, 시간 초과는 Fail, 실패 후 환경 정상화는 Recover입니다. Recover는 이미 발생한 실패와 보상을 되돌리지 않습니다. 대상 무효화 취소는 정상 복구로 취급하지 않습니다. 상태 전이 / 결과 / 적 연계의 상세 계약은 [ANOMALY](ANOMALY.md#event_design)에서 관리합니다.

## 7. 위치형 사건 해결 기준

Drop / Displacement는 최초 위치나 Parent로 돌아가야만 해결되는 사건이 아닙니다. 기존 배치 규칙에 맞는 허용 PlacementTarget에 정상 배치하면 해결할 수 있습니다. 예를 들어 Table A의 음식이 떨어져도 허용되는 Table B에 배치하면 Resolve합니다. 상세 판정은 [ANOMALY](ANOMALY.md#event_design)를 따르며 사건 전용 배치 검사를 중복 구현하지 않습니다.

## 8. SPAWN 시 플레이어 시야 규칙

관찰 중에도 사건 발생을 허용하며 관찰자 수에 따라 확률 / Weight를 낮춥니다. 감시로 발생을 완전히 봉쇄하는 대응을 방지하고 낮은 확률로 눈앞 발생도 허용하기 위한 기준입니다. 비관찰 1.0 / 한 명 감소 / 여러 명 추가 감소는 초기 예시이며 정확한 배율은 미정입니다. 실제 계산 계약은 [관찰 가중치](ANOMALY.md#observation_weight)를 따릅니다.

## 9. 방별 변칙 사건 후보

아래 사건과 대응은 콘텐츠 후보입니다. 현재 Remove의 빈자리 조사와 정상 복구 계약은 [ANOMALY의 Remove 대응 설계](ANOMALY.md#remove-대응-설계), 남은 확인과 표현 확장은 [TODO](../TODO.md#presence_follow_up)를 따릅니다. 다른 구성 변화의 대응 규칙까지 동일하게 적용하지 않습니다.

우선순위:

- A = MVP 필수 또는 핵심 시스템 검증
- B = 기반 완성 후 확장
- C = 후순위 / 별도 표현 또는 시스템 필요

---

### 9.1 조리실

| 사건 | 유형 | 대응 예시 | 우선순위 |
|---|---|---|---|
| 음식/재료 낙하 | Drop | Carry + Place | A |
| 창문 열림 | StateChange | Interact | A |
| 조리도구 이동 | Displacement | 이동/재배치 | B |
| 냄비/팬 뚜껑 열림 | StateChange | Interact | B |
| 재료 소실 | CompositionChange | Investigate / 복구 | B |
| 조리도구 방향 변화 | OrientationChange | Interact | C |

---

### 9.2 창고

| 사건 | 유형 | 대응 예시 | 우선순위 |
|---|---|---|---|
| 준비물 낙하 | Drop | Carry + Place | A |
| 상자 이동 | Displacement | 이동/재배치 | A |
| 상자 개폐 | StateChange | Interact | A |
| 자루 흔들림 | BehaviorChange | Investigate | A |
| 선반 물품 위치 변화 | Displacement | 재배치 | B |
| 선반 물품 방향 변화 | OrientationChange | Interact | B |
| 준비물 소실 | CompositionChange | Investigate | B |
| 상자 위치 교환 | CompositionChange / Displacement 조합 | 재배치 | B |
| 없던 물건 추가 | CompositionChange | 판별/제거 | C |

창고는 담비 핵심 공간이며, 정상 상태 자체가 복잡해 변화 탐지가 어렵다는 특성을 가집니다.

---

### 9.3 파티룸

| 사건 | 유형 | 대응 예시 | 우선순위 |
|---|---|---|---|
| 음식/접시 낙하 | Drop | Carry + Place | A |
| 액자/벽 장식 회전 | OrientationChange | Interact | A |
| 의자 위치 이동 | Displacement | 이동/배치 | B |
| 의자 방향 변화 | OrientationChange | Interact | B |
| 장식 소실 | CompositionChange | 조사/복구 | B |
| 풍선 증가/감소 | CompositionChange | 판별/복구 | B |
| 스피커 방향 변화 | OrientationChange | Interact | B |
| 음악 출력 왜곡 | Distortion / Hard 전용 | 문제 스피커 탐색 / 조율·리셋 | C |
| 표면 / 텍스처 좌표 왜곡 | Distortion / Hard 전용 | 짧은 커서 보정 퍼즐 | C |
| 조명 출력 왜곡 | Distortion / Hard 전용 | 제어 패널 / 전원 장치 리셋 | C |
| 공간 음향 방향 왜곡 | Distortion / Hard 실험 후보 | 간섭 발생원 탐색 / 리셋 | C |
| NPC 비정상 행동 | BehaviorChange | Investigate | C |

---

### 9.4 드레스룸

| 사건 | 유형 | 대응 예시 | 우선순위 |
|---|---|---|---|
| 의상/준비물 낙하 | Drop | Carry + Place | A |
| 옷장 열림 | StateChange | Investigate / Interact | A |
| 커튼 흔들림 | BehaviorChange | Investigate | A |
| 옷더미 이동 | Displacement | 이동/배치 | A |
| 옷걸이 방향 변화 | OrientationChange | Interact | B |
| 의상 소실 | CompositionChange | Investigate | B |
| 거울 반사 동기 왜곡 | Distortion / Hard 전용 | 담요 / 천 운반·배치로 반사 차단 | C |
| 옷장 내부 실루엣 형태 교체 | CompositionChange / Replace | 대체 대응 / 복원 | C |

---

## 10. 적 연계 계약

담비는 자신의 사건 Resolved 결과를 저지 기록에 반영하고, 여우는 Failed를 일반 납치 조건으로 사용합니다. 여우의 구조 저지 누적 보복은 사건을 생략합니다. 상세는 [PREDATOR](PREDATOR.md#room_activity_plan)를 따릅니다.

구현과 사용자 확인 이력은 [DONE](../DONE.md#포식자), 반복 흐름 / 동시 처리 / 종료와 밸런스 검증은 [TODO](../TODO.md#follow_up)에서 관리합니다.

---

## 11. 사건 후보의 구현 특성 비교

아래는 사건 후보의 구현 난이도와 검증 가치를 비교한 설계 참고입니다. 구현 순서와 완료 여부는 TODO / DONE을 따릅니다.

사건 간 비교에서는 Drop과 구현 방식이 다른 기능에도 `AnomalyEvent → Presenter → AnomalyLinker → 결과` 흐름을 재사용할 수 있는지 구분합니다.

| 후보 | 유형 | 구현 난이도 | 검증 가치 | 비고 |
|---|---|---:|---:|---|
| 상자 개폐 | StateChange | 기반 작업 포함 | 매우 높음 | 일반 개폐 / E 입력 / 정상 기준 / 공통 사건 연결 구현됨 |
| 액자 회전 | OrientationChange | 낮음~중간 | 높음 | 단계 상호작용 검증 |
| 자루 흔들림 | BehaviorChange | 중간 | 매우 높음 | Investigate Action까지 검증 |
| 상자 이동 | Displacement | 중간 | 중간~높음 | Drop과 위치 변화 개념이 일부 가까움 |
| 준비물 소실 | CompositionChange | 중간~높음 | 높음 | 정상 구성 집합 관리 필요 |
| 풍선 증가/감소 | CompositionChange | 높음 | 높음 | 다중 대상/집합 상태 필요 |
| 음악 출력 왜곡 | Distortion | 높음 | 중간 | Hard 전용 후속 후보 / Audio 연계 필요 |

난이도는 후보 간 비교 참고입니다. 개폐와 공통 Presenter / 링커 연결은 구현됐으며 실행 확인 범위는 DONE을 따릅니다.

---

## 12. 두 번째 사건: 상자 개폐

StateChange / 개폐는 정상 상태가 열림이면 닫고 닫힘이면 여는 양방향 사건입니다. 발생 전 정상 상태로 돌아오면 해결합니다.

- 일반 개폐와 E 입력, 비운반 등록과 정상 기준, 개폐 사건 및 공통 Presenter / 링커 연결은 구현됐습니다.
- 낙하는 정상 Placement 완료로, 개폐는 정상 논리 상태 복구로 해결합니다. 두 사건은 AnomalyEvent의 시간과 생명주기를 공유합니다.
- 기본 발생 API는 TryStart(out AnomalyEvent), 수동 테스트는 TryStartManual입니다. 적은 여러 Presenter 후보를 등록하며 현재는 적마다 결과 대기 한 건을 허용합니다.
- 공통화 범위와 현재 파일명 / 폴더 / API 및 남은 확인은 [ANOMALY.md](ANOMALY.md), 실제 사용자 확인은 [DONE.md](../DONE.md)를 따릅니다.
- 현재 대표 사건의 우선순위는 [TODO.md](../TODO.md), 완료와 실행 확인 범위는 [DONE.md](../DONE.md)를 따릅니다.

---

## 13. 구현 문서

- 유형별 계약과 상세 설계: [ANOMALY](ANOMALY.md#event_design)
- 현재 단계: [ROADMAP](../ROADMAP.md)
- 다음 작업: [TODO](../TODO.md)
- 코드 반영과 사용자 확인 범위: [DONE](../DONE.md)

이 문서는 유형과 콘텐츠 후보를 관리합니다. 구현 클래스 수를 유형 수와 동일시하지 않으며 조사 / 운반 등 대응 Action은 유형과 구분합니다.

