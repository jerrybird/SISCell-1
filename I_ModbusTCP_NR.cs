using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using ModbusLibrary_NR;
using CSOPCServerLib;

namespace SISCell
{
    public class I_ModbusTCP_NR : iProtocol
    {
        private System.ComponentModel.BackgroundWorker backgroundWorker1;
        //TCP连接
        Socket socket;
        //发送队列
        Queue<TCP> SendList = new Queue<TCP>();
        //等待语句柄。挂起后台线程时阻塞使用
        EventWaitHandle waitHandel = new EventWaitHandle(false, EventResetMode.AutoReset);
        //检查周期
        DateTime check = new DateTime();
        //定时器发送报文周期
        int sendRate;
        //定时器总招周期
        int callAllRate;

        //定时器_发送报文
        System.Timers.Timer tmSend;
        //定时器_总招数据
        System.Timers.Timer tmCallAll;

        int m_startAddr = 0;
        int m_len = 0;
        int m_type = 0;
        //允许读标志。关闭后台线程时同步信号使用
        bool ReadEnable = false;
        bool sendFlag;

        //建立数据索引，查找已初始化的数据更新
        Dictionary<int, numInf> find = new Dictionary<int, numInf>();
        //配置信息
        CONFG_Modbus_NR m_config_Modbus;
        OPCServerFUN fun = null;

        //输出文本路径
        StreamWriter sw;
        string logfile;
        /// <summary>
        /// 构造函数
        /// </summary>
        public I_ModbusTCP_NR()
        {
            this.backgroundWorker1 = new System.ComponentModel.BackgroundWorker();
            this.backgroundWorker1.WorkerReportsProgress = true;
            this.backgroundWorker1.WorkerSupportsCancellation = true;
            this.backgroundWorker1.DoWork += new System.ComponentModel.DoWorkEventHandler(this.backgroundWorker1_DoWork);
            this.backgroundWorker1.RunWorkerCompleted += new System.ComponentModel.RunWorkerCompletedEventHandler(this.backgroundWorker1_RunWorkerCompleted);
            this.backgroundWorker1.ProgressChanged += new System.ComponentModel.ProgressChangedEventHandler(this.backgroundWorker1_ProgressChanged);

            m_config_Modbus = new CONFG_Modbus_NR("config\\Modbus_NR.ini");
            sendRate = Convert.ToInt32(m_config_Modbus.m_SendRate * 1000);
            callAllRate = sendRate * 4;

            logfile = "log.txt";
            FileStream fs = new FileStream(logfile, FileMode.OpenOrCreate);
            fs.Close();
            
            sw = File.AppendText(logfile);
            sw.WriteLine("初始化成功！");
            sw.Close();
        }
        ~I_ModbusTCP_NR()
        {
            if (fun != null)
                fun.UnregisterS(m_config_Modbus.m_OPCServerName);
        }
        /// <summary>
        /// “连接”事件触发
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        public bool Connect()
        {
            if (!Mconnect()) return false;

            if (!ReadEnable)
            {
                backgroundWorker1.RunWorkerAsync();
            }
            else
            {
                waitHandel.Set();
            }
            return true;

        }
        public bool Mconnect()
        {
            //服务端IP和端口信息设定,这里的IP可以是127.0.0.1，可以是本机局域网IP，也可以是本机网络IP
            IPEndPoint ServerInfo = new IPEndPoint(IPAddress.Parse(m_config_Modbus.m_IPAddress), m_config_Modbus.m_Port);
            try
            {
                socket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
                socket.Connect(ServerInfo);
                try
                {
                    sw = File.AppendText(logfile);
                    sw.WriteLine(DateTime.Now.ToString() + " 连接服务器成功！");
                    sw.Close();
                }
                catch { }
                
            }
            catch (Exception er)
            {
                Console.Write(er.ToString());
                try
                {
                    sw = File.AppendText(logfile);
                    sw.WriteLine(DateTime.Now.ToString() + " 登录服务器失败，请确认服务器是否正常工作！");
                    sw.Close();
                }
                catch { }
                return false;
            }

            SendList.Clear();
            sendFlag = true;
            ReadEnable = false;
            return true;
        }

