using System;
using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.Timeline;

namespace Battle.Presentation
{
    [TrackClipType(typeof(BattleStagePoseClip)), TrackBindingType(typeof(BattlePresentationCoordinator))]
    [TrackColor(.4f, .8f, .4f)]
    public sealed class BattleStagePoseTrack : TrackAsset
    {
        public override Playable CreateTrackMixer(PlayableGraph graph, GameObject go, int count) => ScriptPlayable<BattleStagePoseMixer>.Create(graph, count);
    }
    public sealed class BattleStagePoseMixer : PlayableBehaviour
    {
        public override void ProcessFrame(Playable playable, FrameData info, object playerData)
        {
            if (playerData is not BattlePresentationCoordinator coordinator) return;
            foreach (BattleStageRole role in Enum.GetValues(typeof(BattleStageRole)))
            {
                float total = 0; Pose mixed = default;
                for (int i = 0; i < playable.GetInputCount(); i++)
                {
                    float weight = playable.GetInputWeight(i); if (weight <= 0) continue;
                    var input = (ScriptPlayable<BattleStagePoseBehaviour>)playable.GetInput(i); var b = input.GetBehaviour(); if (b.Role != role) continue;
                    Pose p = b.Evaluate(input.GetDuration() <= 0 ? 1 : Mathf.Clamp01((float)(input.GetTime() / input.GetDuration())));
                    float blend = weight / (total + weight); mixed = total == 0 ? p : new Pose(Vector3.Lerp(mixed.position, p.position, blend), Quaternion.Slerp(mixed.rotation, p.rotation, blend)); total += weight;
                }
                if (total > 0) coordinator.SetStagePose(role, mixed);
            }
        }
    }
}
