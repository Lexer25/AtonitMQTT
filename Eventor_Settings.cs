using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Eventor
{
    public class Eventor_Settings
    {
        public string dbconfig { get; set; }
        public string broker { get; set; }
        public int port { get; set; }
        public string topic {  get; set; }
        public string username {  get; set; }
        public string password { get; set; }
        public required string liveTopic { get; set; }
        public List<string> event_reaction { get; set; }
        public TimeSpan breaktime {  get; set; }
        public Dictionary<int, String> idmap { get; set; }
        public List<int> id_eventtype { get; set; }
    }
}
