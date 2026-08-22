# PB Animation Library / Pose Lab


## 1. 개요

PB Animation Library는 Phantom Brigade에서 **커스텀 애니메이션을 만들기 위한 authoring toolchain**임

- **Pose Lab(모드)**  
  실제 메크의 포즈를 기준으로 관절을 조정하고, 여러 시점의 keyframe을 만들어 `PoseSequence`를 내보낼 수 있음
- **PB Mod SDK Add-on(SDK)**  
  Pose Lab이 내보낸 `.pbalibpose.json`을 Unity의 native `AnimationClip`인 `.anim`으로 bake하여 모드가 사용 가능한 AssetBundle에 사용 가능하게 만듬


작업 순서:

```text
Pose Lab 모드 설치
→ Source Pose 선택/캡처
→ Authoring Bone 편집
→ PoseSequence keyframe 작성
→ Track Scope 선택
→ .pbalibpose.json export
→ PB Mod SDK Add-on
→ native .anim bake
→ AssetBundle export
```

Pose Lab에서는 vanilla `UnitVisualManager.primaryBones`를 기준으로 편집 가능한 관절을 제한함

- 기본 authoring bone: 21개
- finger bone: 40개 (`Show finger joints (advanced)`에서 선택적으로 표시)
- `*_auto` 및 non-primary helper: 직접 편집하지 않음
- `joint_pelvis_xyz`: 추가로 Pelvis의 **local Y 높이**를 조정 가능

또한 Weapon Follow, native hand weapon snap preview, weapon muzzle guide, 좌우 symmetry/branch mirror, unit forward guide, sparse Track Scope를 각자 지원함

이 프로젝트의 책임은 **PB에서 정상적으로 사용할 수 있는 `.anim`를 만드는 것까지**다.  
완성된 `.anim`을 어떤 action에서 언제 재생할지, aiming/blend/cancel 등을 어떻게 처리할지는 WFEF 또는 각 consumer/content mod의 역할이며 해당 모드는 그것에 관여하지 않음

---

## 2. Add-on 설치법

이 ZIP 안의 다음 폴더가 PB Mod SDK용 Add-on임

```text
PBAnimationLibrary_SDK_Add-on/
└─ Assets/
   └─ Editor/
      └─ PBAnimationLibrary/
```

### 설치

1. Phantom Brigade Mod SDK 프로젝트를 연다
2. 이 패키지의
   `PBAnimationLibrary_SDK_Add-on/Assets/Editor/PBAnimationLibrary/`
   폴더, 또는 Git 릴리스의 PBAnimationLibrary_SDK_Add-on을 다운받아서 압축을 푼 결과물을 SDK 프로젝트의
   `Assets/Editor/PBAnimationLibrary/`
   위치에 복사한다
3. Unity가 script compile을 끝낼 때까지 기다린다
4. 상단 메뉴에 다음 항목이 생기면 설치 완료다

```text
Tools
└─ PB Animation Library
   ├─ Pose Sequence Baker
   └─ Animation Preview
```

### Preview용 선택 사항

`.anim` bake 자체에는 별도 vanilla asset이 필요하지 않음

SDK에서 자동 Visual Preview까지 사용하려면 SDK 프로젝트 안에 다음 자료가 있어야 함(어디든 존재하기만 하면 된다).

- `unit_mech_body` asset - 아직 공식 배포 허가를 받지 못함
- PB Mod SDK의 공식 `armor_set_skeleton-replace` - https://wiki.braceyourselfgames.com/en/PhantomBrigade/Modding/official-mech-armor-modding 의 맨 아래 링크

---

## 3. 사용법

### A. Pose Lab에서 애니메이션 만들기

1. steam 창작마당에서 'PB Pose Lab'를 구독
2. 모드를 적용하고 아무 전투에 진입
3. Pose Lab이 표시되면 **Source Browser**에서 편집할 주 Actor를 선택
4. Source Pose를 정함
   - `Capture current visible pose`: 현재 보이는 자세를 Source로 캡처함
   - `Refresh loaded customization poses`: 로드된 vanilla customization pose 검색
   - 원하는 customization pose 선택(선택사항)
   - `Restore original pose`: 처음 캡처한 원래 자세로 복원
