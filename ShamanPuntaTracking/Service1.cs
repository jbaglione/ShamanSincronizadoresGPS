using System;
using System.ServiceProcess;
using System.Text;
using System.Timers;
using System.IO;
using System.Net;
using ShamanExpressDLL;
using System.Net.Http;
using System.Json;
using System.Configuration;

namespace ShamanPuntaTracking
{
    public partial class Service1 : ServiceBase
    {
        private string URL = ConfigurationManager.AppSettings["URL"];
        private string urlParameters = ConfigurationManager.AppSettings["urlParameters"];

        private string URL2 = ConfigurationManager.AppSettings["URL2"];
        private string urlParameters2 = ConfigurationManager.AppSettings["urlParameters2"];//"?username=webservice&password=webcardio&orgname=0512";


        Timer t = new Timer();
        bool flgDBConnect = false;
        public Service1()
        {
            InitializeComponent();
        }

        protected override void OnStart(string[] args)
        {
            t.Elapsed += delegate { ElapsedHandler(); };
            t.Interval = 60000;
            t.Start();
        }

        protected override void OnPause()
        {
            t.Stop();
        }

        protected override void OnContinue()
        {
            t.Start();
        }

        protected override void OnStop()
        {
            t.Stop();
        }

        public void ElapsedHandler()
        {
            /*------> Conecto a DB <---------*/
            if (this.setConexionDB())
            {
                /*------> Proceso <--------*/
                this.ReadGPSDevices();
            }
        }

        private void addLog(bool rdo, string logProcedure, string logDescription)
        {

            string path;

            path = Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location);
            path = path + "\\" + modFechas.DateToSql(DateTime.Now).Replace("-", "_") + ".log";

            if (!File.Exists(path))
            {
                // Create a file to write to.
                using (StreamWriter sw = File.CreateText(path))
                {
                    sw.WriteLine("Log " + DateTime.Now.Date);
                }
            }

            using (StreamWriter sw = File.AppendText(path))
            {
                string rdoStr = "Ok";
                if (rdo == false)
                {
                    rdoStr = "Error";
                }
                sw.WriteLine(DateTime.Now.Hour.ToString("00") + ":" + DateTime.Now.Minute.ToString("00") + "\t" + rdoStr + "\t" + logProcedure + "\t" + logDescription);
            }
        }

        #region proceso

        private bool setConexionDB()
        {

            bool devCnn = flgDBConnect;

            try
            {
                if (devCnn == false)
                {
                    StartUp init = new StartUp();
                    if (init.GetValoresHardkey(false))
                    {
                        if (init.GetVariablesConexion())
                        {
                            /*
                            modDatabase.cnnDataSource = "mcocompany.ddns.net";
                            modDatabase.cnnUser = "sa";
                            modDatabase.cnnPassword = "#shaman2004";
                            */

                            if (init.AbrirConexion(modDeclares.cnnDefault) == true)
                            {
                                devCnn = true;
                                flgDBConnect = true;
                                modFechas.InitDateVars();
                                modNumeros.InitSepDecimal();

                                addLog(true, "setConexionDB", "Conectado a Database Shaman");
                            }
                            else
                                addLog(false, "setConexionDB", "No se pudo conectar a base de datos Shaman - " + init.MyLastExec.ErrorDescription);
                        }
                        else
                            addLog(false, "setConexionDB", "No se pudieron recuperar las variables de conexión - " + init.MyLastExec.ErrorDescription);
                    }
                    else
                        addLog(false, "setConexionDB", "No se encuentran los valores HKey - " + init.MyLastExec.ErrorDescription);
                }
            }

            catch (Exception ex)
            {
                addLog(false, "setConexionDB", ex.Message.ToString());
                devCnn = false;
            }
            return devCnn;
        }

