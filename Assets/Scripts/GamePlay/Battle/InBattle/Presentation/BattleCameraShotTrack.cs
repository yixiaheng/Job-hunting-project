using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.Timeline;

namespace Battle.Presentation
{
    [TrackColor(0.25f, 0.65f, 0.95f)]
    [TrackClipType(typeof(BattleCameraShotClip))]
    [TrackBindingType(typeof(BattleShotDirector))]
    public sealed class BattleCameraShotTrack : TrackAsset
    {
        public override Playable CreateTrackMixer(PlayableGraph graph, GameObject go, int inputCount)
            => ScriptPlayable<BattleCameraShotMixer>.Create(graph, inputCount);
    }
    public sealed class BattleCameraShotMixer : PlayableBehaviour
    {
        public override void ProcessFrame(Playable playable, FrameData info, object playerData)
        {
            if (!(playerData is BattleShotDirector director)) return;
            float total = 0;
            BattleCameraPose mixed = default;
            for (int i = 0; i < playable.GetInputCount(); i++)
            {
                float weight = playable.GetInputWeight(i);
                var input = (ScriptPlayable<BattleCameraShotBehaviour>)playable.GetInput(i);
                if (weight <= 0) { input.GetBehaviour().ResetReference(); continue; }
                float seconds = (float)input.GetTime();
                float t = input.GetDuration() <= 0 ? 1 : Mathf.Clamp01((float)(seconds / input.GetDuration()));
                var pose = input.GetBehaviour().Evaluate(director, t, seconds);
                mixed = total <= 0 ? pose : BattleCameraPose.Blend(mixed, pose, weight/(total+weight));
                total += weight;
            }
            if (total > 0) director.SetSequencePose(mixed.Position, mixed.Rotation, mixed.Fov);
        }
    }
}
