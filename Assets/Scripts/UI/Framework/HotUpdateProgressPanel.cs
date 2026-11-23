using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Game.UI
{
    public sealed class HotUpdateProgressContext : IUIContext
    {
        public HotUpdateProgressContext(HotUpdateBootstrap bootstrap)
        {
            Bootstrap = bootstrap;
        }

        public HotUpdateBootstrap Bootstrap { get; }
    }

    public sealed class HotUpdateProgressPanel : UIPanel
    {
        [SerializeField] private TMP_Text phaseLabel;
        [SerializeField] private TMP_Text versionLabel;
        [SerializeField] private TMP_Text fileLabel;
        [SerializeField] private TMP_Text bytesLabel;
        [SerializeField] private TMP_Text percentLabel;
        [SerializeField] private Slider progressBar;
        [SerializeField] private Button retryButton;

        private HotUpdateBootstrap bootstrap;

        protected override void OnInitialize()
        {
            if (retryButton != null)
                retryButton.onClick.AddListener(HandleRetry);
        }

        protected override void OnOpen(IUIContext context)
        {
            Bind(context as HotUpdateProgressContext);
        }

        protected override void OnContextChanged(IUIContext context)
        {
            Bind(context as HotUpdateProgressContext);
        }

        protected override void OnClosing()
        {
            Unbind();
        }

        private void Bind(HotUpdateProgressContext context)
        {
            HotUpdateBootstrap next = context?.Bootstrap;
            if (bootstrap == next)
            {
                if (bootstrap != null)
                    Refresh(bootstrap.CurrentStatus);
                return;
            }

            Unbind();
            bootstrap = next;
            if (bootstrap == null)
            {
                Refresh(
                    HotUpdateStatus.Create(
                        HotUpdatePhase.Failed,
                        HotUpdateSourceKind.LocalOnly,
                        message: "热更新服务未初始化",
                        error: "缺少 HotUpdateBootstrap。"));
                return;
            }

            bootstrap.StatusChanged += Refresh;
            Refresh(bootstrap.CurrentStatus);
        }

        private void Unbind()
        {
            if (bootstrap != null)
                bootstrap.StatusChanged -= Refresh;
            bootstrap = null;
        }

        private void Refresh(HotUpdateStatus status)
        {
            if (phaseLabel != null)
            {
                phaseLabel.text = status.IsFailure && !string.IsNullOrEmpty(status.Error)
                    ? status.Error
                    : string.IsNullOrEmpty(status.Message)
                        ? PhaseText(status.Phase)
                        : status.Message;
            }

            if (versionLabel != null)
            {
                string version = string.IsNullOrEmpty(status.Version)
                    ? "待确定"
                    : status.Version;
                versionLabel.text = $"来源：{SourceText(status.Source)}    版本：{version}";
            }

            if (fileLabel != null)
            {
                fileLabel.text = string.IsNullOrEmpty(status.CurrentFile)
                    ? PhaseText(status.Phase)
                    : status.CurrentFile;
            }

            if (bytesLabel != null)
            {
                bytesLabel.text = status.TotalBytes == 0
                    ? string.Empty
                    : $"{FormatBytes(status.DownloadedBytes)} / " +
                      $"{FormatBytes(status.TotalBytes)}";
            }

            float progress = status.HasDeterminateProgress
                ? status.Progress
                : status.Phase == HotUpdatePhase.Completed
                    ? 1f
                    : 0f;
            if (progressBar != null)
                progressBar.value = progress;
            if (percentLabel != null)
            {
                percentLabel.text = status.HasDeterminateProgress
                    ? $"{progress * 100f:0}%"
                    : status.Phase == HotUpdatePhase.Completed
                        ? "100%"
                        : string.Empty;
            }

            if (retryButton != null)
                retryButton.gameObject.SetActive(status.Phase == HotUpdatePhase.Failed);
        }

        private void HandleRetry()
        {
            bootstrap?.Retry();
        }

        private void OnDestroy()
        {
            Unbind();
            if (retryButton != null)
                retryButton.onClick.RemoveListener(HandleRetry);
        }

        private static string SourceText(HotUpdateSourceKind source)
        {
            return source switch
            {
                HotUpdateSourceKind.LocalOnly => "本地",
                HotUpdateSourceKind.CurrentRelease => "当前正式版",
                HotUpdateSourceKind.SpecificRelease => "指定正式版",
                HotUpdateSourceKind.StagingVersion => "暂存版",
                _ => source.ToString()
            };
        }

        private static string PhaseText(HotUpdatePhase phase)
        {
            return phase switch
            {
                HotUpdatePhase.Preparing => "正在准备",
                HotUpdatePhase.ResolvingVersion => "正在查询版本",
                HotUpdatePhase.DownloadingManifest => "正在下载清单",
                HotUpdatePhase.ComparingFiles => "正在比较资源",
                HotUpdatePhase.DownloadingBundles => "正在下载资源",
                HotUpdatePhase.VerifyingFiles => "正在校验资源",
                HotUpdatePhase.ActivatingVersion => "正在启用资源",
                HotUpdatePhase.LoadingAssetBundles => "正在加载 AssetBundle",
                HotUpdatePhase.StartingLua => "正在启动 Lua",
                HotUpdatePhase.Fallback => "正在回退本地资源",
                HotUpdatePhase.Completed => "资源准备完成",
                HotUpdatePhase.Failed => "热更新失败",
                _ => "正在启动"
            };
        }

        private static string FormatBytes(ulong value)
        {
            string[] units = { "B", "KB", "MB", "GB" };
            double size = value;
            int unit = 0;
            while (size >= 1024d && unit < units.Length - 1)
            {
                size /= 1024d;
                unit++;
            }

            return unit == 0 ? $"{size:0} {units[unit]}" : $"{size:0.0} {units[unit]}";
        }
    }
}
