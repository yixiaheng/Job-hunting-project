using UnityEngine;

public class PlayerAnimation : MonoBehaviour
{
    public Animator animator{get; private set;}
    public bool isGrounded = true;


    void Awake()
    {
        isGrounded = true;
        animator = GetComponentInChildren<Animator>();
    }
    


    /// <summary>
    /// 轮巡判断动画是否播放完毕
    /// </summary>
    /// <param name="stateName">动画Animation Name</param>
    /// <param name="earlyExitTime">提早退出时间</param>
    /// <returns></returns>
    public bool IsAnimationFinished(
        string stateName,
        float earlyExitTime = 0f)
    {
        AnimatorStateInfo info = animator.GetCurrentAnimatorStateInfo(0);

        if (animator.IsInTransition(0) || !info.IsName(stateName))
            return false;

        float totalTime = info.length;
        float currentTime = info.normalizedTime * totalTime;

        return currentTime >= totalTime - earlyExitTime;
    }
}
