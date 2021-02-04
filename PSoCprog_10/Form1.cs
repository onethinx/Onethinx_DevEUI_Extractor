using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.IO;
using System.Windows.Forms;
using System.Diagnostics;
using PP_ComLib_Wrapper;
using System.Threading;

namespace PSoCprog_10
{
    public partial class Form1 : Form
    {
        PP_ComLib_WrapperClass pp;

        bool autoRun = false;
        public Form1()
        {
            InitializeComponent();
        }

        protected virtual void ThreadSafe(MethodInvoker method)
        {
            if (InvokeRequired)
                Invoke(method);
            else
                method();
        }

        string lastError = "";
        private int hrval;
        public bool ErrorState = false;

        private string SucceedStr(long hr, string strMSG)
        {
            if (hr < 0) lastError = strMSG;
            return ((hr >= 0) ? "OK" : strMSG);
        }

        private bool SUCCEEDED(long hr)
        {
            return (hr >= 0);
        }


        public int hr
        {
            get
            {
                return hrval;
            }
            set
            {
                hrval = value;
                if (!SUCCEEDED(hr))
                {
                    ErrorState = true;
                    panel1.BackColor = Color.Red;
                }
            }
        }

        private void Form1_Load(object sender, EventArgs e)
        {
            string strMsg;
            hr = Init(out strMsg);
            AppendText("\r\nInit: " + SucceedStr(hr, strMsg));
        }


        public void AppendText(string txt)
        {
            ThreadSafe(delegate
            {
                textBox1.AppendText(txt);
            });
        }

        public int Init(out string strMSG)
        {
            strMSG = "";
            try
            {
                pp = new PP_ComLib_WrapperClass();
            }
            catch
            { }
            try
            {
                pp.w_ConnectToLatest();
                pp._StartSelfTerminator(Process.GetCurrentProcess().Id);
                pp.OnConnect += Pp_OnConnect;
                pp.OnDisconnect += Pp_OnDisconnect;
                //pp.OnDisconnect += new PP_ComLib_WrapperClass.DelegateOneStringParam(pp_OnDisconnect);
            }
            catch
            {
                strMSG += " Unable To Execute MP3 service";
                return -0x2002;
            }
            strMSG = "\r\nFound PP_ComLib: " + pp.Version();
            return 0;
        }

        private void Pp_OnDisconnect(string strMsg)
        {
            AppendText("\r\nDisconnected: " + strMsg);
        }

        private void Pp_OnConnect(string strMsg)
        {
            AppendText("\r\nConnected: " + strMsg);
        }

        private void btRead_Click(object sender, EventArgs e)
        {
            //btLoadHEX.Enabled = false;
            btRead.Enabled = false;
            panel1.BackColor = this.BackColor;
            ErrorState = false;
            doRead();
            if (!ErrorState)
            {
                panel1.BackColor = Color.Green;
                Console.WriteLine("Read OK");
                if (autoRun)
                {
                    Thread.Sleep(1000);
                    this.Close();
                }
            }
            else
            {
                Console.WriteLine(lastError);
            }
           // btLoadHEX.Enabled = true;
            btRead.Enabled = true;
        }
        public int CM4Attach(out string strMSG)
        {
            string[] devices; strMSG = "";
            int hr = 0;

            pp.ToggleReset(0, 100, out strMSG);

            for (int cnt = 0; cnt < 10; cnt++)
            {
                hr = pp.JTAG_EnumerateDevices(out devices, out strMSG);
                if (SUCCEEDED(hr)) break;
            }
            if (!SUCCEEDED(hr)) return hr;

            byte[] dataIN;
            byte[] dataOUT;

            //swdior_raw 0x00 #Read DPIDR)
            dataIN = new byte[] { 0x00 };
            hr = pp.swdior_raw(dataIN, out dataOUT);

            //swdiow_raw 0x01 0x00 0x00 0x00 0x50 #Init DAP (CSYSPWRUPREQ=1, CDBGPWRUPREQ=1)
            dataIN = new byte[] { 0x01, 0x00, 0x00, 0x00, 0x50 };
            hr = pp.swdiow_raw(dataIN, out dataOUT);

            //# *********************** CM4_AP ***********************
            //# Init CM4_AP access
            //swdiow_raw 0x00 0x1E 0x00 0x00 0x00 # Clear sticky errors (DP.ABORT)
            //swdiow_raw 0x02 0x00 0x00 0x00 0x02 #Select AP (0 - SYS_AP, 1 - CM0_AP, 2 - CM4 AP)
            //swdiow_raw 0x04 0x02 0x00 0x00 0x23 #Set 32-bit mode and prot bits

            dataIN = new byte[] { 0x00, 0x1E, 0x00, 0x00, 0x00 };
            hr = pp.swdiow_raw(dataIN, out dataOUT);

            dataIN = new byte[] { 0x02, 0x00, 0x00, 0x00, 0x02 };
            hr = pp.swdiow_raw(dataIN, out dataOUT);

            dataIN = new byte[] { 0x04, 0x02, 0x00, 0x00, 0x23 };
            hr = pp.swdiow_raw(dataIN, out dataOUT);

            // Enable debug, and halt the CPU using the DHCSR register
            unchecked
            {
             //   pp.DAP_WriteIO((int)0xE000EDF0, (int)0xA05F0003, out strMSG);
                // Verify the debug enable and CPU halt bits are set. Result must == 0x*******3
            //    pp.DAP_ReadIO((int)0xE000EDF0, out int data, out strMSG);

            }
            return hr;
        }

