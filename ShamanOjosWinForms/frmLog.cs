using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Diagnostics;
using System.Timers;
using System.IO;
using System.Net;
using ShamanExpressDLL;
using System.Xml.Linq;

namespace ShamanOjosWinForms
{
    public partial class frmLog : Form
    {

        bool flgDBConnect = false;

        public frmLog()
        {
            InitializeComponent();
            this.tmrRefresh.Enabled = true;
            this.tmrRefresh_Tick(null, null);
        }

        private void addLog(bool rdo, string logProcedure, string logDescription, bool clear = false)
        {

            string path;

            path = System.IO.Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location);
            path = path + "\\" + modFechas.DateToSql(DateTime.Now).Replace("-", "_") + ".log";

            if (!File.Exists(path))
            {
                // Create a file to write to.
                using (StreamWriter sw = File.CreateText(path))
                {
                    sw.WriteLine("Log " + DateTime.Now.Date);
                    this.txtLog.Text = "Log " + DateTime.Now.Date + Environment.NewLine;
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
                if (clear) { this.txtLog.Text = ""; }
                this.txtLog.Text = this.txtLog.Text + DateTime.Now.Hour.ToString("00") + ":" + DateTime.Now.Minute.ToString("00") + "\t" + rdoStr + "\t" + logProcedure + "\t" + logDescription + Environment.NewLine;

            }

        }

        private void tmrRefresh_Tick(object sender, System.EventArgs e)
        {

            this.tmrRefresh.Enabled = false;

            /*------> Conecto a DB <---------*/
            if (this.setConexionDB())
            {
                /*------> Proceso <--------*/
                this.ReadGPSDevices();
            }

            this.tmrRefresh.Enabled = true;

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
                string empresa = "";

                switch (modDeclares.sysHardKey)
                {
                    case "21532 08645":
                        myKey = "m355u5upr4nu";
                        empresa = "mcocompanyshaman";
                        break;
                    case "14659 30427":
                        myKey = "s4ndm4nsauc3";
                        empresa = "emergershaman";
                        myIp = "palabra3";
                        break;
                    case "10147 41472":
                        empresa = "medicalexpress";
                        myKey = "r4str34nd0ok";
                        myIp = "palabra3";
                        break;
                    case "23859 42806":
                        empresa = "doctored";
                        myKey = "c3res4stre2t";
                        myIp = "palabra3";
                        break;
                    default:
                        empresa = "";
                        myKey = "";
                        myIp = "";
                        break;
                //}

                /* Armo Firma */
                string myFechaHora = DateTime.Now.ToString("yyyyMMdd HH:mm:ss:fff");

                string myFirma = Firmar(myIp, myKey, myFechaHora);

                /* GPS Activos */
                addLog(true, "ReadGPSDevices", "Obtengo móviles activos");

                WSOjos.OdcMedicoWebServiceSoapClient gpsWS = new WSOjos.OdcMedicoWebServiceSoapClient();

                XElement movPos = gpsWS.GetPosicionMoviles(empresa, myFirma, myFechaHora);

                string plain_xml = movPos.ToString();
                var reader = new StringReader(plain_xml);
                var dataSet = new DataSet();
                dataSet.ReadXml(reader);
                var dtGps = dataSet.Tables[0];

                conMoviles objMovilesMaster = new conMoviles();

                /* Recorro */
                for (int i = 0; i < dtGps.Rows.Count; i++)
                {

                    bool chgEst = false;

                    string[] fechahora = dtGps.Rows[i]["fechaYhora"].ToString().Split(' ');

                    DateTime gpsFhr = Convert.ToDateTime(modFechas.AnsiToDate(Convert.ToInt64(fechahora[0])).ToShortDateString() + ' ' + fechahora[1]);

                    conMovilesActuales objMoviles = new conMovilesActuales();

                    if (objMoviles.Abrir(objMoviles.GetIDByIndex(objMovilesMaster.GetIDByMovil(dtGps.Rows[i]["movil"].ToString())).ToString()))
                    {

                        if (objMoviles.gpsFecHorTransmision < gpsFhr)
                        {
                            objMoviles.gpsFecHorTransmision = gpsFhr;
                            objMoviles.gpsLatitud = Convert.ToDecimal(dtGps.Rows[i]["lat"].ToString().Replace(".", modNumeros.wSepDecimal));
                            objMoviles.gpsLongitud = Convert.ToDecimal(dtGps.Rows[i]["lon"].ToString().Replace(".", modNumeros.wSepDecimal));

                            if (objMoviles.FechaHoraMovimiento > gpsFhr) { chgEst = true; }

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

                        if (chgEst)
                        {
                            /// Estado del móvil
                            string vEstId;
                            string vLey = "Situación: " + objMoviles.SucesoIncidenteId.Descripcion;
                            int vClrId = 0;

                            switch (objMoviles.SucesoIncidenteId.AbreviaturaId)
                            {
                                case "L":
                                    vEstId = "L";
                                    break;
                                case "R":
                                    vEstId = "L";
                                    break;
                                default:
                                    vEstId = "A";
                                    break;
                            }


                            if (vEstId == "A" && objMoviles.IncidenteViajeId.IncidenteDomicilioId.ID > 0 && objMoviles.IncidenteViajeId.IncidenteDomicilioId.IncidenteId.ID > 0)
                            {
                                vLey = vLey + ";Grado: " + objMoviles.IncidenteViajeId.IncidenteDomicilioId.IncidenteId.GradoOperativoId.AbreviaturaId;
                                vLey = vLey + ";Cliente: " + objMoviles.IncidenteViajeId.IncidenteDomicilioId.IncidenteId.ClienteId.AbreviaturaId;

                                switch (objMoviles.IncidenteViajeId.IncidenteDomicilioId.IncidenteId.GradoOperativoId.AbreviaturaId)
                                {
                                    case "R":
                                        vClrId = 3;
                                        break;
                                    case "A":
                                        vClrId = 4;
                                        break;
                                    case "VP":
                                        vClrId = 6;
                                        break;
                                    case "V":
                                        vClrId = 5;
                                        break;
                                    default:
                                        vClrId = 7;
                                        break;
                                }
                            }

                            string format = "<Root><Child>{0}</Child></Root>";

                            conGrillaOperativa objGrilla = new conGrillaOperativa();
                            DataTable dtDot = objGrilla.GetCurrentDotacion(objMoviles.MovilId.ID);
                            string listDot = "";

                            for (int trp = 0; trp < dtDot.Rows.Count; trp++)
                            {
                                if (trp == 0)
                                    listDot = trp.ToString() + "-" + dtDot.Rows[trp]["Personal"];
                                else
                                    listDot = listDot + ";" + trp.ToString() + "-" + dtDot.Rows[trp]["Personal"];
                            }

                            var empleados = XElement.Parse(string.Format(format, listDot));
                            var descripciones = XElement.Parse(string.Format(format, vLey));

                            XElement rdoEst = gpsWS.SetCambioEstado(empresa, myFirma, myFechaHora, objMoviles.MovilId.Movil, vEstId, vClrId, empleados, descripciones);

                            string est_xml = rdoEst.ToString();
                            var essreader = new StringReader(est_xml);
                            var dataSetEst = new DataSet();
                            dataSetEst.ReadXml(essreader);
                            var dtRdo = dataSetEst.Tables[0];
                        }
                    }

                    else
                        addLog(false, "ReadGPSDevices", "El móvil " + dtGps.Rows[i]["movil"].ToString() + " no está operativo");
                }
            }

            catch (Exception ex)
            {
                addLog(false, "ReadGPSDevices", ex.Message);
            }
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
