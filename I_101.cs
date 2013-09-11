using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Ports;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using SerialPort101Library;
using CSOPCServerLib;

namespace SISCell
{
    public class I_101 : iProtocol
    {
        ////private System.ComponentModel.BackgroundWorker backgroundWorker1;
        ////TCP连接
        ////Socket socket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
        //Socket socket;
        ////发送队列
        Queue<Frame_101> SendList = new Queue<Frame_101>();
        ////等待语句柄。挂起后台线程时阻塞使用
        //EventWaitHandle waitHandel = new EventWaitHandle(false, EventResetMode.AutoReset);
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
        //定时器_总招数据
        System.Timers.Timer tmCallAll;
        List<byte> recevBuffer = new List<byte>();
        
        /// <summary>
        /// 串口变量
        /// </summary>
        SerialPort port1;
        //建立数据索引，查找已初始化的数据更新
        Dictionary<int, numInf> find = new Dictionary<int, numInf>();
        //配置信息
        CONFG_101 m_config_101;
        OPCServerFUN fun = null;

        bool prm = true;
        bool fcb = false;
        bool fcv = false;

        //输出文本路径
        StreamWriter sw;
        string  logfile;
        /// <summary>
        /// 构造函数
        /// </summary>
        public I_101()
        {
            m_config_101 = new CONFG_101("config\\101.ini");
            sendRate =Convert.ToInt32( m_config_101.m_SendRate * 1000);
            ymRate = Convert.ToInt32( m_config_101.m_YmRate * 1000 );
            callAllRate = Convert.ToInt32( (m_config_101.m_CallAllRate - 5 ) * 1000 );


            port1 = new SerialPort(m_config_101.m_COM);
            port1.BaudRate = m_config_101.m_BaudRate;//波特率
            port1.Parity = (Parity)m_config_101.m_Parity;//奇偶校验位
            port1.DataBits = m_config_101.m_DataBits;//数据位
            port1.StopBits =(StopBits)m_config_101.m_StopBits;//停止位
            port1.Handshake =( Handshake)m_config_101.m_Handshake;//控制协议

            logfile = "log.txt";
            FileStream fs = new FileStream(logfile, FileMode.OpenOrCreate);
            fs.Close();

            sw = File.AppendText(logfile);
            sw.WriteLine(DateTime.Now.ToString()+" 初始化成功！");
            sw.Close();
        }
        ~I_101()
        {
            if (fun != null)
                fun.UnregisterS(m_config_101.m_OPCServerName);
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

            this.InitLink();

            //设置定时器
            tmSend = new System.Timers.Timer();
            tmSend.AutoReset = true;
            tmSend.Interval = sendRate;
            tmSend.Elapsed += new System.Timers.ElapsedEventHandler(tm_Elapsed);
            tmSend.Start();

            tmYM = new System.Timers.Timer();
            tmYM.AutoReset = true;
            tmYM.Interval = ymRate;
            tmYM.Elapsed += new System.Timers.ElapsedEventHandler(ym_Elapsed);
            tmYM.Start();

            tmCallAll = new System.Timers.Timer();
            tmCallAll.AutoReset = true;
            tmCallAll.Interval = callAllRate;
            tmCallAll.Elapsed += new System.Timers.ElapsedEventHandler(callAll_Elapsed);
            tmCallAll.Start();

            return true;

        }