5. **Pose Editor**의 `Authoring Bones`에서 관절을 선택해 회전시킴
   - 좌우 대칭 편집/미러 사용 가능
   - 현재 무기 위치를 유지한 채 손을 따라오게 하려면 `Left/Right weapon follows current offset` 사용
   - Source Pose에서 무기가 손에 없으면 `Snap left/right weapon to native hand reference`로 native 손 기준 위치에 붙여서 preview 가능
   - 총구 방향을 확인하려면 `Show equipped weapon muzzle guides` 사용. 현재 손 장비의 `ItemActivationLink.visualTransform`에서 +Z 방향으로 선이 표시됨
   - 필요하면 `Show unit forward guide` 사용
   - weapon snap/follow와 muzzle guide는 authoring preview 전용이며 weapon root나 muzzle은 PoseSequence/.anim track으로 export되지 않음
   - finger는 `Show finger joints (advanced)`를 켰을 때만 표시
   - `joint_pelvis_xyz`를 선택하면 `Pelvis Height — local Y only`로 높이를 조정할 수 있음
6. `Show Pose Sequence window`를 눌러 Pose Sequence 창을 엶
7. `Clip name`, `Frame rate`, `Sequence length`를 정함
8. 원하는 시점으로 Time slider를 이동한 뒤 포즈를 만들고
   `Add / replace keyframe at current time`버튼을 클릭
9. 필요한 만큼 keyframe을 반복해서 만든다
10. `Bake track scope`를 선택
11. `Export bake exchange JSON`를 클릭

Export된 파일은 게임의 `Application.persistentDataPath` 아래 다음 폴더에 저장되며, Pose Lab 창에도 마지막 export 경로가 표시됨
기본경로는 다음과 같다: AppData\LocalLow\Brace Yourself Games\Phantom Brigade\PBAnimationLibrary\PoseSequenceExports

```text
PBAnimationLibrary/PoseSequenceExports/
```

Pose Lab 전체 창은 **Numpad 5**로 숨기거나 다시 표시할 수 있음

### B. SDK에서 `.anim`으로 Bake

1. PB Mod SDK에서
   `Tools > PB Animation Library > Pose Sequence Baker`를 얾
2. Pose Lab에서 만든 `.pbalibpose.json`을 선택
3. `Clip asset name`을 정함
4. `AssetBundle name`을 정함
5. `Output asset folder`를 확인
6. `Bake .anim`을 클릭

`Overwrite existing .anim`은 기본 OFF  
같은 이름이 이미 있으면 `_001`, `_002` 같은 suffix를 붙여 기존 파일을 보존함

### C. SDK에서 Preview

가장 간단한 방법:

1. Project 창에서 생성된 `.anim`을 선택
2. `Tools > PB Animation Library > Animation Preview`를 연다

필요한 local rig/reference asset이 있으면 Add-on이 preview rig와 visual proxy를 자동으로 준비함
정상이라면 `Automatic preview READY`가 표시

### D. AssetBundle로 내보내기

`.anim`에 지정한 `AssetBundle name`을 사용해 PB Mod SDK의 일반 AssetBundle export 절차로 bundle을 만들 수 있음

### E. 게임에서 `.anim` 최종 검증

Pose Lab의 Pose Sequence 창 아래쪽 `AnimationClip Validation`은 만들어진 `.anim`이 실제 PB visible mech에서 정상적으로 load/sample되는지 확인하기 위한 내부 검증 도구를 지원함

1. `.anim`이 포함된 AssetBundle을 게임에서 로드
2. `AnimationClip Validation`에서 `Refresh loaded .anim`을 클릭
3. 원하는 clip을 선택
4. Play / Pause / scrub / Stop 으로 애니메이션을 재확인

이 Validation 기능은 authoring 결과 확인용이며 외부 모드용 gameplay API가 아님

---

추가 문서:

- `Docs/PoseSequence_Bake_Exchange_Schema_v1_KO.md` — bake exchange schema
- `Docs/PB_Animation_Library_Project_Charter_KO.md` — 프로젝트 책임 경계
- `Docs/PB_Animation_Library_Validation_KO.md` — runtime validation 설명
