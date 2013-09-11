using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using ModbusLibrary;
using CSOPCServerLib;

namespace SISCell
{
    public class I_ModbusTCP : iProtocol
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
        //int m_type = 0;
        //允许读标志。关闭后台线程时同步信号使用
        bool ReadEnable = false;
        bool sendFlag;

        //建立数据索引，查找已初始化的数据更新
        Dictionary<string, numInf> find = new Dictionary<string, numInf>();
        //配置信息
        CONFG_Modbus m_config_Modbus;
        OPCServerFUN fun = null;
        
        
        /// <summary>
        /// dataType(addr,type)
        /// </summary>
        Dictionary<string, int> dataType = new Dictionary<string, int>();
        int startInputAddr = 999999;
        int startCoilAddr = 999999;
        int startInputRegAddr = 999999;
        int startHoldRegAddr = 999999;
        int numInput = 0;
        int numCoil = 0;
        int numInputReg = 0;
        int numHoldReg = 0;

        //输出文本路径
        StreamWriter sw;
        string logfile;
        /// <summary>
        /// 构造函数
        /// </summary>
        public I_ModbusTCP()
        {
            this.backgroundWorker1 = new System.ComponentModel.BackgroundWorker();
            this.backgroundWorker1.WorkerReportsProgress = true;
            this.backgroundWorker1.WorkerSupportsCancellation = true;
            this.backgroundWorker1.DoWork += new System.ComponentModel.DoWorkEventHandler(this.backgroundWorker1_DoWork);
            this.backgroundWorker1.RunWorkerCompleted += new System.ComponentModel.RunWorkerCompletedEventHandler(this.backgroundWorker1_RunWorkerCompleted);
            this.backgroundWorker1.ProgressChanged += new System.ComponentModel.ProgressChangedEventHandler(this.backgroundWorker1_ProgressChanged);

            m_config_Modbus = new CONFG_Modbus("config\\Modbus.ini");
            sendRate = Convert.ToInt32(m_config_Modbus.m_SendRate * 1000);
            callAllRate = sendRate * 4;

            logfile = "log.txt";
            FileStream fs = new FileStream(logfile, FileMode.OpenOrCreate);
            fs.Close();
            try
            {
                sw = File.AppendText(logfile);
                sw.WriteLine("初始化成功！");
                sw.Close();
            }
            catch { }
        }
	    ~I_ModbusTCP()
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
            dataType.Clear();
            for (int i = 0; i < nrst.Length; i++)
            {
                string[] arry = nrst[i].srcId.Split('_');
                dataType.Add(arry[0]+int.Parse(arry[1]).ToString("d4"), nrst[i].datatype);

                if (nrst[i].srcId.StartsWith("0"))
                {
                    if (startCoilAddr > int.Parse(arry[1])) startCoilAddr = int.Parse(arry[1]);
                    numCoil++;
                }
                if (nrst[i].srcId.StartsWith("1"))
                {
                    int itemp = int.Parse(arry[1]);
                    if (startInputAddr > itemp) startInputAddr = itemp;
                    numInput++;
                }
                if (nrst[i].srcId.StartsWith("3"))
                {
                    int itemp = int.Parse(arry[1]);
                    if (startInputRegAddr > itemp) startInputRegAddr = itemp;

                    switch (nrst[i].datatype)
                    {
                        case 1:
                        case 5:
                            numInputReg += 1;
                            break;
                        case 2:
                        case 3:
                        case 6:
                        case 7:
                        case 9:
                        case 10:
                            numInputReg += 2;
                            break;
                        case 4:
                        case 8:
                            numInputReg += 4;
                            break;
                    }
                }
                if (nrst[i].srcId.StartsWith("4"))
                {
                    int itemp = int.Parse(arry[1]);
                    if (startHoldRegAddr > itemp) startHoldRegAddr = itemp;

                    switch (nrst[i].datatype)
                    {
                        case 1:
                        case 5:
                            numHoldReg += 1;
                            break;
                        case 2:
                        case 3:
                        case 6:
                        case 7:
                        case 9:
                        case 10:
                            numHoldReg += 2;
                            break;
                        case 4:
                        case 8:
                            numHoldReg += 4;
                            break;
                    }
                }
            }
        }

        public bool GetRtValue(int nNum, numInf[] nrst, int sNum, strInf[] srst)
        {
            if (find.Count < 1) return false;
            string index = "";
            numInf numtemp = new numInf();
            for (int i = 0; i < nrst.Length; i++)
            {
                try
                {
                    string[] arry = nrst[i].srcId.Split('_');
                    index = arry[0] + int.Parse(arry[1]).ToString("d4");
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
                catch(Exception ex)
                {
                    Console.WriteLine(ex.ToString());
                }
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
                catch (Exception ex)
                {
                    Console.WriteLine(ex.ToString());
                }

                try
                {
                    byte[] btemp = new byte[2] { receive[5], receive[4] };
                    int bufferlen = BitConverter.ToInt16(btemp, 0) + 6;
                    if (bufferlen == receive.Length)//报文长度正确
                    {
                        TCP tcptemp = new TCP(receive, m_len, m_startAddr, dataType);

                        if ((tcptemp.Responseread != null) && (tcptemp.Responseread.ByteNum != 0))
                        {

                            Console.WriteLine("RX: FC:{0} ", tcptemp.Responseread.FC);
                            try
                            {
                                sw = File.AppendText(logfile);
                                sw.WriteLine(DateTime.Now.ToString() + " RX: FC:{0} ", tcptemp.Responseread.FC);
                                sw.Close();
                            }
                            catch (Exception ex)
                            {
                                Console.WriteLine(ex.ToString());
                            }

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
                                catch (Exception ex)
                                {
                                    Console.WriteLine(ex.ToString());
                                }

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
                            catch (Exception ex)
                            {
                                Console.WriteLine(ex.ToString());
                            }
                        }//end if ((tcptemp.Responseread != null) && (tcptemp.Responseread.ByteNum != 0))
                    }//end if (bufferlen == buffer.Length)
                }// end try
                catch (Exception ex)
                {
                    Console.WriteLine(ex.ToString());
                    //System.Diagnostics.Debug.WriteLine(ex.ToString());
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
                    m_startAddr = tcptemp.Requestread.StartAddr + 1;
                    m_len = tcptemp.Requestread.ReadNum;
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
                            StreamWriter sw1 = new StreamWriter(logfile);
                            string w = "";
                            sw1.Write(w);
                            sw1.Close();
                            break;
                        }
                        Thread.Sleep(200);
                    }
                }
                fs.Close();
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.ToString());
            }

            if (SendList.Count > 0) return;

            if ((startCoilAddr < 999999) && (startCoilAddr > 0))
            {
                //RequestData(FunctionCode.Inputs, 0, 10);
                for (int i = 0; i < (numCoil / 2000 + 1); i++)
                {
                    RequestData(FunctionCode.Coils, startCoilAddr - 1 + i * 2000,
                                (i == numCoil / 2000 ? numCoil % 2000 : 2000));
                }
            }
            if ((startInputAddr < 999999)&&(startInputAddr >0))
            {
                //RequestData(FunctionCode.Inputs, 0, 10);
                for (int i = 0; i < (numInput / 2000 + 1); i++)
                {
                    RequestData(FunctionCode.Inputs, startInputAddr - 1 + i * 2000,
                                (i == numInput / 2000 ? numInput % 2000 : 2000));
                }
            }

            if ((startHoldRegAddr < 999999) && (startHoldRegAddr > 0))
            {
                int outype = 0;
                int pointAddr = startHoldRegAddr-1;
                int callAddr = startHoldRegAddr-1;
                int callNum = 0;

                for (int i = 0; i < numHoldReg; )
                {
                    dataType.TryGetValue("4" + (pointAddr + 1).ToString("d4"), out outype);
                    switch (outype)
                    {
                        case 1:
                        case 5:
                            pointAddr += 1;
                            callNum += 1;
                            i += 1;
                            break;
                        case 2:
                        case 3:
                        case 6:
                        case 7:
                        case 9:
                        case 10:
                            pointAddr += 2;
                            callNum += 2;
                            i += 2;
                            break;
                        case 4:
                        case 8:
                            pointAddr += 4;
                            callNum += 4;
                            i += 4;
                            break;
                    }
                    if (callNum > 120)
                    {
                        RequestData(FunctionCode.HoldReg, callAddr, callNum);
                        callAddr = pointAddr;
                        callNum = 0;
                    }
                    if((i > numHoldReg)||(i == numHoldReg))//最后一次招
                    {
                        RequestData(FunctionCode.HoldReg, callAddr, callNum);
                        callAddr = pointAddr;
                        callNum = 0;
                    }
                }
            }
            if ((startInputRegAddr < 999999) && (startInputRegAddr > 0))
            {
                int outype = 0;
                int pointAddr = startInputRegAddr-1;
                int callAddr = startInputRegAddr-1;
                int callNum = 0;

                for (int i = 0; i < numInputReg; )
                {
                    dataType.TryGetValue("3" + (pointAddr + 1).ToString("d4"), out outype);
                    switch (outype)
                    {
                        case 1:
                        case 5:
                            pointAddr += 1;
                            callNum += 1;
                            i += 1;
                            break;
                        case 2:
                        case 3:
                        case 6:
                        case 7:
                        case 9:
                        case 10:
                            pointAddr += 2;
                            callNum += 2;
                            i += 2;
                            break;
                        case 4:
                        case 8:
                            pointAddr += 4;
                            callNum += 4;
                            i += 4;
                            break;
                    }
                    if (callNum > 120)
                    {
                        RequestData(FunctionCode.InputReg, callAddr, callNum);
                        callAddr = pointAddr;
                        callNum = 0;
                    }
                    if ((i > numInputReg) || (i == numInputReg))//最后一次招
                    {
                        RequestData(FunctionCode.InputReg, callAddr, callNum);
                        callAddr = pointAddr;
                        callNum = 0;
                    }
                }
            }
        }
        /// <summary>
        /// 读取数据
        /// </summary>
        public void RequestData(FunctionCode fc, int startAddr, int len)
        {
            if (len == 0) return;

            TCP temp = new TCP(m_config_Modbus.m_DeviceAddress, fc, startAddr, len);
            SendList.Enqueue(temp);
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
            catch (Exception ex)
            {
                Console.WriteLine(ex.ToString());
            }

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
            Dictionary<string, IntPtr> list = new Dictionary<string, IntPtr>();
            list.Clear();
            DateTime time;
            float value;
            IntPtr intptr;
            while (true)
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
                Thread.Sleep(1000);
            }
        }
    }

}