        public void DisConnect()
        {
            ReadEnable = false;
            tmSend.Stop();
            tmSend.Dispose();
            tmCallAll.Stop();
            tmCallAll.Dispose();
            System.Threading.Thread.Sleep(1000);
            socket.Close();
        }

        public bool Connected
        {
            get
            {
                return (socket.Connected);
            }
        }

        public void InitPt(int nNum, numInf[] nrst, int sNum, strInf[] srst)
        {

        }

        public bool GetRtValue(int nNum, numInf[] nrst, int sNum, strInf[] srst)
        {
        
            if (find.Count < 1) return false;
            int index = 0;
            numInf numtemp = new numInf();
            for (int i = 0; i < nrst.Length; i++)
            {
                try
                {   
                    string[] arry = nrst[i].srcId.Split('_');
                    index = int.Parse(arry[0] + int.Parse(arry[1]).ToString("d4"));
                    if (find.TryGetValue(index, out numtemp))
                    {
                        float rat = 1.0f, dev = 0.0f;
                        if (nrst[i].ratio.Contains("+"))
                        {
                            string[] ss = nrst[i].ratio.Split('+');
                            rat = float.Parse(ss[0]);
                            dev = float.Parse(ss[1]);
                        }
                        else if (nrst[i].ratio.Contains("-"))
                        {
                            string[] ss = nrst[i].ratio.Split('-');
                            rat = float.Parse(ss[0]);
                            dev = float.Parse(ss[1]) * (-1);
                        }
                        else
                        {
                            rat = float.Parse(nrst[i].ratio);
                            dev = 0.0f;
                        }
                        nrst[i].val = numtemp.val * rat + dev;
                        if (numtemp.dtm.Year == 1) nrst[i].dtm = DateTime.Now;
                        else nrst[i].dtm = numtemp.dtm;
                    }
                 }
                catch (Exception e)
                {
                    try
                    {
                        sw = File.AppendText(logfile);
                        sw.WriteLine(DateTime.Now.ToString() + e.ToString());
                        sw.Close();
                    }
                    catch { }
                    continue;
                }
            }
            return true;  
        }

