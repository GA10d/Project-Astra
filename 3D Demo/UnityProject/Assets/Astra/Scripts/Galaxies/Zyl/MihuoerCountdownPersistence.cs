using AstraCabin.Navigation;
namespace AstraCabin.Galaxies.Zyl
{
    public static class MihuoerCountdownPersistence
    {
        // Same-path session replacement means import/debug replay already wrote a new voyage.
        // Never flush the old in-memory clock over that file; Restart also replaces the snapshot reference.
        public static bool CanFlush(NavigationSession bound,NavigationSession active,MihuoerCountdownClock clock)
        {
            return bound!=null&&clock!=null&&ReferenceEquals(bound.Save.mihuoerCountdown,clock.State)&&
                !(active!=null&&active!=bound&&active.Path==bound.Path);
        }
    }
}
