using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Unity.Profiling;
using UnityEngine;
using UnityEngine.Rendering;

namespace BelobogOptimization
{
    // Inert in normal gameplay. Enable with --belobog-benchmark in a standalone build.
    public sealed class BelobogBenchmark : MonoBehaviour
    {
        public GameObject originalVisual, splitVisual, optimizedVisual;
        public RenderPipelineAsset baselinePipeline, optimizedPipeline;
        public Vector3 mapOffset;
        [Serializable] public class Result
        {
            public string variant, gpu, cpu, unityVersion, qualityMode;
            public int run, frames, width, height, longFrames;
            public double seconds, averageFps, p95Ms, p99Ms, maxMs;
            public double meanBatches, meanSetPass, meanTriangles, meanMainThreadMs, meanRenderThreadMs, meanGpuMs, totalMemoryMB;
            public bool batchesAvailable, setPassAvailable, trianglesAvailable, mainThreadAvailable, renderThreadAvailable, gpuTimingAvailable;
        }
        static string Argument(string key, string fallback)
        {
            var args = Environment.GetCommandLineArgs();
            int i = Array.IndexOf(args, key);
            return i >= 0 && i + 1 < args.Length ? args[i + 1] : fallback;
        }
        IEnumerator Start()
        {
            if (!Environment.GetCommandLineArgs().Contains("--belobog-benchmark")) yield break;
            Application.runInBackground = true;
            Screen.SetResolution(1920, 1080, FullScreenMode.Windowed);
            yield return null;
            var player = FindFirstObjectByType<PlayerController>();
            if (player != null)
            {
                player.enabled = false;
                if (player.InputHandler != null) player.InputHandler.enabled = false;
                player.GetComponent<Rigidbody>().isKinematic = true;
            }
            var camera = Camera.main;
            foreach (var b in camera.GetComponents<Behaviour>())
                if (b.GetType().Name == "CinemachineBrain") b.enabled = false;
            QualitySettings.vSyncCount = 0; Application.targetFrameRate = -1;
            string output = Argument("--report-dir", Path.Combine(Application.persistentDataPath, "BelobogBenchmarks"));
            Directory.CreateDirectory(output);
            var variants = Argument("--variant", "split,original,optimized").Split(',');
            int repeats = int.Parse(Argument("--repeats", "3"));
            float duration = float.Parse(Argument("--duration", "120"), System.Globalization.CultureInfo.InvariantCulture);
            var positions = new[] { new Vector3(0, 2, 5), new Vector3(0, 4, 25), new Vector3(60, 3, 0), new Vector3(0, 1, -25), new Vector3(-30, 38, 45) };
            var targets = new[] { new Vector3(0, 2, 45), new Vector3(30, 6, 0), new Vector3(95, 3, -15), new Vector3(0, -3, -45), new Vector3(0, 5, 15) };
            var timings = new FrameTiming[1];
            foreach (string variant in variants)
            {
                originalVisual.SetActive(variant == "original"); splitVisual.SetActive(variant == "split"); optimizedVisual.SetActive(variant == "optimized");
                // Shared collision stays active, while baseline renderers retain their original settings.
                foreach (var c in originalVisual.GetComponentsInChildren<Collider>(true)) c.enabled = false;
                foreach (var c in splitVisual.GetComponentsInChildren<Collider>(true)) c.enabled = false;
                QualitySettings.renderPipeline = Environment.GetCommandLineArgs().Contains("--common-quality") || variant == "optimized" ? optimizedPipeline : baselinePipeline;
                camera.transform.SetPositionAndRotation(mapOffset + positions[0], Quaternion.LookRotation(targets[0] - positions[0]));
                yield return new WaitForSecondsRealtime(10);
                // Capture validation views before measured frames, allowing encoding and I/O to settle.
                for (int view=0;view<positions.Length;view++)
                {
                    camera.transform.SetPositionAndRotation(mapOffset+positions[view],Quaternion.LookRotation(targets[view]-positions[view]));
                    yield return new WaitForSecondsRealtime(.5f);
                    ScreenCapture.CaptureScreenshot(Path.Combine(output,variant+"_view"+view+".png"));
                    yield return new WaitForSecondsRealtime(.5f);
                }
                yield return new WaitForSecondsRealtime(3);
                for (int run = 1; run <= repeats; run++)
                {
                    var samples = new List<double>(50000);
                    using var batches = ProfilerRecorder.StartNew(ProfilerCategory.Render, "Batches Count");
                    using var setpass = ProfilerRecorder.StartNew(ProfilerCategory.Render, "SetPass Calls Count");
                    using var triangles = ProfilerRecorder.StartNew(ProfilerCategory.Render, "Triangles Count");
                    using var main = ProfilerRecorder.StartNew(ProfilerCategory.Internal, "Main Thread");
                    using var render = ProfilerRecorder.StartNew(ProfilerCategory.Internal, "Render Thread");
                    using var memory = ProfilerRecorder.StartNew(ProfilerCategory.Memory, "Total Used Memory");
                    double bsum=0, ssum=0, tsum=0, msum=0, rsum=0, gsum=0; int gcount=0;
                    double started=Time.realtimeSinceStartupAsDouble, last=started;
                    
                    while (Time.realtimeSinceStartupAsDouble-started < duration)
                    {
                        double elapsed=Time.realtimeSinceStartupAsDouble-started;
                        float progress=(float)(elapsed/duration)*positions.Length;
                        int index=Mathf.Min((int)progress,positions.Length-1);
                        float sway=Mathf.Sin((progress-index)*Mathf.PI*2)*2f;
                        var position=mapOffset+positions[index]+new Vector3(sway,0,0);
                        camera.transform.SetPositionAndRotation(position,Quaternion.LookRotation(mapOffset+targets[index]-position));
                        FrameTimingManager.CaptureFrameTimings();
                        yield return null;
                        double now=Time.realtimeSinceStartupAsDouble; samples.Add((now-last)*1000);last=now;
                        bsum+=batches.LastValue;ssum+=setpass.LastValue;tsum+=triangles.LastValue;
                        msum+=main.LastValue/1000000.0;rsum+=render.LastValue/1000000.0;
                        if(FrameTimingManager.GetLatestTimings(1,timings)>0 && timings[0].gpuFrameTime>0){gsum+=timings[0].gpuFrameTime;gcount++;}
                    }
                    var sorted=samples.OrderBy(x=>x).ToArray();int count=sorted.Length;
                    var result=new Result{variant=variant,qualityMode=Environment.GetCommandLineArgs().Contains("--common-quality")?"common Performance120":"original vs tuned",run=run,gpu=SystemInfo.graphicsDeviceName,cpu=SystemInfo.processorType,unityVersion=Application.unityVersion,
                        frames=count,width=Screen.width,height=Screen.height,seconds=(last-started),averageFps=count/(last-started),
                        p95Ms=sorted[Math.Min(count-1,(int)(count*.95))],p99Ms=sorted[Math.Min(count-1,(int)(count*.99))],maxMs=sorted[count-1],longFrames=samples.Count(x=>x>33.3),
                        meanBatches=bsum/count,meanSetPass=ssum/count,meanTriangles=tsum/count,meanMainThreadMs=msum/count,meanRenderThreadMs=rsum/count,meanGpuMs=gcount>0?gsum/gcount:0,totalMemoryMB=memory.LastValue/1048576.0,
                        batchesAvailable=batches.Valid,setPassAvailable=setpass.Valid,trianglesAvailable=triangles.Valid,mainThreadAvailable=main.Valid,renderThreadAvailable=render.Valid,gpuTimingAvailable=gcount>0};
                    File.WriteAllText(Path.Combine(output,variant+"_run"+run+".json"),JsonUtility.ToJson(result,true));
                    Debug.Log("BELOBOG_BENCHMARK "+JsonUtility.ToJson(result));
                }
            }
            Application.Quit();
        }
    }
}