        public void SetRtValue(int nNum, numInf[] nrst, int sNum, strInf[] srst)
        {

        }
       /* public static void UnPackString(string str)
        {

            //numInf[] numinf;
            string stemp = str.ToLower().Replace(" ", "");
            byte[] bytetemp = new byte[stemp.Length / 2];
            for (int i = 0; i < stemp.Length / 2; i++)
            {
                string ss = stemp.Substring(2 * i, 2);

                bytetemp[i] = Convert.ToByte(ss, 16);
            }
            try
            {
                APDUClass temp = new APDUClass(bytetemp);
                if (temp != null)
                {
                    //sw = File.AppendText(logfile);
                    //sw.WriteLine("RX: " + DateTime.Now.ToString() + " Type:" + temp.GetAsduType().ToString() + " Res:" + temp.Res.ToString() 
                    //                    + " SR:" + temp.GetSR().ToString() + " NR:" + temp.GetNR().ToString()
                    //                    + "\r\n" + temp.ApciToString() + temp.AsduToString() + "\r\n");
                    //sw.Close();
                    //Console.WriteLine("RX: " + DateTime.Now.ToString() + " Type:" + temp.GetAsduType().ToString() + " Res:" + temp.Res.ToString()
                    //                        + " SR:" + temp.GetSR().ToString() + " NR:" + temp.GetNR().ToString()
                    //                        + "\n" + temp.ApciToString() + temp.AsduToString() + "\r\n"); 

                    Console.WriteLine("RX: " + "Type:" + temp.GetAsduType().ToString() + " Res:" + temp.Res.ToString()
                                     + " SR:" + temp.GetSR().ToString() + " NR:" + temp.GetNR().ToString());
                    var datas = temp.GetData();
                    //numinf = new numInf[datas.Count];
                    //int i = 0;
                    foreach (var data in datas)
                    {
                        //try { sw = File.AppendText(filestr); }
                        //catch { Thread.Sleep(50); sw = File.AppendText(filestr); }
                        //sw.WriteLine("RX " + "addr:" + data.Addr.ToString() + " " +
                        //            "data:" + data.Data.ToString() + " " +
                        //            "time:" + data.Time.ToString());
                        //sw.Close();
                        if (data.Addr == 0) continue;
                        Console.WriteLine("RX: " + "addr:" + data.Addr.ToString() + " " +
                                            "data:" + data.Data.ToString() + " " +
                                            "time:" + data.Time.ToString());


                        //numInf numtemp = new numInf() ;
                        //numinf[i].sn = Convert.ToInt16( data.Addr);
                        //numinf[i].val = Convert.ToSingle( data.Data);
                        //numinf[i].dtm = Convert.ToDateTime(data.Time);
                        //i++;

                    }

                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine(ex.ToString());
            }
            //return numinf;

        }*/
        /// <summary>
        /// 后台线程启动
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void backgroundWorker1_DoWork(object sender, DoWorkEventArgs e)
        {
            ReadEnable = true;
            ReceiveOnce();
            tmSend = new System.Timers.Timer();
            tmSend.AutoReset = true;
            tmSend.Interval = sendRate;
            tmSend.Elapsed += new System.Timers.ElapsedEventHandler(tm_Elapsed);
            tmSend.Start();

            tmCallAll = new System.Timers.Timer();
            tmCallAll.AutoReset = true;
            tmCallAll.Interval = callAllRate;
            tmCallAll.Elapsed += new System.Timers.ElapsedEventHandler(callAll_Elapsed);
            tmCallAll.Start();
            waitHandel.WaitOne();
        }
        /// <summary>
        /// 后台线程提交数据
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void backgroundWorker1_ProgressChanged(object sender, ProgressChangedEventArgs e)
        {
            try
            {

                if (e.ProgressPercentage == -1)
                {
                    byte[] sendbuffer = e.UserState as byte[];
                    Console.WriteLine("TX: " + BitConverter.ToString(sendbuffer, 0).Replace("-", " "));
                    try
                    {
                        sw = File.AppendText(logfile);
                        sw.WriteLine(DateTime.Now.ToString() + " TX: " + BitConverter.ToString(sendbuffer, 0).Replace("-", " "));
                        sw.Close();
                    }
                    catch { }
                }

                else if (e.ProgressPercentage > 0)
                {
                    byte[] receive = new byte[e.ProgressPercentage];
                    Array.Copy(e.UserState as byte[], receive, e.ProgressPercentage);

                    Console.WriteLine("RX: " + BitConverter.ToString(receive, 0).Replace("-", " "));
                    try
                    {
                        sw = File.AppendText(logfile);
                        sw.WriteLine(DateTime.Now.ToString() + " RX: " + BitConverter.ToString(receive, 0).Replace("-", " "));
                        sw.Close();
                    }
                    catch { }

                    try
                    {
                        byte[] btemp = new byte[2] { receive[5], receive[4] };
                        int bufferlen = BitConverter.ToInt16(btemp, 0) + 6;
                        if (bufferlen == receive.Length)//报文长度正确
                        {
                            TCP tcptemp = new TCP(receive, m_len, m_startAddr, m_type);

                            if ((tcptemp.Responseread != null) && (tcptemp.Responseread.ByteNum != 0))
                            {

                                Console.WriteLine("RX: FC:{0} ", tcptemp.Responseread.FC);
                                try
                                {
                                    sw = File.AppendText(logfile);
                                    sw.WriteLine(DateTime.Now.ToString() + " RX: FC:{0} ", tcptemp.Responseread.FC);
                                    sw.Close();
                                }
                                catch { }

                                var datas = tcptemp.GetData();
                                foreach (var data in datas)
                                {
                                    //if (data.Addr == 0) continue;
                                    Console.WriteLine("RX: " + "addr:" + data.Addr.ToString() + " " +
                                                        "data:" + data.Data.ToString());
                                    try
                                    {
                                        sw = File.AppendText(logfile);
                                        sw.WriteLine("RX: " + "addr:" + data.Addr.ToString() + " " +
                                                            "data:" + data.Data.ToString());
                                        sw.Close();
                                    }
                                    catch { }

                                    numInf numtemp = new numInf();
                                    if (find.TryGetValue(data.Addr, out numtemp))
                                    {
                                        numtemp.val = Convert.ToSingle(data.Data);
                                        numtemp.dtm = DateTime.Now;

                                        find.Remove(data.Addr);
                                        find.Add(data.Addr, numtemp);
                                    }
                                    else
                                    {
                                        numtemp.val = Convert.ToSingle(data.Data);
                                        numtemp.dtm = DateTime.Now;
                                        find.Add(data.Addr, numtemp);
                                    }
                                }
                                sendFlag = true;
                                Console.WriteLine("\n");
                                try
                                {
                                    sw = File.AppendText(logfile);
                                    sw.WriteLine("\r\n");
                                    sw.Close();
                                }
                                catch { }
                            }//end if ((tcptemp.Responseread != null) && (tcptemp.Responseread.ByteNum != 0))
                        }//end if (bufferlen == buffer.Length)
                    }// end try
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine(ex.ToString());
                    }
                }
            }
            catch { }
        }
        /// <summary>
        /// 后台线程完成工作
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void backgroundWorker1_RunWorkerCompleted(object sender, RunWorkerCompletedEventArgs e)
        {
            Console.WriteLine("#######################################");
            ReadEnable = false;
            tmSend.Stop();
            tmSend.Dispose();
            tmCallAll.Stop();
            tmCallAll.Dispose();
            System.Threading.Thread.Sleep(1000);
            socket.Close();
        }
        /// <summary>
        /// 定时发送的定时器
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void tm_Elapsed(object sender, System.Timers.ElapsedEventArgs e)
        {
            try
            {
                TimeSpan currentSpan = DateTime.Now.Subtract(check);
                if (currentSpan.Seconds > m_config_Modbus.m_SendTaskInteval)
                {
                    sendFlag = true;
                }

                if (sendFlag == false) return;

                if (SendList.Count > 0)
                {
                    TCP tcptemp = SendList.Dequeue();
                    m_startAddr = tcptemp.Requestread.StartAddr;
                    m_len = tcptemp.Requestread.ReadNum; switch (tcptemp.Requestread.FC)
                    {
                        case FunctionCode.InputReg:
                            m_type = m_config_Modbus.m_inputregType;
                            break;
                        case FunctionCode.HoldReg:
                            m_type = m_config_Modbus.m_holdregType;
                            break;
                    }
                    byte[] SendBuffer = tcptemp.ToArray();
                    check = DateTime.Now;
                    socket.Send(SendBuffer);
                    backgroundWorker1.ReportProgress(-1, SendBuffer);
                    sendFlag = false;

                    //Console.WriteLine("TX: " + BitConverter.ToString(SendBuffer, 0).Replace("-", " "));
                    //sw = File.AppendText(logfile);
                    //sw.WriteLine(DateTime.Now.ToString() + " TX: " + BitConverter.ToString(SendBuffer, 0).Replace("-", " "));
                    //sw.Close();
                }
            }
            catch (Exception er)
            {
                Console.WriteLine(er.ToString());
                Reconnect();
                //isContinue = false;
            }
        }

