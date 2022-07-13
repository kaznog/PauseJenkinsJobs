using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using CommandLine;

namespace PauseJenkinsJobs
{
    class CommandLineOptions
    {
        public enum OptionMode
        {
            enable,
            disable
        }
        [CommandLine.Option('m')]
        public OptionMode mode
        {
            get;
            set;
        }
    }
}
