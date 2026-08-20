using System;

namespace PB_AnimationLibrary.Exchange
{
    [Serializable]
    public sealed class PoseSequenceBakeExchangeFile
    {
        public string schema = "PBAnimationLibrary.PoseSequenceBakeExchange";
        public int schemaVersion = 1;

        public string clipName;
        public string sourcePoseName;
        public string samplingRoot = "unit_mech_body";
        public string pathRoot = "joint_root";

        public float frameRate;
        public float duration;
        public int sourceKeyframeCount;

        public PoseSequenceBakeTrack[] tracks =
            new PoseSequenceBakeTrack[0];
    }

    [Serializable]
    public sealed class PoseSequenceBakeTrack
    {
        public string path;
        public string pathHash;

        public bool hasPosition;
        public bool hasRotation;

        public PoseSequenceBakePositionKey[] positionKeys =
            new PoseSequenceBakePositionKey[0];

        public PoseSequenceBakeRotationKey[] rotationKeys =
            new PoseSequenceBakeRotationKey[0];
    }

    [Serializable]
    public sealed class PoseSequenceBakePositionKey
    {
        public float time;
        public float x;
        public float y;
        public float z;
    }

    [Serializable]
    public sealed class PoseSequenceBakeRotationKey
    {
        public float time;
        public float x;
        public float y;
        public float z;
        public float w;
    }
}
