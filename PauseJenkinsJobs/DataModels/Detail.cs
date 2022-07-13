using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PauseJenkinsJobs.DataModels
{
    public class Detail
    {
        public String _class;
        public String fullName;
        public String name;
        public String url;
        public bool buildable;
        public List<Builds> builds;
        public List<Jobs> downstreamProjects;
        public List<Jobs> upstreamProjects;
    }
}