        /// <summary>
        /// 定时发送全招换请求的定时器
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void callAll_Elapsed(object sender, System.Timers.ElapsedEventArgs e)
        {
            try
            {
                FileStream fs = new FileStream(logfile, FileMode.OpenOrCreate, FileAccess.Write);
                if (fs.Length > 1024000)//文件大小。byte
                {
                    while (true)
                    {
                        if (fs.CanWrite)
                        {
                            fs.Close();
                            try
                            {
                                StreamWriter sw1 = new StreamWriter(logfile);
                                string w = "";
                                sw1.Write(w);
                                sw1.Close();
                            }
                            catch { }
                            break;
                        }
                        Thread.Sleep(200);
                    }
                }
                fs.Close();
            }
            catch { }
            if (SendList.Count > 0) return;
            try
            {
                if ((m_config_Modbus.m_coilsLen > 0) && (m_config_Modbus.m_coilsStartAddr > 0))
                {
                    for (int i = 0; i < (m_config_Modbus.m_coilsLen / 2000 + 1); i++)
                    {
                        RequestData(FunctionCode.Coils, m_config_Modbus.m_coilsStartAddr - 1 + i * 2000,
                                    (i == m_config_Modbus.m_coilsLen / 2000 ? m_config_Modbus.m_coilsLen % 2000 : 2000));
                    }
                }
                if ((m_config_Modbus.m_inputsLen > 0) && (m_config_Modbus.m_inputsStartAddr > 0))
                {
                    //RequestData(FunctionCode.Inputs, 0, 10);
                    for (int i = 0; i < (m_config_Modbus.m_inputsLen / 2000 + 1); i++)
                    {
                        RequestData(FunctionCode.Inputs, m_config_Modbus.m_inputsStartAddr - 1 + i * 2000,
                                    (i == m_config_Modbus.m_inputsLen / 2000 ? m_config_Modbus.m_inputsLen % 2000 : 2000));
                    }
                }
                if ((m_config_Modbus.m_holdregLen > 0) && (m_config_Modbus.m_holdregStartAddr > 0))
                {
                    //RequestData(FunctionCode.HoldReg, 0, 10);
                    int lentemp = 0;
                    switch (m_config_Modbus.m_holdregType)
                    {
                        case 1:
                        case 5:
                            lentemp = 255 / 2;
                            break;
                        case 2:
                        case 3:
                        case 6:
                        case 7:
                        case 9:
                        case 10:
                            lentemp = 255 / 4;
                            break;
                        case 4:
                        case 8:
                            lentemp = 255 / 8;
                            break;

                    }
                    for (int i = 0; i < (m_config_Modbus.m_holdregLen / lentemp + 1); i++)
                    {
                        RequestData(FunctionCode.HoldReg, m_config_Modbus.m_holdregStartAddr - 1 + i * lentemp,
                                    (i == m_config_Modbus.m_holdregLen / lentemp ?
                                     m_config_Modbus.m_holdregLen % lentemp : lentemp));
                    }
                }
                if ((m_config_Modbus.m_inputregLen > 0) && (m_config_Modbus.m_inputregStartAddr > 0))
                {
                    //RequestData(FunctionCode.InputReg, 0, 10);
                    int lentemp = 0;
                    switch (m_config_Modbus.m_inputregType)
                    {
                        case 1:
                        case 5:
                            lentemp = 255 / 2;
                            break;
                        case 2:
                        case 3:
                        case 6:
                        case 7:
                        case 9:
                        case 10:
                            lentemp = 255 / 4;
                            break;
                        case 4:
                        case 8:
                            lentemp = 255 / 8;
                            break;

                    }
                    for (int i = 0; i < (m_config_Modbus.m_inputregLen / lentemp + 1); i++)
                    {
                        RequestData(FunctionCode.InputReg, m_config_Modbus.m_inputregStartAddr - 1 + i * lentemp,
                                    (i == m_config_Modbus.m_inputregLen / lentemp ?
                                    m_config_Modbus.m_inputregLen % lentemp : lentemp));
                    }
                }
            }
            catch { }
        }
        /// <summary>
        /// 读取数据
        /// </summary>
        public void RequestData(FunctionCode fc, int startAddr, int len)
        {
            if (len == 0) return;
            try
            {
                TCP temp = new TCP(m_config_Modbus.m_DeviceAddress, fc, startAddr, len);
                SendList.Enqueue(temp);
            }
            catch { }
        }
        /// <summary>
        /// 接收数据的函数
        /// </summary>
        private void ReceiveOnce()
        {
            byte[] ReceiveBuffer = new byte[1024];

            try
            {
                //check = DateTime.Now;
                socket.BeginReceive(ReceiveBuffer, 0, ReceiveBuffer.Length, SocketFlags.None, Received, ReceiveBuffer);
            }

            catch (SocketException erSocket)
            {
                if (erSocket.ErrorCode == 10054)
                {
                    Reconnect();
                }
            }   
        }
        /// <summary>
        /// 异步接收触发函数
        /// </summary>
        /// <param name="ar"></param>
        private void Received(IAsyncResult ar)
        {
            if (ReadEnable)
            {
                try
                {
                    int lenth = socket.EndReceive(ar);
                    check = DateTime.Now;
                    backgroundWorker1.ReportProgress(lenth, ar.AsyncState as byte[]);
                    ReceiveOnce();
                }
                catch { }
            }
        }
        private void Reconnect()
        {
            try
            {
                sw = File.AppendText(logfile);
                sw.WriteLine(DateTime.Now.ToString() + " 与服务器断开，重连接！");
                sw.Close();
            }
            catch { }

            socket.Close();
            Mconnect();
            System.Threading.Thread.Sleep(1000);
            ReadEnable = true;
            ReceiveOnce();
        }

