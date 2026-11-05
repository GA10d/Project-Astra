using UnityEngine;
namespace AstraCabin.Galaxies.Zyl
{
    /// <summary>Scene/Timeline/Animation-event receiver; resolve the voyage clock at the time of the cue.</summary>
    public sealed class MihuoerCountdownCue : MonoBehaviour
    {
        public CountdownAction action=new CountdownAction();
        public void Execute()
        {
            if(!Application.isPlaying)return;
            var timer=FindObjectOfType<MihuoerCountdown>();
            if(timer)timer.Apply(action);else Debug.LogWarning("当前没有活动的zyl倒计时控制器。");
        }
    }
}
