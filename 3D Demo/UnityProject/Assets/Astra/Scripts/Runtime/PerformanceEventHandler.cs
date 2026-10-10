using System.Text.RegularExpressions;
namespace AstraCabin.Navigation
{
    public sealed class PerformanceEventHandler : IEventHandler
    {
        public string Type => "performance";
        public bool NeedsObservation => true;
        public string Validate(EventDefinition e)
        {
            if(string.IsNullOrEmpty(e.performanceId)||!Regex.IsMatch(e.performanceId,"^[A-Za-z0-9_-]{1,64}$"))return "会议需要有效的演出包 ID";
            if(e.choices==null||e.choices.Length!=1||e.choices[0]==null||e.choices[0].costs==null||e.choices[0].costs.Length!=0)return "会议必须有且仅有一个无消耗的自动结算项";
            return null;
        }
        public void Begin(NavigationMode host,EventDefinition e) { host.GetComponent<CabinComputer>().Meeting.Begin(e); }
        public bool IsComplete(NavigationMode host,EventDefinition e) => false;
    }
}
