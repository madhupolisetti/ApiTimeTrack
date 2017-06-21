using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Threading;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using System.Xml;
using Newtonsoft.Json.Linq;
using System.Data;
using System.Data.SqlClient;

namespace ApiTimeTrack
{
    public class RabitMQClient
    {
        private string _host = "127.0.0.1";
        private int _port = 5672;
        private string _userName = "guest";
        private string _password = "guest";
        private bool _isConnected = false;
        private bool _isConnectSignalInProgress = false;
        private Thread _deQueueThread = null;
        private ConnectionFactory _connectionFactory = null;
        private IConnection _connection = null;
        private IModel _channel = null;
        private QueueingBasicConsumer _consumer = null;
        private string _consumerTag = string.Empty;
        private XmlDocument _xmlDoc = null;
        private XmlElement _rootElement = null;
        private JObject _jObj = null;
        private ushort _retryAttempt = 0;
        private SqlConnection _sqlConnection = null;
        private SqlCommand _sqlCommand = null;
        private bool _isSubscriberRunning = false;

        public void Initialize()
        {
            this._xmlDoc = new XmlDocument();
            this._rootElement = this._xmlDoc.CreateElement("Data");
            this._xmlDoc.AppendChild(this._rootElement);
            this._sqlConnection = new SqlConnection();
            this._sqlCommand = new SqlCommand("ApiEndPointTimeInsert");
            this._sqlCommand.CommandType = CommandType.StoredProcedure;
            this._deQueueThread = new Thread(new System.Threading.ThreadStart(this.StartDeQueuing));
            this._deQueueThread.Name = "Tracker";
            this._deQueueThread.Start();
            ConnectToServer();
        }
        public void StartDeQueuing()
        {
            while (!this._isConnected && !SharedClass.HasStopSignal)
            {
                SharedClass.Logger.Info("Waiting for a CONNECT_SIG");
                Thread.Sleep(5000);
            }
            if (SharedClass.HasStopSignal)
            {
                SharedClass.Logger.Info("Service has STOP_SIG received, not dequeuing, terminating DeQueue Thread");
            }
            else
            {
                this._isSubscriberRunning = true;
                SharedClass.Logger.Info("Started");
                BasicDeliverEventArgs deliverEventArgs = null;
                while (!SharedClass.HasStopSignal)
                {
                    try
                    {
                        deliverEventArgs = null;
                        this._consumer.Queue.Dequeue(5000, out deliverEventArgs);
                        if (deliverEventArgs != null)
                        {
                            string message = System.Text.Encoding.UTF8.GetString(deliverEventArgs.Body);
                            this._jObj = JObject.Parse(message);
                            this._rootElement.RemoveAll();
                            foreach (JProperty property in this._jObj.Properties())
                                this._rootElement.SetAttribute(property.Name, property.Value.ToString());
                            Insert();
                        }
                    }
                    catch (System.IO.EndOfStreamException e)
                    {
                        SharedClass.Logger.Error(string.Format("EndOfStream Exception, Reason : {0}, HasStopSignal : {1}, IsConnected : {2}", e.ToString(), SharedClass.HasStopSignal, this._isConnected));
                        if (!SharedClass.HasStopSignal)
                        {
                            this.ConnectToServer();
                            while (!this._isConnected && !SharedClass.HasStopSignal)
                            {
                                try
                                {
                                    Thread.Sleep(5000);
                                }
                                catch (ThreadInterruptedException te) { }
                                catch (ThreadStateException te) { }
                                catch (ThreadAbortException te) { }
                            }
                            if (!SharedClass.HasStopSignal)
                                SharedClass.Logger.Info("Started DeQueuing Again");
                        }
                    }
                    catch (Exception e)
                    {
                        SharedClass.Logger.Info(string.Format("Exception ==> {0}", e.ToString()));
                        if (deliverEventArgs != null)
                            this._channel.BasicAck(deliverEventArgs.DeliveryTag, false);
                        if (!SharedClass.HasStopSignal)
                        {
                            try
                            {
                                Thread.Sleep(5000);
                            }
                            catch (ThreadInterruptedException ex2) { }
                            catch (ThreadStateException ex2) { }
                            catch (ThreadAbortException ex2) { }
                        }
                    }
                } // While Loop End
                SharedClass.Logger.Info("Exit");
                this._isSubscriberRunning = false;
            }
        }
        private void ConnectToServer()
        {   
            int timeOutInMilliSeconds = 3000;
            if (this._isConnectSignalInProgress)
                SharedClass.Logger.Info("A CONNECT_SIG already in progress. Terminating request");
            else
            {
                this._isConnectSignalInProgress = true;
                this._isConnected = false;
                SharedClass.Logger.Info(string.Format("Trying connecting to RabitMQ-Server. Host : {0}, Port : {1}, User : {2}, Password : {3}", this._host, this._port, this._userName, this._password));
                while (!SharedClass.HasStopSignal)
                {
                    try
                    {
                        this._connectionFactory = new ConnectionFactory();
                        this._connectionFactory.HostName = this._host;
                        this._connectionFactory.Port = this._port;
                        this._connectionFactory.UserName = this._userName;
                        this._connectionFactory.Password = this._password;
                        this._connection = this._connectionFactory.CreateConnection();
                        this._channel = this._connection.CreateModel();
                        this._channel.QueueDeclare("ApiTimeTrack", true, false, false, null);
                        this._channel.BasicQos(0, 1, false);
                        this._consumer = new QueueingBasicConsumer(this._channel);
                        this._consumerTag = this._channel.BasicConsume("ApiTimeTrack", false, this._consumer);
                        SharedClass.Logger.Info(string.Format("ApiTimeTrack Queue Consumer Created, ConsumerTag : {0}", this._consumerTag));
                        SharedClass.Logger.Info("Connected to RabbitMQ-Server Successfully");
                        this._isConnected = true;                        
                        break;
                    }
                    catch (Exception e)
                    {
                        SharedClass.Logger.Error(string.Format("Exception ==> Error Connecting to RabitMQ-Server. Reason : {0}", e.ToString()));
                        if (timeOutInMilliSeconds > 12000)
                            timeOutInMilliSeconds = 3000;
                        else
                            timeOutInMilliSeconds *= 2;
                        try
                        {
                            System.Threading.Thread.Sleep(timeOutInMilliSeconds);
                        }
                        catch (Exception se)
                        {
                            SharedClass.Logger.Error(string.Format("Error In Thread Sleep Block Of RMQConnection Intialization, Reason : {0}", se.ToString()));
                        }
                    }
                }
                this._isConnectSignalInProgress = false;
            }
        }
        private void Insert()
        {
            this._retryAttempt = 0;
            while (this._retryAttempt <= 3)
            {
                try
                {
                    if (!this._sqlConnection.ConnectionString.Equals(SharedClass.GetConnectionString(this._jObj.SelectToken(Constants.API_HOST).ToString())))
                        this._sqlConnection.ConnectionString = SharedClass.GetConnectionString(this._jObj.SelectToken(Constants.API_HOST).ToString());
                    this._sqlCommand.Parameters.Clear();
                    this._sqlCommand.Connection = this._sqlConnection;
                    this._sqlCommand.Parameters.Add(Constants.DataBaseParameters.DATA, SqlDbType.Xml, this._xmlDoc.InnerXml.Length).Value = this._xmlDoc.InnerXml;
                    this._sqlCommand.Parameters.Add(Constants.DataBaseParameters.SUCCESS, SqlDbType.Bit).Direction = ParameterDirection.Output;
                    this._sqlCommand.Parameters.Add(Constants.DataBaseParameters.MESSAGE, SqlDbType.VarChar, 1000).Direction = ParameterDirection.Output;
                    if (this._sqlConnection.State != ConnectionState.Open)
                        this._sqlConnection.Open();
                    this._sqlCommand.ExecuteNonQuery();
                    if (Convert.ToBoolean(this._sqlCommand.Parameters[Constants.DataBaseParameters.SUCCESS].Value))
                        break;
                    else
                        SharedClass.Logger.Error(string.Format("Unable To Insert. Reason {0}", this._sqlCommand.Parameters[Constants.DataBaseParameters.MESSAGE].Value));
                }
                catch (Exception e)
                {
                    ++this._retryAttempt;
                    SharedClass.Logger.Error(string.Format("Exception While Inserting [RetryAttempt - {0}]. {1}", this._retryAttempt, e.ToString()));
                }
            }
        }
        public void Stop()
        {
            try
            {
                if (this._connection.IsOpen)
                {
                    this._connection.Close();
                    SharedClass.Logger.Info("RabbitMQ Connection Closed");
                }
                else
                    SharedClass.Logger.Info("RabbitMQ Connection is not in open state. Unable to issue Close command");
                this._connection = null;
                this._connectionFactory = null;
                this._channel = null;
            }
            catch (Exception ex)
            {
                SharedClass.Logger.Error("Error Stopping RabbitMQClient, " + ex.ToString());
            }
            finally
            {
                
            }
        }

        #region PROPERTIES
        public string Host
        {
            get { return this._host; }
            set { this._host = value; }
        }
        public int Port
        {
            get { return this._port; }
            set { this._port = value; }
        }
        public string UserName
        {
            get { return this._userName; }
            set { this._userName = value; }
        }
        public string Password
        {
            get { return this._password; }
            set { this._password = value; }
        }
        public bool IsConnected
        {
            get { return this._isConnected; }
            set { this._isConnected = value; }
        }
        public bool IsConnectSignalInProgress
        {
            get { return this._isConnectSignalInProgress; }
            set { this._isConnectSignalInProgress = value; }
        }
        public bool IsRunning
        {
            get { return this._isSubscriberRunning; }            
        }
        #endregion
    }
}
