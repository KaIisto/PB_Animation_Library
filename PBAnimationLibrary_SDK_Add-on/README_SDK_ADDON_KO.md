# PB Animation Library SDK Authoring Add-on

Phantom Brigade Mod SDK용 Editor Add-on.

Pose Lab이 export한 `.pbalibpose.json`을 native Unity `.anim`으로 bake하고,
SDK Scene에서 animation을 시각적으로 검증한다.

## 설치

ZIP의 `Assets/Editor/PBAnimationLibrary/`를 PB Mod SDK 프로젝트의 동일 경로에 복사한다.

Add-on은 다음 asset을 포함하지 않는다.

- vanilla `unit_mech_body.prefab`
- vanilla mesh/material
- `armor_set_skeleton-replace.fbx`

Preview에 필요한 vanilla rig는 사용자가 로컬 연구용으로 준비해야 한다.
Visual Proxy는 PB Mod SDK에 존재하는 공식 `armor_set_skeleton-replace`를 사용한다.

## Bake

`Tools > PB Animation Library > Pose Sequence Baker`

1. `Bake exchange JSON`에서 `.pbalibpose.json` 선택
2. `Clip asset name` 지정
3. `AssetBundle name` 지정
4. `Output asset folder` 확인
5. `Bake .anim`

`Overwrite existing .anim`은 기본 OFF다.

같은 이름의 `.anim`이 이미 있으면 `_001`, `_002` 형식으로 새 asset을 만든다.
명시적으로 기존 clip을 교체할 때만 overwrite를 켠다.

`AssetBundle name`은 생성된 `.anim`의 Unity AssetBundle label이다.
실제 bundle 파일 생성은 PB Mod SDK의 일반 export 절차가 담당한다.

## Animation Preview

가장 단순한 사용법:

1. Project에서 `.anim` 선택
2. `Tools > PB Animation Library > Animation Preview`

Add-on이 자동으로:

```text
unit_mech_body local asset 탐색
→ Transform-only Scene rig 생성
→ clip binding 검증
→ armor_set_skeleton-replace 탐색
→ visual proxy 생성
→ time 0 sample
→ Scene View framing
```

을 수행한다.

정상 상태에서는 `Automatic preview READY`가 표시된다.

## Advanced / Manual Setup

자동 탐색이 맞지 않을 때만 사용한다.

- Preview Root 직접 지정
- 선택한 GameObject에서 Transform-only copy 생성
- skeleton / joint name 표시
- SDK reference model 직접 지정
- Visual Proxy build/remove

## Visual Proxy

AnimationClip은 Transform-only preview rig에 sample한다.

`armor_set_skeleton-replace` mesh는 별도의 Editor-only visual layer로 대응 joint에 부착한다.
Visual Proxy는 clip curve, binding, rig hierarchy를 변경하지 않는다.

생성된 proxy에는 필요한 rendering component만 유지하며
Editor build 산출물에는 포함하지 않는다.

## 안전 경계

- vanilla 추출 asset을 Add-on ZIP에 포함하지 않음
- SDK 공식 sample asset을 Add-on ZIP에 중복 포함하지 않음
- bridge/probe/test clip generator를 release package에 포함하지 않음
- 생성된 `.anim`은 PB Mod SDK의 일반 AssetBundle workflow를 사용
