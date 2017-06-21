using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using log4net;

namespace ApiTimeTrack
{
    public static class SharedClass
    {
        private static ILog _logger = null;
        private static ILog _dumpLogger = null;
        private static Dictionary<string, string> _apiHosts = new Dictionary<string,string>();
        private static bool _hasStopSignal = true;
        public static void InitializeLogger()
        {
            GlobalContext.Properties["LogName"] = DateTime.Now.ToString("yyyyMMdd");
            log4net.Config.XmlConfigurator.Configure();
            _logger = LogManager.GetLogger("Log");
            _dumpLogger = LogManager.GetLogger("DumpLogger");
        }
        public static void AddApiHost(string apiHostName, string connectionString)
        {
            lock (_apiHosts)
            {
                _apiHosts.Add(apiHostName, connectionString);
            }
        }
        public static string GetConnectionString(string apiHostName)
        {
            lock (_apiHosts)
            {
                if (!_apiHosts.ContainsKey(apiHostName))
                    throw new KeyNotFoundException(string.Format("{0} not found in the dictionary"));
                return _apiHosts[apiHostName];
            }
        }
        public static ILog Logger
        {
            get { if (_logger == null) { InitializeLogger(); } return _logger; }
        }
        public static ILog DumpLogger
        {
            get { if (_dumpLogger == null) { InitializeLogger(); } return _dumpLogger; }
        }
        public static Dictionary<string, string> ApiHosts
        {
            get { if (_apiHosts == null) { _apiHosts = new Dictionary<string, string>(); } return _apiHosts; }
        }
        public static bool HasStopSignal
        {
            get { return _hasStopSignal; }
            set { _hasStopSignal = value; }
        }
    }
}