        private void ReadGPSDevices()
        {
            try
            {
                // Obtengo Dirección IP
                string myIp = this.getMyIp();
                string myKey = "";

                switch (modDeclares.sysHardKey)
                {
                    case "21532 08645":
                        myKey = "m355u5upr4nu";
                        //empresa = "mcocompanyshaman";
                        break;
                    case "14659 30427":
                        myKey = "s4ndm4nsauc3";
                        //empresa = "emergershaman";
                        myIp = "palabra3";
                        break;
                    case "10147 41472":
                        //empresa = "medicalexpress";
                        myKey = "r4str34nd0ok";
                        myIp = "palabra3";
                        break;
                    default:
                        //empresa = "medicalexpress";
                        myKey = "r4str34nd0ok";
                        myIp = "palabra3";
                        break;
                }

                /* Armo Firma */
                string myFechaHora = DateTime.Now.ToString("yyyyMMdd HH:mm:ss:fff");
                string myFirma = Firmar(myIp, myKey, myFechaHora);

                /* GPS Activos */
                addLog(true, "ReadGPSDevices", "Obtengo móviles activos");

                //Read RestService
                using (var httpClient = new HttpClient())
                {
                    httpClient.DefaultRequestHeaders.Add("User-Agent", "Mozilla/5.0 (Windows NT 6.1; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/58.0.3029.110 Safari/537.36");

                    var response = httpClient.GetStringAsync(new Uri(URL + urlParameters)).Result;
                    //{"errno":0, "result":[    { "vid":6454,"utc":"2018-12-03 15:28:03","lat":-34.918129,"lon":-54.961102,"speed":25,"degrees":196.0,"odometer":126392071,"localtime":"2018-12-03 12:28:03"}
                    //                          { "vid":6454,"utc":".....}
                    //                          { ...}]
                    //}

                    var responseAux = httpClient.GetStringAsync(new Uri(URL2 + urlParameters2)).Result;
                    //2
                    //{"errno":0, "result":[ 
                    //                       { "id":6454,"name":"Mov. 18","info":"ALFA"},
                    //                       { "id":6454,"name":"Mov. 18","info":"ALFA"}
                    //                       { ...}]
                    //}

                    var jsonObject = JsonValue.Parse(response);
                    conMoviles objMovilesMaster = new conMoviles();

                    if (jsonObject.ContainsKey("result"))
                    {

                        JsonArray jsonArrayResult = (JsonArray)JsonValue.Parse(jsonObject["result"].ToString());

                        var jsonObjectAux = JsonValue.Parse(responseAux);
                        if (jsonObjectAux.ContainsKey("result"))
                        {
                            JsonArray jsonArrayResultAux = (JsonArray)JsonValue.Parse(jsonObjectAux["result"].ToString());

                            foreach (var item in jsonArrayResult)
                                SetGpsDataIntoDataBase(objMovilesMaster, item, GetMovilId(item["vid"].ToString(), jsonArrayResultAux));
                        }

                    }
                }
            }

            catch (Exception ex)
            {
                addLog(false, "ReadGPSDevices", ex.Message);
            }
        }

        private string GetMovilId(string vid, JsonArray jsonArrayResultAux)
        {
            try
            {
                foreach (var item in jsonArrayResultAux)
                    if (item["id"].ToString() == vid)
                        return item["info"].ToString().Remove(0, 1).Remove(item["info"].ToString().Length - 2, 1);

                return null;
            }
            catch (Exception)
            {
                return null;
            }
        }


