using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Net;
using System.IO;
using PauseJenkinsJobs.DataModels;
using CommandLine;

namespace PauseJenkinsJobs
{
    public class Program
    {
        private string mergedCredentials;
        private byte[] byteCredentials;
        private string base64Credentials;
        private const String CLASS_BUILDJOB = "hudson.model.FreeStyleProject";
        private const String CLASS_FOLDER = "com.cloudbees.hudson.plugins.folder.Folder";
        //
        // 全てのジョブを調べて、処理するように。
        // http://jpc00133120.jp.hoge.com/jenkins/api/json??pretty=true
        //
        private const String jenkinsHost = "http://jpc00133120.jp.hoge.com/jenkins";
        List<string> targetJobs = new List<string> {
            "/EverybodysGolf",
            "/Gran_Turismo_SPORT",
            "/System_Update_Server_Configuration_for_PS4",
            "/MonkeyKing"
        };
        Dictionary<string, string> triggerJob = new Dictionary<string, string>
        {
            {"/EverybodysGolf", "CopyAndDownload" },
            {"/Gran_Turismo_SPORT", "GetPackagesFromShareNet" },
            {"/System_Update_Server_Configuration_for_PS4", "Recurring_Task" },
            {"/MonkeyKing", "SyncUpScript" }
        };
        Dictionary<string, int> progressJob = new Dictionary<string, int>
        {
            {"/EverybodysGolf", 0 },
            {"/Gran_Turismo_SPORT", 0 },
            {"/System_Update_Server_Configuration_for_PS4", 0 },
            {"/MonkeyKing", 0 }
        };
        private const String api = "/lastBuild/api/json?tree=building,result,timestamp,estimatedDuration,url,name";
        //private const String api = "/api/json?pretty=true";

        List<Detail> BuildableJobs = new List<Detail>();
        List<Detail> DisableJobs = new List<Detail>();

        private String DISABLE_JOBS_JSON_FILENAME = "jenkins_disable_jobs.json";

        public Program()
        {
            mergedCredentials = string.Format("{0}:{1}", "jp20100", "11d40ed173cff6b1924b1ecde92b92f833");
            byteCredentials = UTF8Encoding.UTF8.GetBytes(mergedCredentials);
            base64Credentials = Convert.ToBase64String(byteCredentials);
        }

        private void PostjenkinsJson(string url, string json)
        {
            WebRequest req = WebRequest.Create(url);
            req.Headers.Add("Authorization", "Basic " + base64Credentials);
            req.Method = "POST";
            using (StreamWriter sw = new StreamWriter(req.GetRequestStream()))
            {
                sw.Write(json);
                sw.Flush();
                sw.Close();
            }
            WebResponse res = req.GetResponse();
            using (StreamReader sr = new StreamReader(res.GetResponseStream()))
            {
                string resString = sr.ReadToEnd();
                sr.Close();
            }
        }
        private WebResponse GetJenkinsJson(string url)
        {
            WebRequest req = WebRequest.Create(url);
            req.Headers.Add("Authorization", "Basic " + base64Credentials);
            return req.GetResponse();
        }

        private LastBuild GetLastBuild(string url)
        {
            LastBuild lastBuild = null;
            WebResponse buildres = GetJenkinsJson(url);
            using (StreamReader sr = new StreamReader(buildres.GetResponseStream()))
            {
                string lastBuildJson = sr.ReadToEnd();
                lastBuild = LitJson.JsonMapper.ToObject<LastBuild>(lastBuildJson);
            }
            return lastBuild;
        }

        private Detail GetDetail(string url)
        {
            Detail detail = null;
            WebResponse detailres = GetJenkinsJson(url);
            using (StreamReader sr = new StreamReader(detailres.GetResponseStream()))
            {
                string detailJson = sr.ReadToEnd();
                detail = LitJson.JsonMapper.ToObject<Detail>(detailJson);
            }
            return detail;
        }

