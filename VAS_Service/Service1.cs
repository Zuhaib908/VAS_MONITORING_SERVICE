using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.ServiceProcess;
using System.Text;
using System.Timers;
using Newtonsoft.Json.Linq;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using StackExchange.Redis;
using System.Configuration;
using VAS_Service.DAL;
using Serilog;

namespace VAS_Service
{
    public partial class Service1 : ServiceBase
    {
        private Timer _updateTimer;
        private Dictionary<int, int> DicGetAllCounts;
        private Dictionary<int, string> DicChangeColor;

        private IConnection rabbitConnection;
        private IModel rabbitChannel;
        private ConnectionMultiplexer redisConnection;
        private IDatabase redisDatabase;
        private string redisHostIp;
        private string redisPort;
        private string redisPassword;
        private long redisTimer;
        private string hashKeyForCount;
        private string hashKeyForColor;
        private ushort prefetchCount;
        public Service1()
        {
            InitializeComponent();
            LogsConfiguration ObjLogs = new LogsConfiguration();
            ObjLogs.GenerateLogs();
        }
        protected override void OnStart(string[] args)
        {
            try
            {
                Log.Information("Info-Application start11");

                redisHostIp = ConfigurationSettings.AppSettings["RedisHostIp"].ToString();
                redisPort = ConfigurationSettings.AppSettings["RedisPort"].ToString();
                redisPassword = ConfigurationSettings.AppSettings["RedisPassword"].ToString();
                redisTimer = long.Parse(ConfigurationSettings.AppSettings["RedisTimer"]);
                hashKeyForCount = ConfigurationSettings.AppSettings["hashKeyForCount"].ToString();
                hashKeyForColor = ConfigurationSettings.AppSettings["hashKeyForColor"].ToString();
                prefetchCount = ushort.Parse(ConfigurationSettings.AppSettings["Prefetch"].ToString()); 

                InitializeDictionary();
                InitializeRabbitMQ();
                InitializeRedis();
                LoadDataFromRedis();

                _updateTimer = new Timer
                {
                    Interval = redisTimer 
                };
                _updateTimer.Elapsed += TimerElapsed;
                _updateTimer.AutoReset = true;
                _updateTimer.Start();
            }
            catch (Exception ex)
            {
                Log.Error("Error on Start=" + ex.Message + "--StackTrace--" + ex.StackTrace);
            }
        }
        protected override void OnStop()
        {
            try
            {
                CleanUp();
            }
            catch (Exception ex)
            {
                Log.Error("Error on Stop=" + ex.Message + "--StackTrace--" + ex.StackTrace);
            }
        }
        private void TimerElapsed(object sender, ElapsedEventArgs e)
        {
            try
            {
                SendDataToRedis(sender, e);
            }
            catch (Exception ex)
            {
                Log.Error("Error on timer Elapsed=" + ex.Message + "--StackTrace--" + ex.StackTrace);
            }
        }
        private void InitializeDictionary()
        {
            try
            {
                DicGetAllCounts = new Dictionary<int, int>();
                DicChangeColor = new Dictionary<int, string>();

                for (int i = 0; i <= 1439; i++)
                {
                    DicGetAllCounts[i] = 0;
                    DicChangeColor[i] = "R";
                }
            }
            catch (Exception ex)
            {
                EventLog.WriteEntry($"Error initializing dictionary: {ex.Message}", EventLogEntryType.Error);
            }
        }
        private void InitializeRabbitMQ()
        {
            try
            {
                var factory = new ConnectionFactory
                {
                    HostName = "localhost",
                    UserName = "guest",
                    Password = "guest",
                    Port = 5672,
                    VirtualHost = "M3-TECH"
                };

                rabbitConnection = factory.CreateConnection();
                rabbitChannel = rabbitConnection.CreateModel();

                rabbitChannel.ExchangeDeclare(exchange: "EX.M3TECH", 
                                               type: ExchangeType.Direct, 
                                               durable: true);

                rabbitChannel.QueueDeclare(queue: "M3.COMPONANT.INFO",
                                           durable: true,
                                           exclusive: false,
                                           autoDelete: false,
                                           arguments: null);

                rabbitChannel.QueueBind(queue: "M3.COMPONANT.INFO",
                                        exchange: "EX.M3TECH",
                                        routingKey: "M3.COMPONANT.INFO");

                rabbitChannel.BasicQos(prefetchSize: 0, prefetchCount: prefetchCount, global: false); 
                var consumer = new EventingBasicConsumer(rabbitChannel);
                consumer.Received += (model, ea) =>
                {
                    try
                    {
                        var body = ea.Body.ToArray();
                        var message = Encoding.UTF8.GetString(body);
                        HandleMessage(message);
                        rabbitChannel.BasicAck(deliveryTag: ea.DeliveryTag, multiple: false);  
                    }
                    catch (Exception ex)
                    {
                        Log.Error("Error initializing RabbitMQ=" + ex.Message + "--StackTrace--" + ex.StackTrace);
                    }
                };
                rabbitChannel.BasicConsume(queue: "M3.COMPONANT.INFO",
                                           autoAck: false,
                                           consumer: consumer);
            }
            catch (Exception ex)
            {
                Log.Error("Error initializing RabbitMQ=" + ex.Message + "--StackTrace--" + ex.StackTrace);
            }
        }
        private void InitializeRedis()
        {
            try
            {
                var configurationOptions = new ConfigurationOptions
                {

                    EndPoints = { redisHostIp + ":" + redisPort },
                    Password = redisPassword
                };
                redisConnection = ConnectionMultiplexer.Connect(configurationOptions);
                redisDatabase = redisConnection.GetDatabase();
            }
            catch (Exception ex)
            {
                Log.Error("Error initializing Redis=" + ex.Message + "--StackTrace--" + ex.StackTrace);
            }
        }
        private void LoadDataFromRedis()
        {
            try
            {
                bool keyExists = redisDatabase.KeyExists(hashKeyForCount);

                if (!keyExists)
                {
                    var HashCountDictionary = new List<HashEntry>();
                    for (int i = 0; i < DicGetAllCounts.Count - 1; i++)
                    {
                        var key = i.ToString();
                        var value = 0;
                        HashCountDictionary.Add(new HashEntry(key, value));
                    }
                    redisDatabase.HashSet(hashKeyForCount, HashCountDictionary.ToArray());

                    var HashColorDictionary = new List<HashEntry>();
                    for (int i = 0; i < DicChangeColor.Count - 1; i++)
                    {
                        var key = i.ToString();
                        var value = "R";
                        HashColorDictionary.Add(new HashEntry(key, value));
                    }
                    redisDatabase.HashSet(hashKeyForColor, HashColorDictionary.ToArray());
                }

                var hashEntriesForCount = redisDatabase.HashGetAll(hashKeyForCount);

                DicGetAllCounts.Clear();
                foreach (var entry in hashEntriesForCount)
                {
                    if (int.TryParse(entry.Name, out int key) && int.TryParse(entry.Value, out int value))
                    {
                        DicGetAllCounts[key] = value;
                    }
                    else
                    {
                        Log.Error($"Failed to parse key or value: Key = {entry.Name}, Value = {entry.Value}", EventLogEntryType.Warning);
                    }
                }

                var sortedDicGetAllCounts = DicGetAllCounts
                   .OrderBy(entry => entry.Key) 
                   .ToDictionary(entry => entry.Key, entry => entry.Value);
            }
            catch (Exception ex)
            {
                Log.Error("Error on Load data=" + ex.Message + "--StackTrace--" + ex.StackTrace);
            }
        }
        private void HandleMessage(string message)
        {
            try
            {
                JObject jsonObject = JObject.Parse(message);
                string appStartTimeStr = (string)jsonObject["APP_START_TIME"];
                DateTime appStartTime;

                if (DateTime.TryParseExact(appStartTimeStr, "yyyyMMdd HH:mm:ss", null, System.Globalization.DateTimeStyles.None, out appStartTime))
                {
                    int appStartMinutes = (appStartTime.Hour * 60) + appStartTime.Minute;
                    DicGetAllCounts[appStartMinutes]++;
                }
            }
            catch (Exception ex)
            {
                Log.Error("Error on Handle Message=" + ex.Message + "--StackTrace--" + ex.StackTrace);
            }
        }
        private void SendDataToRedis(object sender, ElapsedEventArgs e)
        {
            try
            {
                var hashEntries = new List<HashEntry>();

                for (int i = 0; i <= DicChangeColor.Count - 1; i++)
                {
                    int key1 = DicChangeColor.Keys.ElementAt(i);
                    int getValueFromCount = 0;
                     
                    if (DicGetAllCounts.TryGetValue(key1, out int GetValue))
                    {
                        getValueFromCount = GetValue;
                    }
                    else
                    {
                        getValueFromCount = i;
                    }

                    var GetFlag = GetFormattedValue(getValueFromCount);
                    var key = i.ToString(); 
                    hashEntries.Add(new HashEntry(key, GetFlag));
                }

                var batch = redisDatabase.CreateBatch();
                batch.HashSetAsync(hashKeyForColor, hashEntries.ToArray());
                batch.Execute();

                Dictionary<int, int> DicGetAllCountsDuplicate = new Dictionary<int, int>(DicGetAllCounts);
                var hashEntriesCountList = new List<HashEntry>();
                foreach (var kvp in DicGetAllCountsDuplicate)
                {
                    var hashEntry = new HashEntry(kvp.Key.ToString(), kvp.Value);
                    hashEntriesCountList.Add(hashEntry);
                }

                var hashEntriesCount = hashEntriesCountList.ToArray();
                batch.HashSetAsync(hashKeyForCount, hashEntriesCount); 
                batch.Execute();
            }
            catch (Exception ex)
            {
                Log.Error("Error on Sending data to Redis=" + ex.Message + "--StackTrace--" + ex.StackTrace);
            }
        }        
        private string GetFormattedValue(int value)
        {
            try
            {
                if (value == 0)
                {
                    return "R";
                }
                else if (value >= 1 && value <= 10)
                {
                    return $"G";
                }
                else if (value >= 11 && value <= 20)
                {
                    return $"B";
                }
                else if (value >= 21 && value <= 30)
                {
                    return $"Y";
                }
                else if (value >= 31)
                {
                    return $"P";
                }
                else
                {
                    return $"_Sender_Unknown";
                }
            }
            catch (Exception ex)
            {
                EventLog.WriteEntry($"Error formatting value: {ex.Message}", EventLogEntryType.Error);
                return "Unknown";
            }
        }
        private void CleanUp()
        {
            try
            {
                rabbitChannel?.Close();
                rabbitConnection?.Close();
                redisConnection?.Close();
            }
            catch (Exception ex)
            {
                Log.Error("Error on CleanUp=" + ex.Message + "--StackTrace--" + ex.StackTrace);
            }
        }
        public void OnDebug()
        {
            OnStart(null);
        }

    }
}