        private void SetGpsDataIntoDataBase(conMoviles objMovilesMaster, JsonValue item, string movilId)
        {
            DateTime gpsFhr = DateTime.Parse(item["utc"]);

            conMovilesActuales objMoviles = new conMovilesActuales();

            if (objMoviles.Abrir(objMoviles.GetIDByIndex(objMovilesMaster.GetIDByMovil(movilId)).ToString()))
            {

                if (objMoviles.gpsFecHorTransmision < gpsFhr)
                {
                    objMoviles.gpsFecHorTransmision = gpsFhr;
                    objMoviles.gpsLatitud = Convert.ToDecimal(item["lat"].ToString().Replace(".", modNumeros.wSepDecimal));
                    objMoviles.gpsLongitud = Convert.ToDecimal(item["lon"].ToString().Replace(".", modNumeros.wSepDecimal));

                    if (objMoviles.Salvar(objMoviles))
                    {

                        typGpsHistorico objGPSHist = new typGpsHistorico();

                        objGPSHist.CleanProperties(objGPSHist);
                        objGPSHist.MovilId.SetObjectId(objMoviles.MovilId.ID.ToString());
                        objGPSHist.VehiculoId.SetObjectId(objMoviles.VehiculoId.ID.ToString());
                        objGPSHist.FecHorTransmision = gpsFhr;
                        objGPSHist.Latitud = objMoviles.gpsLatitud;
                        objGPSHist.Longitud = objMoviles.gpsLongitud;

                        if (objGPSHist.Salvar(objGPSHist))
                            addLog(true, "ReadGPSDevices", "Actualizado GPS OK de Móvil " + objMoviles.MovilId.Movil.ToString());
                        else
                            addLog(true, "ReadGPSDevices", "Actualizado estado actual sin histórico de Móvil " + objMoviles.MovilId.Movil.ToString());
                    }
                    else
                        addLog(false, "ReadGPSDevices", "Error al actualizar Móvil " + objMoviles.MovilId.Movil.ToString());
                }
                else
                    addLog(false, "ReadGPSDevices", "La fecha/hora del móvil " + objMoviles.MovilId.Movil.ToString() + " es superior a la del GPS");
            }

            else
                addLog(false, "ReadGPSDevices", "El móvil " + movilId + " no está operativo");
        }


        private string getMyIp()
        {
            string result = "";

            using (var client = new WebClient())
            {
                result = client.DownloadString("http://www.lineasodc.com.ar/whatismyip.aspx");
            }
            char delimiter = '\n';
            string[] vals = result.Split(delimiter);
            int i = 0;
            string myIP = "";

            while ((i < vals.Length) & (myIP == ""))
            {
                delimiter = '=';
                string[] reg = vals[i].ToString().Split(delimiter);
                if (reg[0] == "REMOTE_ADDR")
                {
                    myIP = reg[1];
                }
                i++;
            }

            return myIP;
        }

        /// <summary> Genera una firma para usar en los metodos web. </summary>
        /// <param name="formattedTime">En C# es la hora actual en este formato: DateTime.Now.ToString("yyyyMMdd HH:mm:ss:fff")</param>
        /// <param name="ipPublica">Ip publica WAN (no de la red local interna). Se puede consultar accediendo a http://www.lineasodc.com.ar/whatismyip.aspx usando el valor que muestre REMOTE_ADDR.</param>
        /// <param name="privateKey">Clave que les fue asignada por Ojos del Cielo.</param>
        /// <returns></returns> 
        protected static string Firmar(string ipPublica, string privateKey, string fechor)
        {
            var textToSign = ipPublica + "," + fechor;
            var firma = Sign(textToSign, privateKey);
            return firma;
        }

        /// <summary> Genera una firma en base a un texto y una clave. </summary>
        /// <param name="text">Texto</param>
        /// <param name="keyString">Clave</param>
        /// <returns></returns>
        public static string Sign(string text, string keyString)
        {

            var encoding = new System.Text.ASCIIEncoding();

            // converting key to bytes will throw an exception, need to replace '-' and '_' characters first.
            string usablePrivateKey = keyString.Replace("-", "+").Replace("_", "/");
            byte[] privateKeyBytes = Convert.FromBase64String(usablePrivateKey);
            var textBytes = ASCIIEncoding.ASCII.GetBytes(text);

            // compute the hash
            var algorithm = new
            System.Security.Cryptography.HMACSHA1(privateKeyBytes);
            byte[] hash = algorithm.ComputeHash(textBytes);
            // convert the bytes to string and make url-safe by replacing '+' and '/' characters

            string signature = Convert.ToBase64String(hash).Replace("+", "-").Replace("/", "_");
            return signature;
        }

        #endregion
    }
}
