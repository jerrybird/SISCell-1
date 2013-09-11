using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Ports;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using System.Text;
using System.Threading;
using ModbusLibrary_NR;
using CSOPCServerLib;

namespace SISCell
{
    //public class I_Modbus<T>  :iProtocol
    //    where T:new()
    public class I_ModbusCOM_NR: iProtocol
    {
        ////private System.ComponentModel.BackgroundWorker backgroundWorker1;
        ////TCP连接
        ////Socket socket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
        //Socket socket;
        ////发送队列
        //Queue<T> SendList = new Queue<T>();
        Queue<object> SendList = new Queue<object>();
        ////等待语句柄。挂起后台线程时阻塞使用
        //EventWaitHandle waitHandel = new EventWaitHandle(false, EventResetMode.AutoReset);
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
        

        //static T tvar =new T();

        int m_startAddr = 0;
        int m_len = 0;
        int m_type = 0;
        bool sendFlag;
        
        /// <summary>
        /// 串口变量
        /// </summary>
        SerialPort port1;
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
        public I_ModbusCOM_NR()
        {
            m_config_Modbus = new CONFG_Modbus_NR("config\\Modbus_NR.ini");
            sendRate =Convert.ToInt32( m_config_Modbus.m_SendRate * 1000);
            callAllRate = sendRate * 4;


            port1 = new SerialPort(m_config_Modbus.m_COM);
            port1.BaudRate = m_config_Modbus.m_BaudRate;//波特率
            port1.Parity = (Parity)m_config_Modbus.m_Parity;//奇偶校验位
            port1.DataBits = m_config_Modbus.m_DataBits;//数据位
            port1.StopBits =(StopBits)m_config_Modbus.m_StopBits;//停止位
            port1.Handshake =( Handshake)m_config_Modbus.m_Handshake;//控制协议
 
            logfile = "log.txt";
            FileStream fs = new FileStream(logfile, FileMode.OpenOrCreate);
            fs.Close();
            try
            {
                sw = File.AppendText(logfile);
                sw.WriteLine(DateTime.Now.ToString() + " 初始化成功！");
                sw.Close();
            }
            catch { }
        }
        ~I_ModbusCOM_NR()
        {
            if (fun != null)
                fun.UnregisterS(m_config_Modbus.m_OPCServerName);
        }
        /// <summary>
        /// “连接”事件触发
        /// </summary>
        public bool Connect()
        {
            if (!Mconnect()) return false;
            return true;

        }
        public bool Mconnect()
        {
            try
            {
                port1.Open();
            }
            catch { }
            if (port1.IsOpen)
            {
                try
                {
                    sw = File.AppendText(logfile);
                    sw.WriteLine(DateTime.Now.ToString() + " the port is opened!");
                    sw.Close();
                }
                catch { }
                Console.WriteLine(" the port is opened!");
                port1.DataReceived += new SerialDataReceivedEventHandler(DataReceived);//DataReceived事件委托
                sendFlag = true;
                //return true;
            }
            else
            {
                try
                {
                    sw = File.AppendText(logfile);
                    sw.WriteLine(DateTime.Now.ToString() + " failure to open the port!");
                    sw.Close();
                }
                catch { }
                Console.WriteLine(" failure to open the port!");
                return false;
            }

            //this.InitLink();

            //设置定时器
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

            return true;

        }

        public void DisConnect()
        {
            port1.DataReceived -= DataReceived;
            port1.Close();
            if (!port1.IsOpen)
            {
                Console.WriteLine("the port is already closed!");
                try
                {
                    sw = File.AppendText(logfile);
                    sw.WriteLine(DateTime.Now.ToString() + " the port is already closed!");
                    sw.Close();
                }
                catch { }
            }
        }