        public void ProduceOPC()
        {
            fun = new OPCServerFUN();
            int BOOL = fun.RegisterOPC(m_config_Modbus.m_OPCServerName);
            if (BOOL == 0)
            {
                Console.WriteLine("RegisterOPC Failed!");
            }

            Thread t = new Thread(UpDataValue);
            t.Start();
        }
        private void UpDataValue()
        {
            Dictionary<int, IntPtr> list = new Dictionary<int, IntPtr>();
            list.Clear();
            DateTime time;
            float value;
            IntPtr intptr;
            while (true)
            {
                try
                {
                    foreach (var item in find)
                    {
                        if (list.TryGetValue(item.Key, out intptr))
                        {
                            time = item.Value.dtm;
                            value = item.Value.val;
                            fun.UpdateTagWithTimeStamp(intptr, (object)value, 0xc0, time);
                        }
                        else
                        {
                            IntPtr ii = fun.CreateTag(item.Key.ToString(), (object)(0.0f), 0xc0, 1);
                            list.Add(item.Key, ii);

                            time = item.Value.dtm;
                            value = item.Value.val;
                            fun.UpdateTagWithTimeStamp(ii, (object)value, 0xc0, time);
                        }
                    }
                }
                catch { }
                Thread.Sleep(1000);
            }
        }
    }

    //public class CONFG_Modbus
    //{
    //    private INI cfg;
    //    private int yx_StartAddress = 1;
    //    private int yx_EndAddress = 4096;
    //    private int yc_StartAddress = 16385;
    //    private int yc_EndAddress = 20480;
    //    private int ym_StartAddress = 25601;
    //    private int ym_EndAddress = 26112;

    //    private int DeviceAddress = 1;
    //    private float SendRate = 1;
    //    private float SendTaskInteval = 5;
    //    private int ycType=1;
    //    private string modbusType="TCP";

    //    private string IPAddress = "127.0.0.1";
    //    private int Port = 502;

    //    public string m_IPAddress
    //    {
    //        get { return IPAddress; }
    //        set { IPAddress = value; }
    //    }
    //    public int m_Port
    //    {
    //        get { return Port; }
    //        set { Port = value; }
    //    }
    //    public int m_yxStartAddress
    //    {
    //        get { return yx_StartAddress; }
    //        set { yx_StartAddress = value; }
    //    }
    //    public int m_yxEndAddress
    //    {
    //        get { return yx_EndAddress; }
    //        set { yx_EndAddress = value; }
    //    }
    //    public int m_ycStartAddress
    //    {
    //        get { return yc_StartAddress; }
    //        set { yc_StartAddress = value; }
    //    }
    //    public int m_ycEndAddress
    //    {
    //        get { return yc_EndAddress; }
    //        set { yc_EndAddress = value; }
    //    }
    //    public int m_ymStartAddress
    //    {
    //        get { return ym_StartAddress; }
    //        set { ym_StartAddress = value; }
    //    }
    //    public int m_ymEndAddress
    //    {
    //        get { return ym_EndAddress; }
    //        set { ym_EndAddress = value; }
    //    }
    //    public int m_DeviceAddress
    //    {
    //        get { return DeviceAddress; }
    //        set { DeviceAddress = value; }
    //    }
    //    public float m_SendRate
    //    {
    //        get { return SendRate; }
    //        set { SendRate = value; }
    //    }
    //    public float m_SendTaskInteval
    //    {
    //        get { return SendTaskInteval; }
    //        set { SendTaskInteval = value; }
    //    }
    //    public int m_YcType
    //    {
    //        get { return ycType; }
    //        set { ycType = value; }
    //    }
    //    public string m_ModbusType
    //    {
    //        get { return modbusType; }
    //        set { modbusType = value; }
    //    }

    //    public CONFG_ModbusTCP(string configFile)
    //    {
    //        SetConfig_ModbusTCP(configFile);
    //    }

    //    public void SetConfig_ModbusTCP(string configFile)
    //    {
    //        FileStream fs = new FileStream(configFile, FileMode.OpenOrCreate);
    //        fs.Close();
    //        try
    //        {
    //            cfg = new INI(AppDomain.CurrentDomain.SetupInformation.ApplicationBase + configFile);
    //            m_IPAddress = cfg.GetVal("TCP/IP", "IPAddress");
    //            m_Port = Convert.ToInt32(cfg.GetVal("TCP/IP", "Port"));
    //            m_DeviceAddress = Convert.ToByte(cfg.GetVal("Advanced", "DeviceAddress"));
    //            m_SendRate = Convert.ToSingle(cfg.GetVal("Advanced", "SendRate"));
    //            m_SendTaskInteval = Convert.ToSingle(cfg.GetVal("Advanced", "SendTaskInteval"));
    //            m_YcType = Convert.ToInt32(cfg.GetVal("Advanced", "ycType"));
    //            m_ModbusType = cfg.GetVal("Advanced", "ModbusType");

    //            m_yxStartAddress = Convert.ToInt32(cfg.GetVal("DATA", "yx_StartAddress"));
    //            m_ycStartAddress = Convert.ToInt32(cfg.GetVal("DATA", "yc_StartAddress"));
    //            m_ymStartAddress = Convert.ToInt32(cfg.GetVal("DATA", "ym_StartAddress"));
    //        }
    //        catch
    //        {
    //            StreamWriter sw1 = new StreamWriter(configFile);
    //            string w = "";
    //            sw1.Write(w);
    //            sw1.Close();
    //            StreamWriter sw = File.AppendText(configFile);
    //            sw.WriteLine("[SerialPort]");
    //            sw.WriteLine("COM=COM1");
    //            sw.WriteLine("BaudRate=9600");
    //            sw.WriteLine("Parity=0");
    //            sw.WriteLine("DataBits=8");
    //            sw.WriteLine("StopBits=1");
    //            sw.WriteLine("Handshake=0");

    //            sw.WriteLine("[TCP/IP]");
    //            sw.WriteLine("IPAddress=127.0.0.1");
    //            sw.WriteLine("Port=502");

    //            sw.WriteLine("[Advanced]");
    //            sw.WriteLine("DeviceAddress=1");
    //            sw.WriteLine("SendRate=1");
    //            sw.WriteLine("SendTaskInteval=5");
    //            sw.WriteLine("ycType=1");
    //            sw.WriteLine("ModbusType=RTU");

    //            sw.WriteLine("[DATA]");
    //            sw.WriteLine("yx_StartAddress=1");
    //            sw.WriteLine("yc_StartAddress=16385");
    //            sw.WriteLine("ym_StartAddress=25601");
    //            sw.Close();
    //        }
    //    }
    //}
}
