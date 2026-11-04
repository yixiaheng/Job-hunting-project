using UnityEngine;
using UnityEngine.Rendering;

namespace BelobogOptimization
{
    public sealed class BelobogPerformanceProfile : MonoBehaviour
    {
        public RenderPipelineAsset performancePipeline;
        private RenderPipelineAsset previous;
        private int previousVSync;
        private int previousTarget;
        void OnEnable()
        {
            previous = QualitySettings.renderPipeline;
            previousVSync = QualitySettings.vSyncCount;
            previousTarget = Application.targetFrameRate;
            if (performancePipeline != null) QualitySettings.renderPipeline = performancePipeline;
            QualitySettings.vSyncCount = 0;
            Application.targetFrameRate = 120;
        }
        void OnDisable()
        {
            QualitySettings.renderPipeline = previous;
            QualitySettings.vSyncCount = previousVSync;
            Application.targetFrameRate = previousTarget;
        }
    }
}
