using System;

public enum HotUpdateSourceKind
{
    LocalOnly = 0,
    CurrentRelease = 1,
    SpecificRelease = 2,
    StagingVersion = 3
}

public enum HotUpdatePhase
{
    None = 0,
    Preparing = 1,
    ResolvingVersion = 2,
    DownloadingManifest = 3,
    ComparingFiles = 4,
    DownloadingBundles = 5,
    VerifyingFiles = 6,
    ActivatingVersion = 7,
    LoadingAssetBundles = 8,
    StartingLua = 9,
    Fallback = 10,
    Completed = 11,
    Failed = 12
}

public readonly struct HotUpdateStatus
{
    public HotUpdateStatus(
        HotUpdatePhase phase,
        HotUpdateSourceKind source,
        string version,
        string currentFile,
        int completedFiles,
        int totalFiles,
        ulong downloadedBytes,
        ulong totalBytes,
        string message,
        string error)
    {
        Phase = phase;
        Source = source;
        Version = version ?? string.Empty;
        CurrentFile = currentFile ?? string.Empty;
        CompletedFiles = completedFiles;
        TotalFiles = totalFiles;
        DownloadedBytes = downloadedBytes;
        TotalBytes = totalBytes;
        Message = message ?? string.Empty;
        Error = error ?? string.Empty;
    }

    public HotUpdatePhase Phase { get; }
    public HotUpdateSourceKind Source { get; }
    public string Version { get; }
    public string CurrentFile { get; }
    public int CompletedFiles { get; }
    public int TotalFiles { get; }
    public ulong DownloadedBytes { get; }
    public ulong TotalBytes { get; }
    public string Message { get; }
    public string Error { get; }
    public bool IsFailure => Phase == HotUpdatePhase.Failed;
    public bool HasDeterminateProgress => TotalBytes > 0;
    public float Progress => TotalBytes == 0
        ? 0f
        : Math.Min(1f, (float)DownloadedBytes / TotalBytes);

    public static HotUpdateStatus Create(
        HotUpdatePhase phase,
        HotUpdateSourceKind source,
        string version = "",
        string message = "",
        string error = "")
    {
        return new HotUpdateStatus(
            phase,
            source,
            version,
            string.Empty,
            0,
            0,
            0,
            0,
            message,
            error);
    }
}

public readonly struct HotUpdateEndpoint
{
    public HotUpdateEndpoint(
        HotUpdateSourceKind source,
        string version,
        string rootUrl,
        bool remote)
    {
        Source = source;
        Version = version ?? string.Empty;
        RootUrl = rootUrl == null ? string.Empty : rootUrl.TrimEnd('/');
        Remote = remote;
    }

    public HotUpdateSourceKind Source { get; }
    public string Version { get; }
    public string RootUrl { get; }
    public bool Remote { get; }
}

#if UNITY_EDITOR
[Serializable]
public sealed class HotUpdateEditorPlaySettingsData
{
    public HotUpdateSourceKind source = HotUpdateSourceKind.LocalOnly;
    public string version = "0.1.0";
    public string stagingBaseUrl = "http://127.0.0.1:18081/staging";
}
#endif
