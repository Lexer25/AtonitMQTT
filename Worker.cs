using Eventor;
using FirebirdSql.Data.FirebirdClient;
using Microsoft.Extensions.Logging;
using MQTTnet;
using MQTTnet.Protocol;
using System.Data;

namespace WorkerService2
{
    public class Worker : BackgroundService
    {
        private readonly ILogger<Worker> _logger;
        private Eventor_Settings _options;

        public Worker(ILogger<Worker> logger,Eventor_Settings options)
        {
            _logger = logger;
            _options = options;
        }


        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogTrace("Старт программы");
            string clientId = Guid.NewGuid().ToString();
            var factory = new MqttClientFactory();
            var mqttClient = factory.CreateMqttClient();

            // Создаю клиента MQTT
            var options = new MqttClientOptionsBuilder()
                .WithTcpServer(_options.broker, _options.port) // MQTT broker address and port
                .WithCredentials(_options.username, _options.password) // Set username and password
                .WithClientId(clientId)
                .WithCleanSession()
                .Build();
            var connectResult = await mqttClient.ConnectAsync(options);
            if (mqttClient.IsConnected)
            {
                _logger.LogDebug($"40 MQTT успешно подключен brocker: {_options.broker}  port: {_options.port} username: {_options.username} password: {_options.password}");
               
            }  else
            {
                _logger.LogError($"44 MQTT ошибка при подключении brocker brocker: {_options.broker}  port: {_options.port} username: {_options.username} password: {_options.password}");
                 return;
            }



            FbConnection con = new FbConnection(_options.dbconfig);
            FbConnection connectionForEvent = new FbConnection(_options.dbconfig);//второе подключение к БД, для проверки свойств события.
            long id=0;
            try
            {
                con.Open();
                _logger.LogError($"Подключение к базе данных {con.ConnectionString} выполнено успешно.");

            }
            catch (Exception ex)
            {
                _logger.LogError($"Не могу подключиться к базе данных {con.ConnectionString}. Завершаю работу.");
                return;
            }
            FbCommand comd = new FbCommand("SELECT GEN_ID( gen_event_id, 0 ) FROM RDB$DATABASE", con);
            FbDataReader reader;
            try
            {
                reader = comd.ExecuteReader();
            }
            catch (Exception ex)
            {
                _logger.LogError($"Не выполнилась команда: {comd.CommandText}");
                return;
            }
            DataTable table = new DataTable();
                table.Load(reader);
                id = (long)(table.Rows[0]["gen_id"]);// получил id последного события из базы данных
                _logger.LogTrace("77 id_event при старте программы " + id.ToString());
               // con.Close();
      



            using (var events = new FbRemoteEvent(_options.dbconfig))
            {
                events.Open();
                events.RemoteEventCounts += (sender, e) => {
                    _logger.LogDebug($"74 Сработал ивент {e.Name}");

                    //connectionForEvent.Open();
                    

                    DataTable table = new DataTable();

                    string sql=$"select * from events e where e.id_event > {id} and e.id_eventtype in ({String.Join(", ", _options.id_eventtype.ToArray())})";
                    _logger.LogDebug($"95 Формирую запрос  {sql}");
                    FbCommand comd = new FbCommand(sql, con);
                    try
                    {
                        var reader = comd.ExecuteReader();
                        table.Load(reader);
                        Console.WriteLine("83 table has rows " + table.Rows.Count);
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine("92 сообщение об ошибке СУБД: " + ex.Message);    
                        _logger.LogError($"Не выполнилась команда: {comd.CommandText}");
                        return;
                    }

                    //connectionForEvent.Close( );   


                   
                   
                    //id = (int)table.Rows[0]["id_event"];
                    //_logger.LogDebug("89 " + id.ToString());

                    Console.WriteLine("94 " + table.Rows.Count);

                    foreach (DataRow row in table.Rows)
                    {
                        Console.WriteLine("---------------------------");
                        int id_dev = (int) row["id_dev"];//извлекаю id_dev устройства (точки прохода)
                        //id_dev = 14;
                        id= (int)row["id_event"];//запоминаю id_event последнего события
                        if (!_options.idmap.ContainsKey(id_dev))
                        {
                            _logger.LogDebug("128 ID_DEV= " + id_dev.ToString() + " нет в настройках.");
                            return;
                        }
                        //    ContainsKey(id_dev))) return;
                        _logger.LogDebug($"103 Обрабатываю событие от ID_DEV= {id_dev.ToString()} id_event= {row["id_event"].ToString()} Записываю в топик  {_options.topic}{_options.idmap[id_dev].ToString()} = {row["id_eventtype"].ToString()}");

                        //_logger.LogDebug($"109 Записываю в топик  {_options.topic}{_options.idmap[id_dev].ToString()} = {row["id_eventtype"].ToString()}");
                        var message = new MqttApplicationMessageBuilder()
                        .WithTopic($"{_options.topic}{_options.idmap[id_dev].ToString()}")

                        .WithPayload(row["id_eventtype"].ToString())
                        .WithQualityOfServiceLevel(MqttQualityOfServiceLevel.AtLeastOnce)
                        .WithRetainFlag()
                        .Build();
                        mqttClient.PublishAsync(message);
                    }
                    //con.Close();

                };
                events.RemoteEventError += (sender, e) => _logger.LogError($"Ошибка обработки ивента: {e.Error}");
                
                events.QueueEvents(_options.event_reaction);
                while (!stoppingToken.IsCancellationRequested)
                {
                    _logger.LogTrace("Ивент жив");
                    var message = new MqttApplicationMessageBuilder()
                    .WithTopic(_options.liveTopic)
                    .WithPayload((DateTime.Now.Ticks%1000).ToString())
                    .WithQualityOfServiceLevel(MqttQualityOfServiceLevel.AtLeastOnce)
                    .WithRetainFlag()
                    .Build();
                    mqttClient.PublishAsync(message);
                    await Task.Delay(_options.breaktime, stoppingToken);
                }
            }
        }
    }
}
