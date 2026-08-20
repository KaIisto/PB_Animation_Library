# PoseSequence Bake Exchange Schema v1

`PBAnimationLibrary.PoseSequenceBakeExchange`

이 형식은 Pose Lab의 장기 저장 파일이 아니라 SDK `.anim` baking용 교환 형식이다.

```text
root
├─ schema
├─ schemaVersion
├─ clipName
├─ sourcePoseName
├─ samplingRoot
├─ pathRoot
├─ frameRate
├─ duration
├─ sourceKeyframeCount
└─ tracks[]
   ├─ path
   ├─ pathHash
   ├─ hasPosition
   ├─ hasRotation
   ├─ positionKeys[]
   │  ├─ time
   │  ├─ x
   │  ├─ y
   │  └─ z
   └─ rotationKeys[]
      ├─ time
      ├─ x
      ├─ y
      ├─ z
      └─ w
```

Transform path는 `unit_mech_body`를 sampling root로 하는 relative path이며
`joint_root/...` 형태를 사용한다.

Position/Rotation key 값은 Source Pose 대비 delta가 아니라
baking 시 바로 사용할 수 있는 absolute local transform 값이다.