        public bool Connected
        {
            get
            {
                return (port1.IsOpen);
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
        /// <summary>
        /// 对字符串解报
        /// </summary>
        /// ycType= 1; int16;
        /// ycType= 2; int32;
        /// ycType= 3; float32;
        /// ycType= 4; double64;
        /// ycType= 5; Uint16;
        /// ycType= 6; int32_Inver;
        /// ycType= 7; float32_Inver;
        /// ycType= 8; double64_Inver;
        public static void UnPackString(string pm, string str,int type)
        {
            string stemp = str.ToLower().Replace(" ", "");
            byte[] bytetemp = new byte[stemp.Length / 2];
            for (int i = 0; i < stemp.Length / 2; i++)
            {
                string ss = stemp.Substring(2 * i, 2);
                bytetemp[i] = Convert.ToByte(ss, 16);
            }
            try
            {
                if ((pm == "TX"))
                {
                    //T temp = new T(bytetemp);
                    //if (temp.GetControl().PRM)
                    //{
                    //    Console.WriteLine("TX: FCB:{0} FCV:{1} FUNCTION:{2}",
                    //                        temp.GetControl().FCB, temp.GetControl().FCV, temp.GetControl().function_1);
                        
                    //}
                    //else
                    //{
                    //    Console.WriteLine("RX: ACD:{0} DFC:{1} FUNCTION:{2}",
                    //                        temp.GetControl().ACD, temp.GetControl().DFC, temp.GetControl().function_0);
                      
                    //}
                }
                if ((pm == "RX"))
                {
                    //if (tvar is RTU)
                    //if (m_config_Modbus.m_ModbusType == "RTU" )
                    {
                        RTU vtemp = new RTU(bytetemp, 0,type);
                        if ((vtemp.Responseread != null) && (vtemp.Responseread.ByteNum != 0))
                        {

                            Console.WriteLine("RX: FC:{0} ", vtemp.Responseread.FC);
                            Console.WriteLine("RX: {0}", vtemp.ToString());

                            var datas = vtemp.GetData();
                            foreach (var data in datas)
                            {
                                //if (data.Addr == 0) continue;
                                Console.WriteLine("RX: " + "addr:" + data.Addr.ToString() + " " +
                                                    "data:" + data.Data.ToString());
                            }
                            Console.WriteLine("\r\n");
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine(ex.ToString());
            }
        }
        /// <summary>
        /// 对二进制串解报
        /// </summary>
        public void UnpackReceive(byte[] bytetemp, int startAddr, int len)
        {
            try
            {
                //if (tvar is RTU)
                if (m_config_Modbus.m_ModbusType == "RTU")
                {
                    if (bytetemp[2] + 5 == bytetemp.Length)
                    {
                        RTU vtemp = new RTU(bytetemp, len, startAddr,m_type);

                        if ((vtemp.Responseread != null) && (vtemp.Responseread.ByteNum != 0))
                        {

                            Console.WriteLine("RX: FC:{0} ", vtemp.Responseread.FC);
                            try
                            {
                                sw = File.AppendText(logfile);
                                sw.WriteLine(DateTime.Now.ToString() + " RX: FC:{0} ", vtemp.Responseread.FC);
                                sw.Close();
                            }
                            catch { }

                            var datas = vtemp.GetData();
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
                            Console.WriteLine("\n");
                            try
                            {
                                sw = File.AppendText(logfile);
                                sw.WriteLine("\r\n");
                                sw.Close();
                            }
                            catch { }
                        }//end if ((vtemp.Responseread != null) && (vtemp.Responseread.ByteNum != 0))
                    }//end if (bytetemp[2] + 5 == bytetemp.Length)
                }//end if (tvar is RTU)

                //if (tvar is ASCII)
                if (m_config_Modbus.m_ModbusType == "ASCII")
                {
                    ASCII vtemp = new ASCII(bytetemp, len, startAddr, m_type);
                    if ((vtemp.AscRtu.Responseread != null) && (vtemp.AscRtu.Responseread.ByteNum != 0))
                    {

                        Console.WriteLine("RX: FC:{0} ", vtemp.AscRtu.Responseread.FC);
                        try
                        {
                            sw = File.AppendText(logfile);
                            sw.WriteLine(DateTime.Now.ToString() + " RX: FC:{0} ", vtemp.AscRtu.Responseread.FC);
                            sw.Close();
                        }
                        catch { }

                        var datas = vtemp.AscRtu.GetData();
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
                        Console.WriteLine("\n"); 
                        try
                        {
                            sw = File.AppendText(logfile);
                            sw.WriteLine("\r\n");
                            sw.Close();
                        }
                        catch { }
                    }
                    
                }//if (tvar is ASCII)
            }// end try
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine(ex.ToString());
                //异常处理
                throw ex;
            }
        }//end UnpackReceive()

      private void DataReceived(object sender, SerialDataReceivedEventArgs e)
       {
           //if (sendFlag == true) return;
           try
           {
               Thread.Sleep(200);
               byte[] btemp = new byte[port1.BytesToRead];
               port1.Read(btemp, 0, port1.BytesToRead);
               if (BitConverter.ToString(btemp, 0) == "") return;
               check = DateTime.Now;

               Console.WriteLine("RX: " + BitConverter.ToString(btemp, 0).Replace("-", " "));
               try
               {
                   sw = File.AppendText(logfile);
                   sw.WriteLine(DateTime.Now.ToString() + " RX: " + BitConverter.ToString(btemp, 0).Replace("-", " "));
                   sw.Close();
               }
               catch { }

               UnpackReceive(btemp,m_startAddr,m_len);
               sendFlag = true;
           }
           catch (Exception ex)
           {
               Console.WriteLine(ex.Message.ToString());
           }
       }
   
        /// <summary>
        /// 定时发送的定时器
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        /// ycType= 1; int16;
        /// ycType= 2; int32;
        /// ycType= 3; float32;
        /// ycType= 4; double64;
        /// ycType= 5; Uint16;
        /// ycType= 6; int32_Inver;
        /// ycType= 7; float32_Inver;
        /// ycType= 8; double64_Inver;
        private void tm_Elapsed(object sender, System.Timers.ElapsedEventArgs e)
        {
            try
            {
                
                //心跳检测
                TimeSpan currentSpan = DateTime.Now.Subtract(check);
                if (currentSpan.Seconds > m_config_Modbus.m_SendTaskInteval)
                {
                    sendFlag = true;
                }


                if (sendFlag == false) return;
                if (SendList.Count > 0)
                {
                    object temp = SendList.Dequeue();
                    //if (tvar is RTU)
                    if (m_config_Modbus.m_ModbusType == "RTU")
                    {
                        RTU rtuobj = (RTU)temp;
                        m_startAddr = rtuobj.Requestread.StartAddr;
                        m_len = rtuobj.Requestread.ReadNum;
                        switch (rtuobj.Requestread.FC)
                        {
                            case FunctionCode.InputReg:
                                m_type = m_config_Modbus.m_inputregType;
                                break;
                            case FunctionCode.HoldReg:
                                m_type = m_config_Modbus.m_holdregType;
                                break;
                        }
                        byte[] SendBuffer = rtuobj.ToArray();
                        check = DateTime.Now;
                        port1.Write(SendBuffer, 0, SendBuffer.Length);
                        sendFlag = false;
                        Console.WriteLine("TX: " + BitConverter.ToString(SendBuffer, 0).Replace("-", " "));
                        try
                        {
                            sw = File.AppendText(logfile);
                            sw.WriteLine(DateTime.Now.ToString() + " TX: " + BitConverter.ToString(SendBuffer, 0).Replace("-", " "));
                            sw.Close();
                        }
                        catch { }
                    }
                    //if (tvar is ASCII)
                    if (m_config_Modbus.m_ModbusType == "ASCII")
                    {
                        ASCII ascobj = (ASCII)temp;
                        m_startAddr = ascobj.AscRtu.Requestread.StartAddr;
                        m_len = ascobj.AscRtu.Requestread.ReadNum;
                        switch (ascobj.AscRtu.Requestread.FC)
                        {
                            case FunctionCode.InputReg:
                                m_type = m_config_Modbus.m_inputregType;
                                break;
                            case FunctionCode.HoldReg:
                                m_type = m_config_Modbus.m_holdregType;
                                break;
                        }
                       
                        byte[] SendBuffer = ascobj.ToArray();
                        check = DateTime.Now;
                        port1.Write(SendBuffer, 0, SendBuffer.Length);
                        sendFlag = false;
                        Console.WriteLine("TX: " + new System.Text.ASCIIEncoding().GetString(SendBuffer));
                        Console.WriteLine("TX: " + BitConverter.ToString(SendBuffer, 0).Replace("-", " "));
                        try
                        {
                            sw = File.AppendText(logfile);
                            sw.WriteLine("TX: " + new System.Text.ASCIIEncoding().GetString(SendBuffer));
                            sw.WriteLine(DateTime.Now.ToString() + " TX: " + BitConverter.ToString(SendBuffer, 0).Replace("-", " "));
                            sw.Close();
                        }
                        catch { }
                    }
                }
            }
            catch (Exception er)
            {
                Console.WriteLine(er.ToString());
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
            catch { }
            if (SendList.Count > 0) return;
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
                                (i == m_config_Modbus.m_holdregLen / lentemp ? m_config_Modbus.m_holdregLen % lentemp : lentemp));
                }
            }
            if ((m_config_Modbus.m_inputregLen > 0) && (m_config_Modbus.m_inputregStartAddr > 0))
            {
                //RequestData(FunctionCode.InputReg, 0, 10);
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
                for (int i = 0; i < (m_config_Modbus.m_inputregLen / lentemp + 1); i++)
                {
                    RequestData(FunctionCode.InputReg, m_config_Modbus.m_inputregStartAddr - 1 + i * lentemp,
                                (i == m_config_Modbus.m_inputregLen / lentemp ? m_config_Modbus.m_inputregLen % lentemp : lentemp));
                }
            }
        }

        /// <summary>
        /// 读取数据
        /// </summary>
        public void RequestData(FunctionCode fc, int startAddr, int len)
        {
            if (len == 0) return;
            object obj;
            //if (tvar is RTU)
            if (m_config_Modbus.m_ModbusType == "RTU")
            {
                RTU temp = new RTU(m_config_Modbus.m_DeviceAddress, fc, startAddr, len);
                obj = temp;
                SendList.Enqueue(obj);
            }
            //if (tvar is ASCII)
            if (m_config_Modbus.m_ModbusType == "ASCII")
            {
                ASCII temp = new ASCII(m_config_Modbus.m_DeviceAddress, fc, startAddr, len);
                obj = temp;
                SendList.Enqueue(obj);
            }
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

    }// end class I_Modbus





    public class CONFG_Modbus_NR
    {
        private INI cfg;
        private int yx_StartAddress = 1;
        private int yx_EndAddress = 4096;
        private int yc_StartAddress = 16385;
        private int yc_EndAddress = 20480;
        private int ym_StartAddress = 25601;
        private int ym_EndAddress = 26112;
        private int coils_startaddr = 0;
        private int coils_len = 10;
        private int inputs_startaddr = 0;
        private int inputs_len = 10;
        private int holdreg_startaddr = 0;
        private int holdreg_len = 10;
        private int inputreg_startaddr = 0;
        private int inputreg_len = 10;
        private int holdreg_type = 1;
        private int inputreg_type = 1;

        private byte DeviceAddress = 1;
        private float SendRate = 1;
        private float SendTaskInteval = 5;
        private int yctype = 3;
        private int ymtype = 4;
        private string ModbusType = "RTU";
        private string OPC_ServerName = "SAC.OPC";

        private string COM = "";
        private int BaudRate = 9600;
        private int DataBits = 8;
        private int StopBits = 1;
        private int Parity = 0;
        private int Handshake = 0;

        private string IPAddress = "127.0.0.1";
        private int Port = 502;

        private string logfile = "log.txt";
        StreamWriter sw;
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
        public int m_Port
        {
            get { return Port; }
            set { Port = value; }
        }

        public string m_COM
        {
            get { return COM; }
            set { COM = value; }
        }
        public int m_BaudRate
        {
            get { return BaudRate; }
            set { BaudRate = value; }
        }
        public int m_DataBits
        {
            get { return DataBits; }
            set { DataBits = value; }
        }
        public int m_StopBits
        {
            get { return StopBits; }
            set { StopBits = value; }
        }
        public int m_Parity
        {
            get { return Parity; }
            set { Parity = value; }
        }
        public int m_Handshake
        {
            get { return Handshake; }
            set { Handshake = value; }
        }
        public int m_YcType
        {
            get { return yctype; }
            set { yctype = value; }
        }
        public int m_YmType
        {
            get { return ymtype; }
            set { ymtype = value; }
        }
        public string m_ModbusType
        {
            get { return ModbusType; }
            set { ModbusType = value; }
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
        public int m_coilsStartAddr
        {
            get { return coils_startaddr; }
            set { coils_startaddr = value; }
        }
        public int m_coilsLen
        {
            get { return coils_len; }
            set { coils_len = value; }
        }
        public int m_inputsStartAddr
        {
            get { return inputs_startaddr; }
            set { inputs_startaddr = value; }
        }
        public int m_inputsLen
        {
            get { return inputs_len; }
            set { inputs_len = value; }
        }
        public int m_holdregStartAddr
        {
            get { return holdreg_startaddr; }
            set { holdreg_startaddr = value; }
        }
        public int m_holdregLen
        {
            get { return holdreg_len; }
            set { holdreg_len = value; }
        }
        public int m_inputregStartAddr
        {
            get { return inputreg_startaddr; }
            set { inputreg_startaddr = value; }
        }
        public int m_inputregLen
        {
            get { return inputreg_len; }
            set { inputreg_len = value; }
        }
        public int m_inputregType
        {
            get { return inputreg_type; }
            set { inputreg_type = value; }
        }
        public int m_holdregType
        {
            get { return holdreg_type; }
            set { holdreg_type = value; }
        }
        public byte m_DeviceAddress
        {
            get { return DeviceAddress; }
            set { DeviceAddress = value; }
        }
        public float m_SendRate
        {
            get { return SendRate; }
            set { SendRate = value; }
        }
        public float m_SendTaskInteval
        {
            get { return SendTaskInteval; }
            set { SendTaskInteval = value; }
        }
        public CONFG_Modbus_NR(string configFile)
        {
            SetConfig_Modbus(configFile);
        }

        public void SetConfig_Modbus(string configFile)
        {
            FileStream fs = new FileStream(configFile, FileMode.OpenOrCreate);
            fs.Close();

            //try
            {
                cfg = new INI(AppDomain.CurrentDomain.SetupInformation.ApplicationBase + configFile);
                m_IPAddress = cfg.GetVal("TCP/IP", "IPAddress");
                m_Port = Convert.ToInt32(cfg.GetVal("TCP/IP", "Port"));

                m_COM = cfg.GetVal("SerialPort", "COM");
                m_BaudRate = Convert.ToInt32(cfg.GetVal("SerialPort", "BaudRate"));
                m_DataBits = Convert.ToInt32(cfg.GetVal("SerialPort", "DataBits"));
                m_StopBits = Convert.ToInt32(cfg.GetVal("SerialPort", "StopBits"));
                m_Parity = Convert.ToInt32(cfg.GetVal("SerialPort", "Parity"));
                m_Handshake = Convert.ToInt32(cfg.GetVal("SerialPort", "Handshake"));
                m_ModbusType = cfg.GetVal("SerialPort", "ModbusType");

                m_DeviceAddress = Convert.ToByte(cfg.GetVal("Advanced", "DeviceAddress"));
                m_SendRate = Convert.ToSingle(cfg.GetVal("Advanced", "SendRate"));
                m_SendTaskInteval = Convert.ToSingle(cfg.GetVal("Advanced", "SendTaskInteval"));
                m_OPCServerName = cfg.GetVal("Advanced", "OPCServerName");
                //m_YcType = Convert.ToInt32(cfg.GetVal("Advanced", "ycType"));
                //m_YmType = Convert.ToInt32(cfg.GetVal("Advanced", "ymType"));

                m_coilsStartAddr = Convert.ToInt32(cfg.GetVal("DATA", "Coils_StartAddr"));
                m_coilsLen = Convert.ToInt32(cfg.GetVal("DATA", "Coils_Len"));
                m_inputsStartAddr = Convert.ToInt32(cfg.GetVal("DATA", "Inputs_StartAddr")) - 10000;
                m_inputsLen = Convert.ToInt32(cfg.GetVal("DATA", "Inputs_Len"));
                m_holdregStartAddr = Convert.ToInt32(cfg.GetVal("DATA", "HoldReg_StartAddr")) - 40000;
                m_holdregLen = Convert.ToInt32(cfg.GetVal("DATA", "HoldReg_Len"));
                m_inputregStartAddr = Convert.ToInt32(cfg.GetVal("DATA", "InputReg_StartAddr")) - 30000;
                m_inputregLen = Convert.ToInt32(cfg.GetVal("DATA", "InputReg_Len"));
                m_holdregType = Convert.ToInt32(cfg.GetVal("DATA", "HoldReg_Type"));
                m_inputregType = Convert.ToInt32(cfg.GetVal("DATA", "InputReg_Type"));

                //m_yxStartAddress = Convert.ToInt32(cfg.GetVal("DATA", "yx_StartAddress"));
                //m_yxEndAddress = Convert.ToInt32(cfg.GetVal("DATA", "yx_EndAddress"));
                //m_ycStartAddress = Convert.ToInt32(cfg.GetVal("DATA", "yc_StartAddress"));
                //m_ycEndAddress = Convert.ToInt32(cfg.GetVal("DATA", "yc_EndAddress"));
                //m_ymStartAddress = Convert.ToInt32(cfg.GetVal("DATA", "ym_StartAddress"));
                //m_ymEndAddress = Convert.ToInt32(cfg.GetVal("DATA", "ym_EndAddress"));
            }
            //catch
            //{
            //    StreamWriter sw1 = new StreamWriter(configFile);
            //    string w = "";
            //    sw1.Write(w);
            //    sw1.Close();
            //    StreamWriter sw = File.AppendText(configFile);
            //    sw.WriteLine("[SerialPort]");
            //    sw.WriteLine("COM=COM1");
            //    sw.WriteLine("BaudRate=9600");
            //    sw.WriteLine("Parity=0");
            //    sw.WriteLine("DataBits=8");
            //    sw.WriteLine("StopBits=1");
            //    sw.WriteLine("Handshake=0");
            //    sw.WriteLine("ModbusType=RTU");

            //    sw.WriteLine("[TCP/IP]");
            //    sw.WriteLine("IPAddress=127.0.0.1");
            //    sw.WriteLine("Port=502");

            //    sw.WriteLine("[Advanced]");
            //    sw.WriteLine("DeviceAddress=1");
            //    sw.WriteLine("SendRate=1");
            //    sw.WriteLine("SendTaskInteval=5");
            //    sw.WriteLine("OPCServerName=SAC.OPC.Modbus");

            //    sw.WriteLine("[DATA]");
            //    sw.WriteLine("Coils_StartAddr=0");
            //    sw.WriteLine("Coils_Len=10");
            //    sw.WriteLine("Inputs_StartAddr=0");
            //    sw.WriteLine("Inputs_Len=10");
            //    sw.WriteLine("HoldReg_StartAddr=0");
            //    sw.WriteLine("HoldReg_Len=10");
            //    sw.WriteLine("HoldReg_Type=1");
            //    sw.WriteLine("InputReg_StartAddr=0");
            //    sw.WriteLine("InputReg_Len=10");
            //    sw.WriteLine("InputReg_Type=1");

            //    sw.WriteLine("yx_StartAddress=1");
            //    sw.WriteLine("yx_EndAddress=10");
            //    sw.WriteLine("yc_StartAddress=40001");
            //    sw.WriteLine("yc_EndAddress=40012");
            //    sw.WriteLine("ym_StartAddress=30001");
            //    sw.WriteLine("ym_EndAddress=30012");
            //    sw.Close();
            //}
        }
    }
}