        private bool CheckBuilding(string url)
        {
            bool result = false;
            string treejson = "";
            //Console.WriteLine("GetJenkinsJson url: "+url);
            WebResponse res = GetJenkinsJson(url);
            using (StreamReader sr = new StreamReader(res.GetResponseStream()))
            {
                treejson = sr.ReadToEnd();
            }
            Tree tree = LitJson.JsonMapper.ToObject<Tree>(treejson);
            foreach (Jobs job in tree.jobs)
            {
                //Console.WriteLine(string.Format("name:{0} class:{1}", job.name, job._class));
                if (job._class.Equals(CLASS_FOLDER))
                {
                    bool nextNode = CheckBuilding(job.url.TrimEnd('/')+"/api/json");
                    if (nextNode) return nextNode;
                }
                else if (job._class.Equals(CLASS_BUILDJOB))
                {
                    Detail detail = GetDetail(job.url.TrimEnd('/')+"/api/json");
                    if (detail.buildable)
                    {
                        if (detail.builds.Count > 0)
                        {
                            LastBuild lastBuild = GetLastBuild(job.url.TrimEnd('/') + api);
                            if (lastBuild.building)
                            {
                                Console.WriteLine(string.Format("name:{0} url{1} isBuilding!", detail.name, lastBuild.url));
                                return true;
                            }
                            else if (detail.upstreamProjects.Count == 0)
                            {
                                DisableJobs.Add(detail);
                            }
                        }
                        else if (detail.upstreamProjects.Count == 0)
                        {
                            DisableJobs.Add(detail);
                        }
                    }
                }
            }

            return result;
        }

        public int runDisable()
        {
            BuildableJobs.Clear();
            DisableJobs.Clear();
            string jsonfile = Path.Combine(Environment.CurrentDirectory, DISABLE_JOBS_JSON_FILENAME);
            // disable jsonファイルがある場合はdisable処理を2回以上実行したときとして、
            // disableしたジョブを復帰できなくなるので処理を中止
            if (File.Exists(jsonfile)) return 1;

            /*
            string joburl = jenkinsHost + "/job/dart_training/job/test01";
            PostjenkinsJson(joburl.TrimEnd('/') + "/disable", "{}");
            Detail detail = GetDetail(joburl.TrimEnd('/') + "/api/json");
            DisableJobs.Add(detail);
            using (StreamWriter sw = new StreamWriter(jsonfile, false, Encoding.GetEncoding("UTF-8")))
            {
                sw.Write(LitJson.JsonMapper.ToJson(DisableJobs));
                sw.Flush();
                sw.Close();
            }
            Environment.Exit(0);
            */
            do
            {
                string TopUrl = jenkinsHost + "/api/json";
                if (CheckBuilding(TopUrl))
                {
                    // 処理を実行しないので、disableしたジョブはenableに戻す。
                    foreach (Detail item in DisableJobs)
                    {
                        PostjenkinsJson(item.url + "enable", "{}");
                    }
                    System.Threading.Thread.Sleep(5000);    // wait 5 sec
                }
                else
                {
                    // アカウントパスワード更新処理を行うので、あとで戻せるようにdisableしたジョブをjsonにして保存
                    using (StreamWriter sw = new StreamWriter(jsonfile, false, Encoding.GetEncoding("UTF-8")))
                    {
                        sw.Write(LitJson.JsonMapper.ToJson(DisableJobs));
                        sw.Flush();
                        sw.Close();
                    }
                    break;
                }
            } while (true);
            return 0;
        }

        public int runEnable()
        {
            string json = "";
            using (StreamReader sr = new StreamReader(Path.Combine(Environment.CurrentDirectory, DISABLE_JOBS_JSON_FILENAME), Encoding.GetEncoding("UTF-8")))
            {
                json = sr.ReadToEnd();
                sr.Close();
            }
            List<Detail> disable_jobs = LitJson.JsonMapper.ToObject<List<Detail>>(json);
            foreach (Detail job in disable_jobs)
            {
                PostjenkinsJson(job.url.TrimEnd('/') + "/enable", "{}");
            }
            return 0;
        }

        static void Main(string[] args)
        {
            Parser parser = new Parser();
            CommandLine.ParserResult<CommandLineOptions> result = parser.ParseArguments<CommandLineOptions>(args);
            if (result.Tag == CommandLine.ParserResultType.Parsed)
            {
                var parsed = (CommandLine.Parsed<CommandLineOptions>)result;
                Program proc = new Program();
                if (parsed.Value.mode == CommandLineOptions.OptionMode.enable)
                {
                    Environment.Exit(proc.runEnable());
                }
                else
                {
                    // disable
                    Environment.Exit(proc.runDisable());
                }
            }
            else
            {
                var notparsed = (CommandLine.Parsed<CommandLineOptions>)result;
                Console.WriteLine("Optional mistake");
                Environment.Exit(1);
            }
        }
    }
}
