using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Diagnostics;
using System.Linq;
using System.ServiceProcess;
using System.Text;
using System.Threading.Tasks;

namespace ApiTimeTrack
{
    public partial class ApiTimeTrackService : ServiceBase
    {
        ApplicationController _applicationController = null;
        System.Threading.Thread _appThread = null;
        public ApiTimeTrackService()
        {
            InitializeComponent();
        }

        protected override void OnStart(string[] args)
        {
            _applicationController = new ApplicationController();
            _appThread = new System.Threading.Thread(new System.Threading.ThreadStart(_applicationController.StartService));
            _appThread.Name = "Main";
            _appThread.Start();
        }

        protected override void OnStop()
        {
            _applicationController.StopService();
        }
    }
}