        public void InitLink()
        {
            prm = true;
            fcb = false;
            fcv = false;
            Control contemp = new Control();
            port1.DataReceived -= DataReceived;

            //请求链路状态
            Control con = new Control(prm, fcb, fcv, Control.FUNCTION_1.RequestLinkState, Control.FUNCTION_0.Undef);
            Frame_101 flm = new Frame_101( 0x10,con, m_config_101.m_LinkAddress);
            while (true)
            {
                port1.Write(flm.ToArray(), 0, flm.ToArray().Length);
                try
                {
                    sw = File.AppendText(logfile);
                    sw.WriteLine(DateTime.Now.ToString() + " TX: " + flm.ToString());
                    sw.Close();
                }
                catch { }
                Console.WriteLine("TX: " + flm.ToString());
                Thread.Sleep(1000);
                byte[] btemp = new byte[port1.BytesToRead];
                port1.Read(btemp, 0, port1.BytesToRead);
                if (BitConverter.ToString(btemp, 0) == "") continue;
                try
                {
                    sw = File.AppendText(logfile);
                    sw.WriteLine(DateTime.Now.ToString() + " RX: " + BitConverter.ToString(btemp, 0).Replace("-", " "));
                    sw.Close();
                }
                catch { }
                Console.WriteLine("RX: " + BitConverter.ToString(btemp, 0).Replace("-", " "));

                if (UnpackReceive(btemp, contemp) == 2)
                {
                    if ((contemp.PRM == false) && (contemp.function_0 == Control.FUNCTION_0.RespondLinkState))
                        break;
                }
            }
            //复位远方链路
            con = new Control(prm, fcb, fcv, Control.FUNCTION_1.ResetRemoteLink, Control.FUNCTION_0.Undef);
            flm = new Frame_101(0x10, con, m_config_101.m_LinkAddress);
            while (true)
            {
                port1.Write(flm.ToArray(), 0, flm.ToArray().Length);
                try
                {
                    sw = File.AppendText(logfile);
                    sw.WriteLine(DateTime.Now.ToString() + " TX: " + flm.ToString());
                    sw.Close();
                }
                catch { }
                Console.WriteLine("TX: " + flm.ToString());
                Thread.Sleep(1000);
                byte[] btemp = new byte[port1.BytesToRead];
                port1.Read(btemp, 0, port1.BytesToRead);
                if (BitConverter.ToString(btemp, 0) == "") continue;
                Console.WriteLine("RX: " + BitConverter.ToString(btemp, 0).Replace("-", " "));
                try
                {
                    sw = File.AppendText(logfile);
                    sw.WriteLine(DateTime.Now.ToString() + " RX: " + BitConverter.ToString(btemp, 0).Replace("-", " "));
                    sw.Close();
                }
                catch { }

                if (UnpackReceive(btemp, contemp) == 2)
                {
                    if ((contemp.PRM == false) && (contemp.function_0 == Control.FUNCTION_0.AckLink))
                    {
                        fcb = true; fcv = true;
                        break;
                    }
                }
            }

            //是否有一级数据
            if (contemp.ACD)
            {//招唤一级数据
                con = new Control(prm, fcb, fcv, Control.FUNCTION_1.RequestPrimaryData, Control.FUNCTION_0.Undef);
                flm = new Frame_101(0x10, con, m_config_101.m_LinkAddress);
                port1.Write(flm.ToArray(), 0, flm.ToArray().Length);
                Console.WriteLine("TX: " + flm.ToString());
                try
                {
                    sw = File.AppendText(logfile);
                    sw.WriteLine(DateTime.Now.ToString() + " TX: " + flm.ToString());
                    sw.Close();
                }
                catch { }
            }
            else
            {
                //招唤二级数据
                //con = new Control(prm, fcb, fcv, Control.FUNCTION_1.RequestSeconData, Control.FUNCTION_0.Undef);
                //flm = new Frame_101(0x10, con, m_config_101.m_LinkAddress);
                //port1.Write(flm.ToArray(), 0, flm.ToArray().Length);

                //总招
                con = new Control(prm, fcb, fcv, Control.FUNCTION_1.UserDataConfirm, Control.FUNCTION_0.Undef);
                ASDUClass calBuffer = new ASDUClass();
                calBuffer.Pack(ASDUClass.TransRes.Active, ASDUClass.FunType.CalAll, m_config_101.m_PublicAddress);
                flm = new Frame_101(0x68, con, m_config_101.m_LinkAddress, calBuffer);
                port1.Write(flm.ToArray(), 0, flm.ToArray().Length);
                Console.WriteLine("TX: " + flm.ToString());
                try
                {
                    sw = File.AppendText(logfile);
                    sw.WriteLine(DateTime.Now.ToString() + " TX: " + flm.ToString());
                    sw.Close();
                }
                catch { }
            }
            port1.DataReceived += new SerialDataReceivedEventHandler(DataReceived);//DataReceived事件委托
        }//end InitLink()


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
            string stemp = str.ToLower().Replace(" ", "");
            byte[] bytetemp = new byte[stemp.Length / 2];
            for (int i = 0; i < stemp.Length / 2; i++)
            {
                string ss = stemp.Substring(2 * i, 2);

                bytetemp[i] = Convert.ToByte(ss, 16);
            }
            try
            {
                if ((bytetemp.Length == 5) && (bytetemp[0] == 0x10) && (bytetemp[4] == 0x16))
                {
                    Frame_101 temp = new Frame_101(bytetemp);
                    if (temp.GetControl().PRM)
                    {
                        Console.WriteLine("TX: FCB:{0} FCV:{1} FUNCTION:{2}",
                                            temp.GetControl().FCB, temp.GetControl().FCV, temp.GetControl().function_1);
                        
                    }
                    else
                    {
                        Console.WriteLine("RX: ACD:{0} DFC:{1} FUNCTION:{2}",
                                            temp.GetControl().ACD, temp.GetControl().DFC, temp.GetControl().function_0);
                      
                    }
                }
                if (bytetemp.Length > 5)
                {
                    Frame_101 vtemp = new Frame_101(bytetemp);
                    if ((vtemp != null) && (vtemp.GetControl().PRM == false))
                    {

                        Console.WriteLine("RX: ACD:{0} DFC:{1} FUNCTION:{2} Time:{3} ASDUType:{4} Res:{5}",
                                            vtemp.GetControl().ACD, vtemp.GetControl().DFC, vtemp.GetControl().function_0, DateTime.Now, vtemp.GetAsduType(), vtemp.Res);
                        Console.WriteLine("RX: {0}", vtemp.ToString());

                        var datas = vtemp.GetData();
                        foreach (var data in datas)
                        {
                            //if (data.Addr == 0) continue;
                            Console.WriteLine("RX: " + "addr:" + data.Addr.ToString() + " " +
                                                "data:" + data.Data.ToString() + " " +
                                                "time:" + data.Time.ToString());
                        }
                        Console.WriteLine("\r\n");
                    }
                    if ((vtemp != null) && (vtemp.GetControl().PRM == true))
                    {
                        Console.WriteLine("TX: FCB:{0} FCV:{1} FUNCTION:{2} Time:{3} ASDUType:{4} Res:{5}",
                                          vtemp.GetControl().FCB, vtemp.GetControl().FCV, vtemp.GetControl().function_1, DateTime.Now, vtemp.GetAsduType(), vtemp.Res);
                        Console.WriteLine("TX: {0}\n\r", vtemp.ToString());
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine(ex.ToString());
            }
        }

        private void DataReceived(object sender, SerialDataReceivedEventArgs e)
        {

            try
            {
                //Thread.Sleep(500);
                //byte[] btemp = new byte[port1.BytesToRead];
                //port1.Read(btemp, 0, port1.BytesToRead);
                //if (BitConverter.ToString(btemp, 0) == "") return;
                //check = DateTime.Now;

                int n = port1.BytesToRead;
                byte[] buf = new byte[n];
                port1.Read(buf, 0, n);
                recevBuffer.AddRange(buf);
                if (recevBuffer[0] == (byte)0x68)
                {
                    while (true)
                    {
                        if (recevBuffer[1] + 6 == recevBuffer.Count)
                        {
                            break;
                        }
                        else if (recevBuffer[1] + 6 > recevBuffer.Count)
                        {
                            Thread.Sleep(200);
                            int nn = port1.BytesToRead;
                            byte[] buff = new byte[nn];
                            port1.Read(buff, 0, nn);
                            recevBuffer.AddRange(buff);
                            continue;
                        }
                        else { recevBuffer.Clear(); return; }
                    }
                }
                byte[] btemp = new byte[recevBuffer.Count];
                recevBuffer.CopyTo(0,btemp,0,recevBuffer.Count);
                recevBuffer.Clear();
                check = DateTime.Now;


                Console.WriteLine("RX: " + BitConverter.ToString(btemp, 0).Replace("-", " "));
                try
                {
                    sw = File.AppendText(logfile);
                    sw.WriteLine(DateTime.Now.ToString() + " RX: " + BitConverter.ToString(btemp, 0).Replace("-", " "));
                    sw.Close();
                }
                catch (Exception ex)
                {
                    Console.WriteLine(ex.Message.ToString());
                }

                Control contemp = new Control();
                ASDUClass.FunType ft = ASDUClass.FunType.Uformat;
                ASDUClass.TransRes tr = ASDUClass.TransRes.UnDef;
                int itemp = UnpackReceive(btemp, contemp, ref ft, ref tr);
                switch (itemp)
                {

                    case 2://收到固定长度帧  
                        if (contemp.function_0 == Control.FUNCTION_0.NAckLink)
                        {//二级数据，不改变FCB
                            RequestSeconData(false);
                            break;
                        }
                        if (contemp.function_0 == Control.FUNCTION_0.NAckUserData)
                        {
                            //发总招，改变FCB
                            //RequestAllData(true);

                            //二级数据，改变FCB
                            RequestSeconData(true);
                            break;
                        }

                        if (contemp.ACD)
                        {//招唤一级数据
                            RequestPrimaryData(true);
                        }
                        else
                        {//招唤二级数据
                            RequestSeconData(true);
                        }
                        break;

                    case 3://收到可变长度帧
                        //if (((ft == ASDUClass.FunType.CalAll) && (tr == ASDUClass.TransRes.ActiveConfirm))//总招的肯定认可
                        //    || tr == ASDUClass.TransRes.ResAll //响应总招
                        //    || ((ft == ASDUClass.FunType.CalEnergyPulse) && (tr == ASDUClass.TransRes.ActiveConfirm)))//电能脉冲招唤的肯定认可
                        //{
                        //    RequestSeconData(true);
                        //    break;
                        //}
                        //是否有一级数据
                        if (contemp.ACD)
                        {//招唤一级数据
                            RequestPrimaryData(true);
                        }
                        else
                        {//招唤二级数据
                            RequestSeconData(true);
                        }
                        break;

                    case 1: //收到 E5
                    case 4: //收到错误帧
                    default:
                        //招唤二级数据
                        RequestSeconData(true);
                        break;
                }
                btemp = null;
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message.ToString());
            }
        }
        /// <summary>
        /// 招唤一级数据
        /// </summary>
        /// <param name="varfcb" >是否改变fcb</param>
        public void RequestPrimaryData(bool varfcb)
        {
            Control con;
            Frame_101 flm;
            if (varfcb == false)
                con = new Control(prm, fcb, fcv, Control.FUNCTION_1.RequestPrimaryData, Control.FUNCTION_0.Undef);
            else
            {
                fcb = !fcb;
                con = new Control(prm, fcb, fcv, Control.FUNCTION_1.RequestPrimaryData, Control.FUNCTION_0.Undef);
            }
            flm = new Frame_101(0x10,con, m_config_101.m_LinkAddress);
           // port1.Write(flm.ToArray(), 0, flm.ToArray().Length);
            SendList.Enqueue(flm);
        }
        /// <summary>
        /// 招唤二级数据
        /// </summary>
        /// <param name="varfcb" >是否改变fcb</param>
        public void RequestSeconData(bool varfcb)
        {
            Control con;
            Frame_101 flm;
            if (varfcb == false)
                con = new Control(prm, fcb, fcv, Control.FUNCTION_1.RequestSeconData, Control.FUNCTION_0.Undef);
            else
            {
                fcb = !fcb;
                con = new Control(prm, fcb, fcv, Control.FUNCTION_1.RequestSeconData, Control.FUNCTION_0.Undef);
            }
            flm = new Frame_101(0x10, con, m_config_101.m_LinkAddress);
            //port1.Write(flm.ToArray(), 0, flm.ToArray().Length);
            SendList.Enqueue(flm);
        }
        /// <summary>
        /// 总招
        /// </summary>
        /// <param name="varfcb" >是否改变fcb</param>
        public void RequestAllData(bool varfcb)
        {
            Control con;
            Frame_101 vlm;
            if (varfcb == false)
                con = new Control(prm, fcb, fcv, Control.FUNCTION_1.UserDataConfirm, Control.FUNCTION_0.Undef);
            else
            {
                fcb = !fcb;
                con = new Control(prm, fcb, fcv, Control.FUNCTION_1.UserDataConfirm, Control.FUNCTION_0.Undef);
            }
            ASDUClass calBuffer = new ASDUClass();
            calBuffer.Pack(ASDUClass.TransRes.Active, ASDUClass.FunType.CalAll, m_config_101.m_PublicAddress);
            vlm = new Frame_101(0x68, con, m_config_101.m_LinkAddress, calBuffer);
            //port1.Write(vlm.ToArray(), 0, vlm.ToArray().Length);
            SendList.Enqueue(vlm);
        }
        /// <summary>
        /// 对二进制串解报
        /// </summary>
        /// <param name="bytetemp"></param>
        /// <param name="con"></param>
        public int UnpackReceive(byte[] bytetemp, Control con)
        {
            try
            {
                if ((bytetemp.Length == 1) && (bytetemp[0] == 0xe5))
                {
                    //收到 E5
                    return 1;
                }
                if ((bytetemp.Length == 5) && (bytetemp[0] == 0x10) && (bytetemp[4] == 0x16))
                {
                    Frame_101 temp = new Frame_101(bytetemp);
                    if (temp.GetControl().PRM == false)
                    {
                        con.Copy(con, temp.GetControl());
                        Console.WriteLine("RX: ACD:{0} DFC:{1} FUNCTION:{2}\r\n",
                                             temp.GetControl().ACD, temp.GetControl().DFC, temp.GetControl().function_0);
                        try
                        {
                            sw = File.AppendText(logfile);
                            sw.WriteLine(DateTime.Now.ToString() + " RX: ACD:{0} DFC:{1} FUNCTION:{2}\r\n",
                                                 temp.GetControl().ACD, temp.GetControl().DFC, temp.GetControl().function_0);
                            sw.Close();
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine(ex.Message.ToString());
                        }
                        //收到固定长度帧
                        return 2;
                    }
                }
                if (bytetemp.Length > 5)
                {
                    Frame_101 vtemp = new Frame_101(bytetemp);
                    if ((vtemp != null) && (vtemp.GetControl().PRM == false))
                    {

                        Console.WriteLine("RX: ACD:{0} DFC:{1} FUNCTION:{2} Time:{3} ASDUType:{4} Res:{5}",
                                            vtemp.GetControl().ACD, vtemp.GetControl().DFC, vtemp.GetControl().function_0, DateTime.Now, vtemp.GetAsduType(), vtemp.Res);
                        //Console.WriteLine("RX: {0}", vtemp.ToString());
                        try
                        {
                            sw = File.AppendText(logfile);
                            sw.WriteLine(DateTime.Now.ToString() + " RX: ACD:{0} DFC:{1} FUNCTION:{2} ASDUType:{3} Res:{4}",
                                                vtemp.GetControl().ACD, vtemp.GetControl().DFC, vtemp.GetControl().function_0, vtemp.GetAsduType(), vtemp.Res);
                            sw.Close();
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine(ex.Message.ToString());
                        }

                        con.Copy(con, vtemp.GetControl());
                        var datas = vtemp.GetData();
                        foreach (var data in datas)
                        {
                            //if (data.Addr == 0) continue;
                            Console.WriteLine("RX: " + "addr:" + data.Addr.ToString() + " " +
                                                "data:" + data.Data.ToString() + " " +
                                                "time:" + data.Time.ToString());
                            //try
                            //{
                            //    sw = File.AppendText(logfile);
                            //    sw.WriteLine("RX: " + "addr:" + data.Addr.ToString() + " " +
                            //                        "data:" + data.Data.ToString() + " " +
                            //                        "time:" + data.Time.ToString());
                            //    sw.Close();
                            //}
                            //catch (Exception ex)
                            //{
                            //    Console.WriteLine(ex.Message.ToString());
                            //}

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
                        Console.WriteLine("\n");
                        try
                        {
                            sw = File.AppendText(logfile);
                            sw.WriteLine("\r\n");
                            sw.Close();
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine(ex.Message.ToString());
                        }
                        //收到可变长度帧
                        return 3;
                    }
                }
                //收到错误帧
                return 4;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine(ex.ToString());
                //异常处理
                return 4;
                //throw ex;
            }
        }//end UnpackReceive()


        /// <summary>
        /// 对二进制串解报
        /// </summary>
        /// <param name="bytetemp"></param>
        /// <param name="con"></param>
        public int UnpackReceive(byte[] bytetemp, Control con,ref ASDUClass.FunType ft, ref ASDUClass.TransRes tr)
        {
            try
            {
                if ((bytetemp.Length == 1) && (bytetemp[0] == 0xe5))
                {
                    //收到 E5
                    return 1;
                }
                if ((bytetemp.Length == 5) && (bytetemp[0] == 0x10) && (bytetemp[4] == 0x16))
                {
                    Frame_101 temp = new Frame_101(bytetemp);
                    if (temp.GetControl().PRM == false)
                    {
                        con.Copy(con, temp.GetControl());
                        Console.WriteLine("RX: ACD:{0} DFC:{1} FUNCTION:{2}\r\n",
                                             temp.GetControl().ACD, temp.GetControl().DFC, temp.GetControl().function_0);
                        try
                        {
                            sw = File.AppendText(logfile);
                            sw.WriteLine(DateTime.Now.ToString() + " RX: ACD:{0} DFC:{1} FUNCTION:{2}\r\n",
                                                 temp.GetControl().ACD, temp.GetControl().DFC, temp.GetControl().function_0);
                            sw.Close();
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine(ex.Message.ToString());
                        }
                        //收到固定长度帧
                        return 2;
                    }
                }
                if (bytetemp.Length > 5)
                {
                    Frame_101 vtemp = new Frame_101(bytetemp);
                    if ((vtemp != null) && (vtemp.GetControl().PRM == false))
                    {

                        Console.WriteLine("RX: ACD:{0} DFC:{1} FUNCTION:{2} Time:{3} ASDUType:{4} Res:{5}",
                                            vtemp.GetControl().ACD, vtemp.GetControl().DFC, vtemp.GetControl().function_0, DateTime.Now, vtemp.GetAsduType(), vtemp.Res);
                        //Console.WriteLine("RX: {0}", vtemp.ToString());
                        try
                        {
                            sw = File.AppendText(logfile);
                            sw.WriteLine(DateTime.Now.ToString() + " RX: ACD:{0} DFC:{1} FUNCTION:{2} ASDUType:{3} Res:{4}",
                                                vtemp.GetControl().ACD, vtemp.GetControl().DFC, vtemp.GetControl().function_0, vtemp.GetAsduType(), vtemp.Res);
                            sw.Close();
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine(ex.Message.ToString());
                        }

                        con.Copy(con, vtemp.GetControl());
                        ft = vtemp.GetAsduType();
                        tr = vtemp.Res;

                        var datas = vtemp.GetData();
                        foreach (var data in datas)
                        {
                            //if (data.Addr == 0) continue;
                            Console.WriteLine("RX: " + "addr:" + data.Addr.ToString() + " " +
                                                "data:" + data.Data.ToString() + " " +
                                                "time:" + data.Time.ToString());
                            //try
                            //{
                            //    sw = File.AppendText(logfile);
                            //    sw.WriteLine("RX: " + "addr:" + data.Addr.ToString() + " " +
                            //                        "data:" + data.Data.ToString() + " " +
                            //                        "time:" + data.Time.ToString());
                            //    sw.Close();
                            //}
                            //catch (Exception ex)
                            //{
                            //    Console.WriteLine(ex.Message.ToString());
                            //}

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
                        Console.WriteLine("\n");
                        try
                        {
                            sw = File.AppendText(logfile);
                            sw.WriteLine("\r\n");
                            sw.Close();
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine(ex.Message.ToString());
                        }
                        //收到可变长度帧
                        return 3;
                    }
                }
                //收到错误帧
                return 4;
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message.ToString());
                return 4;
            }
        }//end UnpackReceive()
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
                    byte[] SendBuffer = SendList.Dequeue().ToArray();
                    check = DateTime.Now;
                    port1.Write(SendBuffer, 0, SendBuffer.Length);
                    Console.WriteLine("TX: " + BitConverter.ToString(SendBuffer, 0).Replace("-", " "));
                    try
                    {
                        sw = File.AppendText(logfile);
                        sw.WriteLine(DateTime.Now.ToString() + " TX: " + BitConverter.ToString(SendBuffer, 0).Replace("-", " "));
                        sw.Close();
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine(ex.Message.ToString());
                    }
                }
                //心跳检测
                else
                {
                    TimeSpan currentSpan = DateTime.Now.Subtract(check);
                    if (currentSpan.Seconds > m_config_101.m_SendTaskInteval)
                    {
                        //RequestSeconData(false);
                        RequestPrimaryData(false);
                    }
                }
            }
            catch (Exception er)
            {
                Console.WriteLine(er.ToString());
                //Reconnect();
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
            Control con;
            Frame_101 vlm;
            fcb = !fcb;
            con = new Control(prm, fcb, fcv, Control.FUNCTION_1.UserDataConfirm, Control.FUNCTION_0.Undef);
            ASDUClass calymBuffer = new ASDUClass();
            calymBuffer.Pack(ASDUClass.TransRes.Active, ASDUClass.FunType.CalEnergyPulse, m_config_101.m_PublicAddress);
            vlm = new Frame_101(0x68, con, m_config_101.m_LinkAddress, calymBuffer);
            SendList.Clear();
            SendList.Enqueue(vlm);

            fcb = !fcb;
            con = new Control(prm, fcb, fcv, Control.FUNCTION_1.UserDataConfirm, Control.FUNCTION_0.Undef);
            ASDUClass readymBuffer = new ASDUClass();
            readymBuffer.SetData_QCC(0x05, m_config_101.m_PublicAddress);
            vlm = new Frame_101(0x68, con, m_config_101.m_LinkAddress, readymBuffer);
            SendList.Enqueue(vlm);
        }
        /// <summary>
        /// 定时发送总招请求的定时器
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

            

