using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using TCP104Library;
using CSOPCServerLib;

namespace SISCell
{
    public class I_104 : iProtocol
    {
        private System.ComponentModel.BackgroundWorker backgroundWorker1;
        //TCP连接
        //Socket socket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
        Socket socket;
        //发送队列
        Queue<APDUClass> SendList = new Queue<APDUClass>();
        //等待语句柄。挂起后台线程时阻塞使用
        EventWaitHandle waitHandel = new EventWaitHandle(false, EventResetMode.AutoReset);
        //检查周期
        DateTime check = new DateTime();
        //定时器发送报文周期
        int sendRate;
        //定时器读YM周期
        int ymRate;
        //定时器总招周期
        int callAllRate;
        //定时器_发送报文
        System.Timers.Timer tmSend;
        //定时器_读电量数据
        System.Timers.Timer tmYM;
        //定时器_读电量数据
        System.Timers.Timer tmCallAll;
        //允许读标志。关闭后台线程时同步信号使用
        bool ReadEnable = false;
        //发计数。程序中使用，实际应用中暂未起作用
        short sr = 0;
        //收计数。程序中使用，实际应用中暂未起作用
        short nr = 0;
        bool nrflag = false;
        //建立数据索引，查找已初始化的数据更新
        Dictionary<int, numInf> find = new Dictionary<int, numInf>();
        //配置信息
        CONFG_104 m_config_104;
        OPCServerFUN fun = null;

