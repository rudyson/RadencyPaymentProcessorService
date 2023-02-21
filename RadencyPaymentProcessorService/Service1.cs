using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Configuration;
using System.Data;
using System.Diagnostics;
using System.Linq;
using System.ServiceProcess;
using System.Text;
using System.Threading.Tasks;

namespace RadencyPaymentProcessorService
{
    public partial class Service1 : ServiceBase
    {
        private string input_source_path = string.Empty;
        private string output_source_path = string.Empty;
        public Service1()
        {
            InitializeComponent();
        }

        protected override void OnStart(string[] args)
        {
            this.ReadConfiguration();
        }

        protected override void OnStop()
        {
        }
        private void ReadConfiguration()
        {
            input_source_path = ConfigurationManager.AppSettings["SourcePath"];
            output_source_path = ConfigurationManager.AppSettings["ProcessedDataPath"];
        }
    }
}