            Control con;
            Frame_101 vlm;   
            fcb = !fcb;
            con = new Control(prm, fcb, fcv, Control.FUNCTION_1.UserDataConfirm, Control.FUNCTION_0.Undef);
            ASDUClass calBuffer = new ASDUClass();
            calBuffer.Pack(ASDUClass.TransRes.Active, ASDUClass.FunType.CalAll, m_config_101.m_PublicAddress);
            vlm = new Frame_101(0x68, con, m_config_101.m_LinkAddress, calBuffer);
            //port1.Write(vlm.ToArray(), 0, vlm.ToArray().Length);
            SendList.Clear();
            SendList.Enqueue(vlm);
        }

        public void ProduceOPC()
        {
            fun = new OPCServerFUN();
            int BOOL = fun.RegisterOPC(m_config_101.m_OPCServerName);
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

    }// end class I_101


    public class CONFG_101
    {
        private INI cfg;
        private int yx_StartAddress = 1;
        private int yx_EndAddress = 4096;
        private int yc_StartAddress = 16385;
        private int yc_EndAddress = 20480;
        private int ym_StartAddress = 25601;
        private int ym_EndAddress = 26112;

        private byte PublicAddress = 1;
        private byte LinkAddress = 1;
        private float SendRate = 1;
        private float YmRate = 300;
        private float CallAllRate = 60;
        private float SendTaskInteval = 5;

        private int DataTye_Len = 1;
        private int SQ_Len = 1;
        private int TransRes_Len = 1;
        private int PublicAddress_Len = 1;
        private int DataAddress_Len = 2;

        private string COM = "";
        private int BaudRate = 9600;
        private int DataBits = 8;
        private int StopBits = 1;
        private int Parity = 0;
        private int Handshake = 0;
        private string OPC_ServerName = "SAC.OPC";

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
        public byte m_PublicAddress
        {
            get { return PublicAddress; }
            set { PublicAddress = value; }
        }
        public byte m_LinkAddress
        {
            get { return LinkAddress; }
            set { LinkAddress = value; }
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
        public CONFG_101(string configFile)
        {
            SetConfig_101(configFile);
        }

        public void SetConfig_101(string configFile)
        {
            FileStream fs = new FileStream(configFile, FileMode.OpenOrCreate);
            fs.Close();

            //try
            {
                cfg = new INI(AppDomain.CurrentDomain.SetupInformation.ApplicationBase + configFile);
                m_COM = cfg.GetVal("Advanced", "COM");
                m_OPCServerName = cfg.GetVal("Advanced", "OPCServerName");
                m_BaudRate = Convert.ToInt32(cfg.GetVal("Advanced", "BaudRate"));
                m_DataBits = Convert.ToInt32(cfg.GetVal("Advanced", "DataBits"));
                m_StopBits = Convert.ToInt32(cfg.GetVal("Advanced", "StopBits"));
                m_Parity = Convert.ToInt32(cfg.GetVal("Advanced", "Parity"));
                m_Handshake = Convert.ToInt32(cfg.GetVal("Advanced", "Handshake"));
                m_LinkAddress = Convert.ToByte(cfg.GetVal("Advanced", "LinkAddress"));
                m_PublicAddress = Convert.ToByte(cfg.GetVal("Advanced", "PublicAddress"));
                m_SendRate = Convert.ToSingle(cfg.GetVal("Advanced", "SendRate"));
                m_YmRate = Convert.ToSingle(cfg.GetVal("Advanced", "YmRate"));
                m_CallAllRate = Convert.ToSingle(cfg.GetVal("Advanced", "CallAllRate"));
                m_SendTaskInteval = Convert.ToSingle(cfg.GetVal("Advanced", "SendTaskInteval"));
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
            //    sw.WriteLine("COM=COM4");
            //    sw.WriteLine("BaudRate=9600");
            //    sw.WriteLine("DataBits=8");
            //    sw.WriteLine("StopBits=1");
            //    sw.WriteLine("Parity=0");
            //    sw.WriteLine("LinkAddress=1");
            //    sw.WriteLine("PublicAddress=1");
            //    sw.WriteLine("SendRate=1");
            //    sw.WriteLine("YmRate=100");
            //    sw.WriteLine("CallAllRate=60");
            //    sw.WriteLine("SendTaskInteval=5");
            //    sw.WriteLine("Handshake=0");
            //    sw.WriteLine("OPCServerName=SAC.OPC.101");
            //    sw.WriteLine("[DATA]");
            //    sw.WriteLine("yx_StartAddress=1");
            //    sw.WriteLine("yc_StartAddress=16385");
            //    sw.WriteLine("ym_StartAddress=25601");
            //    sw.Close();
            //}
        }
    }
}