        //输出文本路径
        StreamWriter sw;
        string filestr, logfile;
        /// <summary>
        /// 构造函数
        /// </summary>
        public I_104()
        {
            this.backgroundWorker1 = new System.ComponentModel.BackgroundWorker();
            this.backgroundWorker1.WorkerReportsProgress = true;
            this.backgroundWorker1.WorkerSupportsCancellation = true;
            this.backgroundWorker1.DoWork += new System.ComponentModel.DoWorkEventHandler(this.backgroundWorker1_DoWork);
            this.backgroundWorker1.RunWorkerCompleted += new System.ComponentModel.RunWorkerCompletedEventHandler(this.backgroundWorker1_RunWorkerCompleted);
            this.backgroundWorker1.ProgressChanged += new System.ComponentModel.ProgressChangedEventHandler(this.backgroundWorker1_ProgressChanged);

            m_config_104 = new CONFG_104("config\\104.ini");
            sendRate =Convert.ToInt32( m_config_104.m_SendRate * 1000);
            ymRate =Convert.ToInt32(  m_config_104.m_YmRate * 1000);
            callAllRate =Convert.ToInt32(  m_config_104.m_CallAllRate * 1000);

            
            logfile = "log.txt";
            try
            {
                FileStream fs = new FileStream(logfile, FileMode.OpenOrCreate);
                fs.Close();
                sw = File.AppendText(logfile);
                sw.WriteLine("初始化成功！");
                sw.Close();
            }
            catch { }
        }
        ~I_104()
        {
            if (fun != null)
                fun.UnregisterS(m_config_104.m_OPCServerName);
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
            IPEndPoint ServerInfo = new IPEndPoint(IPAddress.Parse(m_config_104.m_IPAddress), m_config_104.m_Port);
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
            SendList.Enqueue(new APDUClass(new APCIClassUFormat(APCIClassUFormat.UFormatType.StartSet), null));
            sr = 0;
            nr = 0;
            //while (true)
            //{
            //    check = DateTime.Now;
            //    byte[] SendBuffer = (new APDUClass(new APCIClassUFormat(APCIClassUFormat.UFormatType.StartSet), null)).ToArray();
            //    socket.Send(SendBuffer);
            //    try
            //    {
            //        sw = File.AppendText(logfile);
            //        sw.WriteLine(DateTime.Now.ToString() + " TX: " + BitConverter.ToString(SendBuffer).Replace('-',' '));
            //        sw.Close();
            //    }
            //    catch { }
            //    Thread.Sleep(1000);
            //    byte[] receiveBuffer = new byte[1024];
            //    int nm = socket.Receive(receiveBuffer);
            //    try
            //    {
            //        sw = File.AppendText(logfile);
            //        sw.WriteLine(DateTime.Now.ToString() + " RX: " + BitConverter.ToString(receiveBuffer, 0, nm).Replace('-', ' '));
            //        sw.Close();
            //    }
            //    catch { }
            //    if ((receiveBuffer[0] == 0x68) && ((receiveBuffer[2] & 0x0b) == 0x0b))
            //        break;
            //}

            //ASDUClass clockBuffer = new ASDUClass();
            //clockBuffer.Pack(ASDUClass.TransRes.Active, ASDUClass.FunType.ClockConfirm, m_config_104.m_PublicAddress);
            ////SendList.Enqueue(new APDUClass(new APCIClassIFormat(sr++, nr), clockBuffer));
            //while (true)
            //{
            //    check = DateTime.Now;
            //    byte[] SendBuffer = (new APDUClass(new APCIClassIFormat(sr++, nr), clockBuffer)).ToArray();
            //    socket.Send(SendBuffer);
            //    try
            //    {
            //        sw = File.AppendText(logfile);
            //        sw.WriteLine(DateTime.Now.ToString() + " TX: " + BitConverter.ToString(SendBuffer));
            //        sw.Close();
            //    }
            //    catch { }
            //    Thread.Sleep(1000);
            //    byte[] receiveBuffer = new byte[1024];
            //    int nm = socket.Receive(receiveBuffer);
            //    try
            //    {
            //        sw = File.AppendText(logfile);
            //        sw.WriteLine(DateTime.Now.ToString() + " RX: " + BitConverter.ToString(receiveBuffer, 0, nm));
            //        sw.Close();
            //    }
            //    catch { }
            //    if ((receiveBuffer[0] == 0x68) && (receiveBuffer[6] == 0x67) && (receiveBuffer[8] == 0x07))
            //        break;
            //}

            //ASDUClass calBuffer = new ASDUClass();
            //calBuffer.Pack(ASDUClass.TransRes.Active, ASDUClass.FunType.CalAll, m_config_104.m_PublicAddress);
            //SendList.Enqueue(new APDUClass(new APCIClassIFormat(sr++, nr), calBuffer));
           

            //ASDUClass calymBuffer = new ASDUClass();
            //calymBuffer.Pack(ASDUClass.TransRes.Active, ASDUClass.FunType.CalEnergyPulse, m_config_104.m_PublicAddress);
            //SendList.Enqueue(new APDUClass(new APCIClassIFormat(sr++, nr), calymBuffer));
            //ASDUClass readymBuffer = new ASDUClass();
            //readymBuffer.SetData_QCC(0x05, m_config_104.m_PublicAddress);
            //SendList.Enqueue(new APDUClass(new APCIClassIFormat(sr++, nr), readymBuffer));

            ReadEnable = false;
            return true;

            #region 或者
            //SendList.Clear();
            //APDUClass myBuffer = new APDUClass(new APCIClassUFormat(APCIClassUFormat.UFormatType.StartSet), null);
            //while (true)
            //{
            //    try
            //    {
            //        socket.Send(myBuffer.ToArray());
            //        System.Threading.Thread.Sleep(500);
            //        byte[] bb = new byte[50];
            //        while (true)
            //        {
            //            System.Threading.Thread.Sleep(500);
            //            if (socket.Receive(bb) == 6)
            //            {
            //                if ((bb[0] == 0x68) && ((bb[2] & 0x0B) == 0x0B)) break;
            //                else socket.Send(myBuffer.ToArray());
            //            }
            //        }
            //    }
            //    catch (Exception er)
            //    {
            //        Console.Write(er.ToString());
            //        socket.Close(); Connect();
            //        return;
            //    }
            //    break;
            //}
            //sr = 0;
            //nr = 0;
            //ASDUClass clockBuffer = new ASDUClass();
            //clockBuffer.Pack(ASDUClass.TransRes.Active, ASDUClass.FunType.ClockConfirm);
            //ASDUClass calBuffer = new ASDUClass();
            //calBuffer.Pack(ASDUClass.TransRes.Active, ASDUClass.FunType.CalAll);
            //SendList.Enqueue(new APDUClass(new APCIClassIFormat(sr++, nr), clockBuffer));
            //SendList.Enqueue(new APDUClass(new APCIClassIFormat(sr++, nr), calBuffer));
            //ReadEnable = false;
            #endregion
        }

