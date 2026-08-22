# PB Animation Library 프로젝트 경계

대상: Phantom Brigade 2.2.2  
현재 개발 기준: `0.11.6-weapon-authoring-preview3`

## 1. 프로젝트 목적

PB Animation Library는 Phantom Brigade용 custom animation을 제작하기 위한 authoring toolchain이다.

핵심 결과물은 Unity `AnimationClip` (`.anim`)이며,
완성된 clip을 gameplay에서 언제 어떻게 사용할지는 이 프로젝트가 결정하지 않는다.

## 2. Runtime Pose Lab

Pose Lab은 실제 PB visible mech rig를 authoring source로 사용한다.

주요 책임:

- Source Pose 캡처와 Original Pose 복원
- vanilla customization pose를 authoring source로 sample
- authoring bone edit
- symmetry / branch mirror
- Weapon Follow / native hand snap preview
- unit forward / equipped weapon muzzle guide
- PoseSequence keyframe / scrub
- Track Scope
- bake exchange JSON export

전체 hierarchy snapshot은 Source Pose와 restore 정확도를 위해 유지한다.
사용자 편집 범위는 `UnitVisualManager.primaryBones`에 한정한다.

기본 authoring bone:

- 21 non-finger primary bones
- 40 finger primary bones는 Advanced 옵션으로 표시

`*_auto` 및 non-primary helper는 직접 authoring하지 않는다.

`joint_pelvis_xyz`는 무릎 꿇기 등 chassis 높이 조정이 필요한 pose를 위해
local Y position만 직접 편집할 수 있다.

Weapon Follow / native hand snap과 muzzle guide는 authoring preview 전용이다.
`joint_left_weapon` / `joint_right_weapon` 또는 muzzle Transform을 PoseSequence track으로 export하지 않는다.

## 3. PoseSequence / Bake Exchange

PoseSequence는 Source Pose 대비 sparse delta를 저장한다.

- position: Lerp
- rotation: Slerp
- curve가 필요하지 않은 bone/channel은 최종 clip에 만들지 않음

Track Scope는 최종 `.anim`이 소유할 authoring bone 범위를 제한한다.

Bake exchange JSON은 장기 프로젝트 저장 포맷이 아니라
SDK `.anim` bake를 위한 교환 포맷이다.

Schema:
`PBAnimationLibrary.PoseSequenceBakeExchange` v1

## 4. SDK Authoring Add-on

PB Mod SDK Add-on은 exchange JSON을 native Unity `.anim`으로 bake한다.

책임:

- schema 검증
- AnimationClip curve 생성
- clip 이름 지정
- AssetBundle label 지정
- overwrite 정책
- local Transform-only preview rig
- SDK reference visual proxy
- scrub / play preview

vanilla 추출 prefab/mesh는 Add-on에 포함하지 않는다.

## 5. Runtime Validation

Pose Lab은 loaded AssetBundle의 `.anim`을 찾아 실제 visible mech rig에 sample할 수 있다.

이 기능의 목적은 다음 bridge만 검증하는 것이다.

```text
SDK .anim
→ PB Mod SDK AssetBundle
→ PB runtime load
→ visible unit_mech_body
→ sparse SampleAnimation
```

Validation playback은 내부 도구이며 public consumer API를 제공하지 않는다.

## 6. Consumer 경계

WFEF 또는 개별 content mod가 소유해야 하는 항목:

- animation 선택 규칙
- action 시작/종료와 clip lifecycle
- interrupt / cancel
- blend / cross-fade
- procedural aiming ordering
- recoil / support-hand 후처리
- weapon grip / attachment / mount semantics
- equipment/tag 조건
- gameplay runtime registry/API

PB Animation Library assembly를 consumer runtime dependency로 요구하지 않는 구성을 기본 방향으로 한다.

## 7. 배포 경계

배포하지 않음:

- vanilla에서 추출한 rig/prefab/mesh/material
- SDK 공식 sample asset의 복제본
- `bin/`, `obj/`, `.idea/`
- 임시 probe/test code
- 개발 과정용 research archive

연구 기록은 release source와 분리된 development archive에서 보존한다.
