# AnimationClip Validation

Pose Lab의 AnimationClip Validation은 authoring 결과를 실제 Phantom Brigade visible mech에서 확인하기 위한 내부 도구다.

## 목적

다음을 확인한다.

- `.anim`이 mod AssetBundle에 정상 포함되었는가
- runtime에서 `AnimationClip`으로 load 가능한가
- binding path가 실제 `unit_mech_body`와 맞는가
- sparse curve가 의도한 bone에 적용되는가
- planning / simulation에서 sample이 보이는가
- Stop 시 validation 시작 전 pose로 복원되는가

## 사용

Pose Sequence 창의 `AnimationClip Validation` 영역에서:

1. Filter 입력
2. `Refresh loaded .anim`
3. clip 선택
4. Loop / Preserve current head aim 설정
5. Play / Pause / Resume / scrub / Stop

`Preserve current head aim`은 validation 편의를 위한 옵션이다.
consumer mod의 실제 head aiming 정책을 정의하지 않는다.

Validation은 gameplay recoil, support-hand, weapon grip/attachment를 재현하지 않는다.
이 항목은 WFEF 또는 개별 consumer/runtime mod가 action과 실제 장비 상태를 기준으로 처리한다.

## 구현 경계

`AnimationClipValidationCatalog`는 현재 로드된 AssetBundle의 `.anim`만 열거한다.

`AnimationClipValidationRuntime`은 선택한 clip을 visible mech에 sample한다.

두 타입은 `internal`이며 외부 mod용 API 계약이 아니다.