        public void DisConnect()
        {
            ReadEnable = false;
            tmSend.Stop();
            tmSend.Dispose();
            //tmYM.Stop();
            //tmYM.Dispose();
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
                    index = int.Parse(nrst[i].srcId);
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
        public static void UnPackString(string str)
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

        }
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

            //tmYM = new System.Timers.Timer();
            //tmYM.AutoReset = true;
            //tmYM.Interval = ymRate;
            //tmYM.Elapsed += new System.Timers.ElapsedEventHandler(ym_Elapsed);
            //tmYM.Start();

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
                FileInfo file = new FileInfo(logfile);
                long size = file.Length;//文件大小。byte
                if (size > 1024000)
                {
                    StreamWriter sw1 = new StreamWriter(logfile);
                    string w = "";
                    sw1.Write(w);
                    sw1.Close();
                }
            }
            catch { }

            if (e.ProgressPercentage == -1)
            {
                APDUClass temp = new APDUClass(e.UserState as byte[]);
                APCIClass.UISFormat dataFormat = temp.GetApciType();
                if (temp != null)
                {
                    try 
                    { 
                        sw = File.AppendText(logfile); 
                        sw.WriteLine(DateTime.Now.ToString() + " TX: " + "Type:" + temp.GetAsduType().ToString() + " Res:" + temp.Res.ToString()
                                        + " SR:" + temp.GetSR().ToString() + " NR:" + temp.GetNR().ToString()
                                        + "\r\n" + temp.ApciToString() + temp.AsduToString() + "\r\n");
                        sw.Close();
                    }
                    catch {}
                    Console.WriteLine(DateTime.Now.ToString() + " TX: " + "Type:" + temp.GetAsduType().ToString() + " Res:" + temp.Res.ToString()
                                             + " SR:" + temp.GetSR().ToString() + " NR:" + temp.GetNR().ToString()
                                             + "\n" + temp.ApciToString() + temp.AsduToString() + "\r\n");
                }
            }
            else if (e.ProgressPercentage > 0)
            {
                byte[] receive = new byte[e.ProgressPercentage];
                Array.Copy(e.UserState as byte[], receive, e.ProgressPercentage);
                if (receive[0] == 0x68)
                {
                    int i = 0;
                    while (i < receive.Length)
                    {
                        try
                        {
                            int recevlen = receive[i + 1];
                            byte[] receivetemp = new byte[recevlen +2];
                            Array.Copy(receive, i, receivetemp, 0, receivetemp.Length);
                            i = i + receivetemp.Length;

                            APDUClass temp = new APDUClass(receivetemp);
                            APCIClass.UISFormat dataFormat = temp.GetApciType();
                            //if (dataFormat == APCIClass.UISFormat.I && !(nr > temp.GetSR()))
                            if (dataFormat == APCIClass.UISFormat.I)
                            {
                                nr = (short)temp.GetSR();
                                if (nr > short.MaxValue)
                                {
                                    nr = 0;
                                }
                                nrflag = true;
                                if (((nr % m_config_104.m_AckNW) == m_config_104.m_AckNW - 1)||(sr == 0))
                                {
                                    SendList.Enqueue(new APDUClass(new APCIClassSFormat(nr), null));
                                    for (int id = 0; id < SendList.Count - 1; id++)
                                    {
                                        APDUClass tempsend = SendList.Dequeue();
                                        SendList.Enqueue(tempsend);
                                    }
                                    nrflag = false;
                                }
                                if ((temp.GetAsduType() == ASDUClass.FunType.CalAll) && (temp.Res == ASDUClass.TransRes.ActiveEnd))
                                {
                                    ASDUClass calymBuffer = new ASDUClass();
                                    calymBuffer.Pack(ASDUClass.TransRes.Active, ASDUClass.FunType.CalEnergyPulse, m_config_104.m_PublicAddress);
                                    SendList.Enqueue(new APDUClass(new APCIClassIFormat(sr++, nr), calymBuffer));
                                    ASDUClass readymBuffer = new ASDUClass();
                                    readymBuffer.SetData_QCC(0x05, m_config_104.m_PublicAddress);
                                    SendList.Enqueue(new APDUClass(new APCIClassIFormat(sr++, nr), readymBuffer));
                                }
                            }
                            if (dataFormat == APCIClass.UISFormat.U)
                            {
                                if ((APCIClassUFormat.UFormatType)temp.GetApci().GetControlByte() == APCIClassUFormat.UFormatType.StartSet)
                                    SendList.Enqueue(new APDUClass(new APCIClassUFormat(APCIClassUFormat.UFormatType.StartConfirm), null));
                                if ((APCIClassUFormat.UFormatType)temp.GetApci().GetControlByte() == APCIClassUFormat.UFormatType.TestSet)
                                    SendList.Enqueue(new APDUClass(new APCIClassUFormat(APCIClassUFormat.UFormatType.TestConfirm), null));
                                if ((APCIClassUFormat.UFormatType)temp.GetApci().GetControlByte() == APCIClassUFormat.UFormatType.StopSet)
                                    SendList.Enqueue(new APDUClass(new APCIClassUFormat(APCIClassUFormat.UFormatType.StopConfirm), null));
                                if ((APCIClassUFormat.UFormatType)temp.GetApci().GetControlByte() == APCIClassUFormat.UFormatType.StartConfirm)
                                {
                                    ASDUClass calBuffer = new ASDUClass();
                                    calBuffer.Pack(ASDUClass.TransRes.Active, ASDUClass.FunType.CalAll, m_config_104.m_PublicAddress);
                                    SendList.Enqueue(new APDUClass(new APCIClassIFormat(sr++, nr), calBuffer));
                                }
                            }
                            ////对方发来确认帧，在核实确认序号等于或小于本分的I帧发送序号，再次发送I帧
                            ////此部分在“实现发送控制命令”时，给以完成。
                            //else if (dataFormat == APCIClass.UISFormat.S)
                            //{
                            //    if (nr > short.MaxValue)
                            //    {
                            //        nr = 0;
                            //    }
                            //    Console.WriteLine(dataFormat.ToString());
                            //    SendList.Enqueue(new APDUClass(new APCIClassSFormat(nr++), null));
                            //}

                            if (temp != null)
                            {
                                try
                                {
                                    sw = File.AppendText(logfile);
                                    sw.WriteLine(DateTime.Now.ToString() + " RX: " + "Type:" + temp.GetAsduType().ToString() + " Res:" + temp.Res.ToString()
                                                        + " SR:" + temp.GetSR().ToString() + " NR:" + temp.GetNR().ToString()
                                                        + "\r\n" + BitConverter.ToString(receivetemp, 0).Replace("-", " "));
                                    sw.Close();
                                }
                                catch { }

                                Console.WriteLine(DateTime.Now.ToString() + " RX: " + "Type:" + temp.GetAsduType().ToString() + " Res:" + temp.Res.ToString()
                                                    + " SR:" + temp.GetSR().ToString() + " NR:" + temp.GetNR().ToString()
                                                    + "\n" + BitConverter.ToString(receivetemp, 0).Replace("-", " "));

                                var datas = temp.GetData();
                                foreach (var data in datas)
                                {
                                    if (data.Addr == 0) continue;
                                    try
                                    {
                                        sw = File.AppendText(logfile);
                                        sw.WriteLine("RX " + "addr:" + data.Addr.ToString() + " " +
                                                    "data:" + data.Data.ToString() + " " +
                                                    "time:" + data.Time.ToString());
                                        sw.Close();
                                    }
                                    catch { }

                                    //Console.WriteLine("RX " + "addr:" + data.Addr.ToString() + " " +
                                    //                "data:" + data.Data.ToString() + " " +
                                    //                "time:" + data.Time.ToString());

                                    numInf numtemp = new numInf();
                                    if (find.TryGetValue(data.Addr, out numtemp))
                                    {
                                        numtemp.val = Convert.ToSingle(data.Data);
                                        if (data.Time != null)
                                        {
                                            numtemp.dtm = Convert.ToDateTime(data.Time);
                                        }
                                        else numtemp.dtm = DateTime.Now;
                                        find.Remove(data.Addr);
                                        find.Add(data.Addr, numtemp);
                                    }
                                    else
                                    {
                                        numtemp.val = Convert.ToSingle(data.Data);
                                        if (data.Time != null)
                                        {
                                            numtemp.dtm = Convert.ToDateTime(data.Time);
                                        }
                                        else numtemp.dtm = DateTime.Now;
                                        find.Add(data.Addr, numtemp);
                                    }
                                }
                                try
                                {
                                    sw = File.AppendText(logfile);
                                    sw.WriteLine("\r\n");
                                    sw.Close();
                                }
                                catch { }
                                Console.WriteLine("\r\n");
                            }
                        }
                        catch (Exception ex)
                        {
                            System.Diagnostics.Debug.WriteLine(ex.ToString());
                        }
                    }
                }
            }
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
            tmYM.Stop();
            tmYM.Dispose();
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
                if (SendList.Count > 0)
                {
                    APDUClass temp = SendList.Dequeue();
                    temp.SetNR(nr);
                    byte[] SendBuffer = temp.ToArray();
                    check = DateTime.Now;
                    socket.Send(SendBuffer);
                    ReceiveOnce();
                    backgroundWorker1.ReportProgress(-1, SendBuffer);
                }
                //心跳检测
                else
                {
                    TimeSpan currentSpan = DateTime.Now.Subtract(check);
                    if (currentSpan.Seconds > m_config_104.m_SendTaskInteval)
                    {
                        if (nrflag)
                        {
                            SendList.Enqueue(new APDUClass(new APCIClassSFormat(nr), null));
                            check = DateTime.Now;
                            nrflag = false;
                        }
                        else
                            SendList.Enqueue(new APDUClass(new APCIClassUFormat(APCIClassUFormat.UFormatType.TestSet), null));
                    }
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
        /// 定时发送遥脉请求的定时器
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void ym_Elapsed(object sender, System.Timers.ElapsedEventArgs e)
        {
            ASDUClass calymBuffer = new ASDUClass();
            calymBuffer.Pack(ASDUClass.TransRes.Active, ASDUClass.FunType.CalEnergyPulse, m_config_104.m_PublicAddress);
            SendList.Enqueue(new APDUClass(new APCIClassIFormat(sr++, nr), calymBuffer));
            ASDUClass readymBuffer = new ASDUClass();
            readymBuffer.SetData_QCC(0x05, m_config_104.m_PublicAddress);
            SendList.Enqueue(new APDUClass(new APCIClassIFormat(sr++, nr), readymBuffer));
        }
        /// <summary>
        /// 定时发送总招请求的定时器
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void callAll_Elapsed(object sender, System.Timers.ElapsedEventArgs e)
        {
            ASDUClass calBuffer = new ASDUClass();
            calBuffer.Pack(ASDUClass.TransRes.Active, ASDUClass.FunType.CalAll, m_config_104.m_PublicAddress);
            SendList.Enqueue(new APDUClass(new APCIClassIFormat(sr++, nr), calBuffer));
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
            //catch (Exception er)
            //{
            //    isContinue = false;
            //    //表示接收出现问题，此时为SOCKET出现问题
            //}       
        }
        /// <summary>
        /// 异步接收触发函数
        /// </summary>
        /// <param name="ar"></param>
        private void Received(IAsyncResult ar)
        {
            //if (ReadEnable)
            {
                try
                {
                    int lenth = socket.EndReceive(ar);
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

            try
            {
                if (socket.Connected) return;
                socket.Close();
                if (Mconnect())
                {
                    System.Threading.Thread.Sleep(1000);
                    ReadEnable = true;
                    ReceiveOnce();
                }
            }
            catch { }
        }

        public void ProduceOPC()
        {
            fun = new OPCServerFUN();
            int BOOL = fun.RegisterOPC(m_config_104.m_OPCServerName);
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

    public class CONFG_104
    {
        private INI cfg;
        private int yx_StartAddress = 1;
        private int yx_EndAddress = 4096;
        private int yc_StartAddress = 16385;
        private int yc_EndAddress = 20480;
        private int ym_StartAddress = 25601;
        private int ym_EndAddress = 26112;

        private int PublicAddress = 1;
        private float SendRate = 1;
        private float YmRate = 300;
        private int AckNW = 8;
        private float CallAllRate = 60;
        private int Port = 2404;
        private float SendTaskInteval = 5;

        private int DataTye_Len = 1;
        private int SQ_Len = 1;
        private int TransRes_Len = 2;
        private int PublicAddress_Len = 2;
        private int DataAddress_Len = 3;
        private string IPAddress = "";
        private string OPC_ServerName = "SAC.OPC";

        public string m_IPAddress
        {
            get { return IPAddress; }
            set { IPAddress = value; }
        }
        public string m_OPCServerName
        {
            get { return OPC_ServerName; }
            set { OPC_ServerName = value; }
        }
        public int m_yxStartAddress
        {
            get { return yx_StartAddress; }
            set { yx_StartAddress = value; }
        }
        public int m_yxEndAddress
        {
            get { return yx_EndAddress; }
            set { yx_EndAddress = value; }
        }
        public int m_ycStartAddress
        {
            get { return yc_StartAddress; }
            set { yc_StartAddress = value; }
        }
        public int m_ycEndAddress
        {
            get { return yc_EndAddress; }
            set { yc_EndAddress = value; }
        }
        public int m_ymStartAddress
        {
            get { return ym_StartAddress; }
            set { ym_StartAddress = value; }
        }
        public int m_ymEndAddress
        {
            get { return ym_EndAddress; }
            set { ym_EndAddress = value; }
        }
        public int m_PublicAddress
        {
            get { return PublicAddress; }
            set { PublicAddress = value; }
        }
        public float m_SendRate
        {
            get { return SendRate; }
            set { SendRate = value; }
        }
        public float m_YmRate
        {
            get { return YmRate; }
            set { YmRate = value; }
        }
        public float m_CallAllRate
        {
            get { return CallAllRate; }
            set { CallAllRate = value; }
        }
        public int m_AckNW
        {
            get { return AckNW; }
            set { AckNW = value; }
        }
        public int m_Port
        {
            get { return Port; }
            set { Port = value; }
        }
        public float m_SendTaskInteval
        {
            get { return SendTaskInteval; }
            set { SendTaskInteval = value; }
        }
        public int m_DataTye_Len
        {
            get { return DataTye_Len; }
            set { DataTye_Len = value; }
        }
        public int m_SQ_Len
        {
            get { return SQ_Len; }
            set { SQ_Len = value; }
        }
        public int m_TransRes_Len
        {
            get { return TransRes_Len; }
            set { TransRes_Len = value; }
        }
        public int m_PublicAddress_Len
        {
            get { return PublicAddress_Len; }
            set { PublicAddress_Len = value; }
        }
        public int m_DataAddress_Len
        {
            get { return DataAddress_Len; }
            set { DataAddress_Len = value; }
        }
        public CONFG_104(string configFile)
        {
            SetConfig_104(configFile);
        }

        public void SetConfig_104(string configFile)
        {
            FileStream fs = new FileStream(configFile, FileMode.OpenOrCreate);
            fs.Close();
            //StreamReader sr = new StreamReader(configFile, Encoding.Default); 
            //string s;
            //string[] word;
            //char[] separator = { '=' };
            //int flag = 0;
            //while ((s = sr.ReadLine()) != null)
            //{
            //    word = s.Split(separator);
            //    if (word[0] == "IPAddress")       m_IPAddress = word[1];
            //    if (word[0] == "Port")            m_Port = Convert.ToInt32(word[1]);
            //    if (word[0] == "PublicAddress")   m_PublicAddress = Convert.ToInt32(word[1]);
            //    if (word[0] == "SendRate")        m_SendRate = Convert.ToInt32(word[1]);
            //    if (word[0] == "YmRate")          m_YmRate = Convert.ToInt32(word[1]);
            //    if (word[0] == "CallAllRate")     m_CallAllRate = Convert.ToInt32(word[1]);
            //    if (word[0] == "SendTaskInteval") m_SendTaskInteval = Convert.ToInt32(word[1]);
            //    if (word[0] == "ACKNW")           m_AckNW = Convert.ToInt32(word[1]);

            //    //if (word[0] == "DataTye_Len")     m_DataTye_Len = Convert.ToInt32(word[1]);
            //    //if (word[0] == "SQ_Len")          m_SQ_Len = Convert.ToInt32(word[1]);
            //    //if (word[0] == "TransRes_Len")        m_TransRes_Len = Convert.ToInt32(word[1]);
            //    //if (word[0] == "PublicAddress_Len")   m_PublicAddress_Len = Convert.ToInt32(word[1]);
            //    //if (word[0] == "DataAddress_Len")     m_DataAddress_Len = Convert.ToInt32(word[1]);

            //    if (word[0] == "yx_StartAddress")     m_yxStartAddress = Convert.ToInt32(word[1]);
            //    if (word[0] == "yx_EndAddress")       m_yxEndAddress = Convert.ToInt32(word[1]);
            //    if (word[0] == "yc_StartAddress")     m_ycStartAddress = Convert.ToInt32(word[1]);
            //    if (word[0] == "yc_EndAddress")       m_yxEndAddress = Convert.ToInt32(word[1]);
            //    if (word[0] == "ym_StartAddress")     m_ymStartAddress = Convert.ToInt32(word[1]);
            //    if (word[0] == "ym_EndAddress")       m_ymEndAddress = Convert.ToInt32(word[1]);
            //    flag++;
            //}
            //sr.Close();
            //try
            {
                cfg = new INI(AppDomain.CurrentDomain.SetupInformation.ApplicationBase + configFile);
                m_IPAddress = cfg.GetVal("Advanced", "IPAddress");
                m_OPCServerName = cfg.GetVal("Advanced", "OPCServerName");
                m_Port = Convert.ToInt32(cfg.GetVal("Advanced", "Port"));
                m_PublicAddress = Convert.ToInt32(cfg.GetVal("Advanced", "PublicAddress"));
                m_SendRate = Convert.ToSingle(cfg.GetVal("Advanced", "SendRate"));
                m_YmRate = Convert.ToSingle(cfg.GetVal("Advanced", "YmRate"));
                m_CallAllRate = Convert.ToSingle(cfg.GetVal("Advanced", "CallAllRate"));
                m_SendTaskInteval = Convert.ToSingle(cfg.GetVal("Advanced", "SendTaskInteval"));
                m_AckNW = Convert.ToInt32(cfg.GetVal("Advanced", "ACKNW"));
                m_yxStartAddress = Convert.ToInt32(cfg.GetVal("DATA", "yx_StartAddress"));
                m_ycStartAddress = Convert.ToInt32(cfg.GetVal("DATA", "yc_StartAddress"));
                m_ymStartAddress = Convert.ToInt32(cfg.GetVal("DATA", "ym_StartAddress"));
            }
            //catch
            //{
            //    StreamWriter sw1 = new StreamWriter(configFile);
            //    string w = "";
            //    sw1.Write(w);
            //    sw1.Close();
            //    StreamWriter sw = File.AppendText(configFile);
            //    sw.WriteLine("[Advanced]");
            //    sw.WriteLine("IPAddress=127.0.0.1");
            //    sw.WriteLine("OPCServerName=SAC.OPC.104");
            //    sw.WriteLine("SendRate=1");
            //    sw.WriteLine("YmRate=300");
            //    sw.WriteLine("ACKNW=8");
            //    sw.WriteLine("CallAllRate=60");
            //    sw.WriteLine("SendTaskInteval=5");
            //    sw.WriteLine("PublicAddress=1");
            //    sw.WriteLine("Port=2404");
            //    sw.WriteLine("[DATA]");
            //    sw.WriteLine("yx_StartAddress=1");
            //    sw.WriteLine("yc_StartAddress=16385");
            //    sw.WriteLine("ym_StartAddress=25601");
            //    sw.Close();
            //}
        }
    }
}