        bool ReadIO(uint addr32, out byte byteOUT)
        {
            byte[] dataIN = new byte[] { 0x05, (byte)addr32, (byte)(addr32 >> 8), (byte)(addr32 >> 16), (byte)(addr32 >> 24) }; //0x20, 0x47, 0x00, 0x00};
            byte[] dataOUT;
            pp.swdiow_raw(dataIN, out dataOUT);
            dataIN = new byte[] { 0x07 };
            pp.swdior_raw(dataIN, out dataOUT);// bool ack2 = Read_DAP (DRW, OUT data32);
            byteOUT = (dataOUT as byte[])[0];
            return true;
        }

        public int readDevEUI( out string dataString, out string strMSG)
        {
            int hr = 0; int Val = 0; dataString = ""; strMSG = "";
            for (int cnt = 0; cnt < 10; cnt++)
            {
                if ((cnt & 3) == 0)
                {
                    hr = pp.DAP_ReadIO(0x14007E00 + cnt, out Val, out strMSG);
                    if (!SUCCEEDED(hr)) return hr;
                }
                if (cnt > 1)
                    dataString += ((Val >> ((cnt & 3) * 8)) & 255).ToString("X2");
            }
            return hr;
        }

                
        private int doRead()
        {
            string strMsg;

            string[] ports;
            hr = pp.GetPorts(out ports, out strMsg);
            AppendText("\r\nGetPorts: " + SucceedStr(hr, strMsg));
            if (ports == null || ports.Length < 1)
            {
                AppendText("\r\nNo programmers found");
                return -1;
            } else if (ports.Length > 1)
            {
                //todo: select programmer
            }
            hr = pp.OpenPort(ports[0], out strMsg);         // Select first programmer available

            AppendText("\r\nOpenPort: " + SucceedStr(hr, strMsg));

            pp.SetPowerVoltage("2.5", out strMsg);
            pp.PowerOn(out strMsg);
            pp.SetAcquireMode("Reset", out strMsg);
            pp.SetProtocol(enumInterfaces.SWD, out strMsg);
            pp.SetProtocolConnector(0, out strMsg); // 5-pin
            pp.SetProtocolClock(enumFrequencies.FREQ_01_6, out strMsg);

            hr = CM4Attach(out strMsg);
            AppendText("\r\nAcquire: " + SucceedStr(hr, strMsg));


            // Get DevEUI
            hr = readDevEUI(out string DevEUI, out strMsg);
            if (SUCCEEDED(hr)) AppendText("\r\nRead DevEUI: " + DevEUI);
            AppendText("\r\nRead DevEUI: " + SucceedStr(hr, strMsg));
            hr = pp.DAP_ReleaseChip(out strMsg);
            AppendText("\r\nRelease: " + SucceedStr(hr, strMsg));
            hr = pp.ClosePort(out strMsg);
            AppendText("\r\nClosePort: " + SucceedStr(hr, strMsg));
            return 0;
        }



        private void GetSiliconID()
        {
            string strMsg;
            object siliconID;
            int familyIdHi, familyIdLo, revisionIdMaj, revisionIdMin, siliconIdHi, siliconIdLo, sromFmVersionMaj, sromFmVersionMin, protectState, lifeCycleStage;
            hr = pp.PSoC6_GetSiliconID(out siliconID, out familyIdHi, out familyIdLo, out revisionIdMaj, out revisionIdMin, out siliconIdHi, out siliconIdLo, out sromFmVersionMaj, out sromFmVersionMin, out protectState, out lifeCycleStage, out strMsg);
            if (SUCCEEDED(hr))
            {
                string msg = "Silicon ID: ";
                byte[] id = siliconID as byte[];
                for (int i = 0; i < id.Length; i++) msg += id[i].ToString("X2") + " ";
                msg = string.Format(@"{0}{1}familyId: {2}.{3}{1}revisionId: {4}.{5}{1}siliconId:{6:X2}.{7:X2}{1}sromFmVersion: {8}.{9}{1}protectState :{10}{1}lifeCycleStage :{11}", msg, "\r\n", familyIdHi, familyIdLo, revisionIdMaj, revisionIdMin, siliconIdHi, siliconIdLo, sromFmVersionMaj, sromFmVersionMin, protectState, lifeCycleStage);
                AppendText("\r\nOK: " + msg);
            }
            else
                AppendText("\r\nError: " + strMsg);
        }


        private void textBox1_Enter(object sender, EventArgs e)
        {
            textBox1.Enabled = false;
            textBox1.Enabled = true;
        }

        private void Form1_FormClosing(object sender, FormClosingEventArgs e)
        {

            if (ErrorState)
            {
                StringWriter sw = new StringWriter();
                sw.WriteLine(lastError);
                Console.SetError(sw);
            }
        }
    }
}
