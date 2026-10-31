/// <summary>稳定的场景身份，与场景文件名及 Build Settings 顺序无关。</summary>
public enum SceneId
{
    None = -1,
    Sample = 0,
    Battle = 1,
    Party = 2
}

public enum SceneTransitionMode
{
    Immediate = 0,
    Fade = 1
}
