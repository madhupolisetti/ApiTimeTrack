using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ApiTimeTrack
{
    public class ApplicationController
    {
        private RabitMQClient _rabitMQClient = null;
        public ApplicationController()
        {
            SharedClass.Logger.Info("Initializing Application");
            this.LoadConfig();
        }
        public void StartService()
        {
            SharedClass.HasStopSignal = false;
            if (this._rabitMQClient == null)
                throw new Exception("RabitMQClient object is null");
            this._rabitMQClient.Initialize();
        }
        public void StopService()
        {
            SharedClass.HasStopSignal = true;
            this._rabitMQClient.Stop();
            while (this._rabitMQClient.IsRunning)
            {
                SharedClass.Logger.Info("RabitMQClient is still running");
            }
            SharedClass.Logger.Info("Application Stopped");
        }
        private void LoadConfig()
        {
            System.Xml.XmlDocument xmlDoc = new System.Xml.XmlDocument();
            string filePath = string.Empty;
            System.Xml.XmlElement configElement = null;
            System.Xml.XmlElement apiHostsElement = null;
            System.Xml.XmlElement rabitMQElement = null;
            try
            {
                filePath = System.IO.Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location) + "/CustomConfig.xml";
                SharedClass.Logger.Info(string.Format("Reading Config File {0}", filePath));
                if (!System.IO.File.Exists(filePath))
                    throw new System.IO.FileNotFoundException(string.Format("File {0} Not Found.", filePath));
                xmlDoc.Load(filePath);
                configElement = (System.Xml.XmlElement)xmlDoc.SelectSingleNode("Config");
                if (configElement == null)
                    throw new System.Xml.XmlException("Config element not found.");
                if(!configElement.HasChildNodes)
                    throw new System.Xml.XmlException("Config element has no child nodes.");
                apiHostsElement = (System.Xml.XmlElement)configElement.SelectSingleNode("ApiHosts");
                if (apiHostsElement == null)
                    throw new System.Xml.XmlException("ApiHosts element not found.");
                if(!apiHostsElement.HasChildNodes)
                    throw new System.Xml.XmlException("ApiHosts element has no child nodes.");
                foreach (System.Xml.XmlElement apiHost in apiHostsElement.ChildNodes)
                    SharedClass.AddApiHost(apiHost.Attributes["name"].Value, apiHost.Attributes["connectionString"].Value);
                rabitMQElement = (System.Xml.XmlElement)configElement.SelectSingleNode("RabitMQ");
                if(rabitMQElement == null)
                    throw new System.Xml.XmlException("RabitMQ element not found.");
                this._rabitMQClient = new RabitMQClient();
                foreach (System.Xml.XmlElement propertyElement in rabitMQElement.ChildNodes)
                {
                    switch (propertyElement.Name)
                    { 
                        case "Host":
                            this._rabitMQClient.Host = propertyElement.InnerText;
                            break;
                        case "Port":
                            this._rabitMQClient.Port = Convert.ToUInt16(propertyElement.InnerText.Trim());
                            break;
                        case "UserName":
                            this._rabitMQClient.UserName = propertyElement.InnerText;
                            break;
                        case "Password":
                            this._rabitMQClient.Password = propertyElement.InnerText;
                            break;
                        default:
                            break;
                    }
                }
            }
            catch (Exception e)
            {
                SharedClass.Logger.Error(string.Format("Exception Reading Config File. {0}", e.ToString()));
                throw;
            }
            finally
            {
                xmlDoc = null;
            }
        }
        private void LoadConfig_Test()
        {
            //System.Collections.Hashtable config = (System.Collections.Hashtable)System.Configuration.ConfigurationSettings.GetConfig("myCustomGroup/myCustomSection");
            System.Collections.Hashtable config = null;
            try
            {
                config = (System.Collections.Hashtable)System.Configuration.ConfigurationManager.GetSection("configuration/configSections/section[name='apiHosts']");
            }
            catch (Exception e)
            {
                Console.WriteLine(e.ToString());
                Console.ReadLine();
            }
            foreach (KeyValuePair<object, object> kvp in config)
            {
                Console.WriteLine(kvp.Key);
                Console.WriteLine(kvp.Value);
            }
        }
    }
}
