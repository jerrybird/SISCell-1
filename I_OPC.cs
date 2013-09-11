using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Ports;
using System.Net;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using OPCAutomation;

namespace SISCell
{
   ////数值型结构体
   // public struct numInf
   // {
   //     public short sn;
   //     public string srcId;
   //     public string dstId;
   //     public string desc;
   //     public float val;
   //     public DateTime dtm;
   //     public string ratio;
   //     public int datatype;
   // }
   // ////字符串型结构体
   // public struct strInf
   // {
   //     public short sn;
   //     public string srcId;
   //     public string dstId;
   //     public string desc;
   //     public string val;
   //     public DateTime dtm;
   //     //public string typeIndex;
   //     public int srcAddr;
   // }
   // interface iProtocol
   // {
   //     //void Init();
   //     bool Connect();
   //     void DisConnect();
   //     bool Connected { get; }
   //     void InitPt(int nNum, numInf[] nrst, int sNum, strInf[] srst);
   //     bool GetRtValue(int nNum, numInf[] nrst, int sNum, strInf[] srst);
   //     void SetRtValue(int nNum, numInf[] nrst, int sNum, strInf[] srst);
   // }
   // class INI
   // {
   //     [DllImport("kernel32")]
   //     private static extern long WritePrivateProfileString(string section, string key, string val, string filePath);

   //     [DllImport("kernel32")]
   //     private static extern int GetPrivateProfileString(string section, string key, string defVal, StringBuilder retVal, int size, string filePath);

   //     private string _fileName;

   //     public INI(string fileName)
   //     {
   //         _fileName = fileName;
   //     }

   //     public void SetVal(string section, string key, string iValue)
   //     {
   //         WritePrivateProfileString(section, key, iValue, _fileName);
   //     }

   //     public string GetVal(string section, string key)
   //     {
   //         StringBuilder temp = new StringBuilder(255);
   //         int i = GetPrivateProfileString(section, key, "", temp, 255, _fileName);
   //         return temp.ToString();
   //     }
   // }
    public class I_OPC : iProtocol
    {
        #region 私有变量
        /// <summary>
        /// OPCServer Object
        /// </summary>
        OPCServer MyServer;
        /// <summary>
        /// OPCGroups Object
        /// </summary>
        OPCGroups MyGroups;
        /// <summary>
        /// OPCGroup Object
        /// </summary>
        OPCGroup MyGroup;
        OPCGroup MyGroup2;
        /// <summary>
        /// OPCItems Object
        /// </summary>
        OPCItems MyItems;
        OPCItems MyItems2;
        /// <summary>
        /// OPCItem Object
        /// </summary>
        OPCItem[] MyItem;
        OPCItem[] MyItem2;
        /// <summary>
        /// 主机IP
        /// </summary>
        string strHostIP = "";
        /// <summary>
        /// 主机名称
        /// </summary>
        string strHostName = "";
        /// <summary>
        /// OPC服务名
        /// </summary>
        string serverProgID = "";
        /// <summary>
        /// 连接状态
        /// </summary>
        bool opc_connected = false;
        /// <summary>
        /// 客户端句柄
        /// </summary>
        int itmHandleClient = 0;
        /// <summary>
        /// 服务端句柄
        /// </summary>
        int itmHandleServer = 0;
        /// <summary>
        /// 配置文件
        /// </summary>
        INI cfg;
        /// <summary>
        /// OPC_tag 数
        /// </summary>
        int point_nums;
        int add_hours;
        //检查周期
        DateTime check = new DateTime();
        int connectRate = 5000;
        //定时器_发送报文
        System.Timers.Timer tmConnect;
        numInf[] numinf;
        strInf[] strinf;
        int numNum;
        int strNum;


        //输出文本路径
        StreamWriter sw;
        string logfile;

        #endregion

        public I_OPC()
        {
            try						// disabled for debugging
            {
                cfg = new INI(AppDomain.CurrentDomain.SetupInformation.ApplicationBase + "config\\opc.ini");
                strHostIP = cfg.GetVal("Connect", "OPCSERVER_IP");
                serverProgID = cfg.GetVal("Connect", "OPCSERVER_NAME");
                add_hours = Convert.ToInt32(cfg.GetVal("Connect", "ADD_HOURS"));
                if (strHostIP == "" || serverProgID == "")
                {
                    sw = File.AppendText("config\\opc.ini");
                    sw.WriteLine("OPCSERVER_IP=127.0.0.1");
                    sw.WriteLine("OPCSERVER_NAME=");
                    sw.WriteLine("ADD_HOURS=0");
                    sw.Close();
                }

                logfile = "log.txt";
                FileStream fs = new FileStream(logfile, FileMode.OpenOrCreate);
                fs.Close();

                sw = File.AppendText(logfile);
                sw.WriteLine("初始化配置成功！");
                sw.Close();

            }
            catch (Exception e)
            {
                Console.WriteLine(e.ToString());
                Console.ReadLine();
                return;
            }
        }

        public void InitPt(int nNum, numInf[] nrst, int sNum, strInf[] srst)
        {
            try
            {
                MyItems = MyGroup.OPCItems;
                MyItem = new OPCItem[nNum + sNum];
                numinf = new numInf[nNum];
                strinf = new strInf[sNum];
                numNum = nNum;
                strNum = sNum;

                int i = 0;
                foreach (numInf nn in nrst)
                {
                    try
                    {
                        MyItem[i] = MyItems.AddItem(nn.srcId, i);//byte
                        i++;
                    }
                    catch
                    {
                        try
                        {
                            sw = File.AppendText(logfile);
                            sw.WriteLine("初始化点" + nn.srcId + "失败！");
                            sw.Close();
                        }
                        catch { }
                        //MyItem[i] = MyItems.AddItem(nrst[i - 1].srcId, i);
                        MyItem[i] = MyItem[i - 1];
                        i++;
                        continue;
                    }
                }
                foreach (strInf ss in srst)
                {
                    try
                    {
                        MyItem[i] = MyItems.AddItem(ss.srcId, i);//byte
                        i++;
                    }
                    catch
                    {
                        try
                        {
                            sw = File.AppendText(logfile);
                            sw.WriteLine("初始化点" + ss.srcId + "失败！");
                            sw.Close();
                        }
                        catch { }
                        MyItem[i] = MyItem[i - 1];
                        i++;
                        continue;
                    }
                }

                object ItemValuestemp; object Qualities; object TimeStamps;//同步读的临时变量：值、质量、时间戳
                for (int ii = 0; ii < nNum + sNum; ii++)
                {
                    MyItem[ii].Read(1, out ItemValuestemp, out Qualities, out TimeStamps);//同步读，第一个参数只能为1或2
                    if (ii < nNum)
                    {
                        numinf[ii].val = Convert.ToSingle(ItemValuestemp);//转换后获取item值
                        numinf[ii].dtm = Convert.ToDateTime(TimeStamps);
                    }
                    else
                    {
                        strinf[ii - numNum].val = Convert.ToString(ItemValuestemp);//转换后获取item值
                        strinf[ii - numNum].dtm = Convert.ToDateTime(TimeStamps);
                    }
                }

                MyGroup.IsSubscribed = true;//使用订阅功能，即可以异步，默认false
                try
                {
                    sw = File.AppendText(logfile);
                    sw.WriteLine("初始化点成功！");
                    sw.Close();
                }
                catch { }
            }
            catch (Exception e)
            {
                Console.WriteLine(e.ToString());
            }
        }
        public bool Connect()
        {
            try
            {
                IPHostEntry ipHostEntry = Dns.GetHostByAddress(strHostIP);
                strHostName = ipHostEntry.HostName.ToString();
                MyServer = new OPCServer();
                try
                {
                    MyServer.Connect(serverProgID, strHostIP);//连接服务器：服务器名+主机名或IP
                    if ((MyServer.ServerState == (int)OPCServerState.OPCFailed) && (MyServer.ServerState == (int)OPCServerState.OPCDisconnected))
                    {
                        Console.WriteLine("已连接到：{0}::{1}",strHostIP, MyServer.ServerName);
                        Console.WriteLine("服务器状态：{0}", MyServer.ServerState.ToString());
                        try
                        {
                            sw = File.AppendText(logfile);
                            sw.WriteLine("服务器状态：{0}", MyServer.ServerState.ToString());
                            sw.WriteLine("已连接到：{0}::{1}", strHostIP, MyServer.ServerName);
                            sw.Close();
                        }
                        catch { }
                        opc_connected = true;
                    }
                    else
                    {
                        //这里你可以根据返回的状态来自定义显示信息，请查看自动化接口API文档
                        Console.WriteLine("服务器状态：{0}", MyServer.ServerState.ToString());
                        opc_connected = false;
                        try
                        {
                            sw = File.AppendText(logfile);
                            sw.WriteLine("服务器状态：{0}", MyServer.ServerState.ToString());
                            sw.Close();
                        }
                        catch { }
                    }
                    //MyServer.ServerShutDown += DisConnect;//服务器断开事件
                }
                catch (Exception err)
                {
                    Console.WriteLine("连接远程服务器出现错误：{0}" + err.Message);
                    try
                    {
                        sw = File.AppendText(logfile);
                        sw.WriteLine("连接远程服务器出现错误：{0}" + err.Message);
                        sw.Close();
                    }
                    catch { }
                    opc_connected = false;
                    return false;
                }
                try
                {
                    MyGroup = MyServer.OPCGroups.Add("sac_opc");//添加组
                    //以下设置组属性
                    {
                        MyServer.OPCGroups.DefaultGroupIsActive = true;//激活组。
                        MyServer.OPCGroups.DefaultGroupDeadband = 0;// 死区值，设为0时，服务器端该组内任何数据变化都通知组。
                        MyServer.OPCGroups.DefaultGroupUpdateRate = 200;//默认组群的刷新频率为200ms
                        MyGroup.UpdateRate = 1000;//刷新频率为1秒。
                        //MyGroup.IsSubscribed = true;//使用订阅功能，即可以异步，默认false
                    }
                    MyGroup.DataChange += new DIOPCGroupEvent_DataChangeEventHandler(GroupDataChange);
                    //MyGroup.AsyncWriteComplete += new DIOPCGroupEvent_AsyncWriteCompleteEventHandler(GroupAsyncWriteComplete);
                    MyGroup.AsyncReadComplete += new DIOPCGroupEvent_AsyncReadCompleteEventHandler(GroupAsyncReadComplete);
                }
                catch (Exception err)
                {
                    Console.WriteLine("创建组出现错误：{0}", err.Message);
                    try
                    {
                        sw = File.AppendText(logfile);
                        sw.WriteLine("创建组出现错误：{0}", err.Message);
                        sw.Close();
                    }
                    catch { }
                    return false;
                }
                return true;
            }
            catch (Exception e)
            {
                Console.WriteLine(e.ToString());
                Console.ReadLine();
                return false;
            }

            tmConnect = new System.Timers.Timer();
            tmConnect.AutoReset = true;
            tmConnect.Interval = connectRate;
            tmConnect.Elapsed += new System.Timers.ElapsedEventHandler(tm_Connect);
            tmConnect.Start();
            return true;

        }

        /// <summary>
        /// 每当项数据有变化时执行的事件
        /// </summary>
        /// <param name="TransactionID">处理ID</param>
        /// <param name="NumItems">项个数</param>
        /// <param name="ClientHandles">项客户端句柄</param>
        /// <param name="ItemValues">TAG值</param>
        /// <param name="Qualities">品质</param>
        /// <param name="TimeStamps">时间戳</param>1    `
        void GroupDataChange(int TransactionID, int NumItems, ref Array ClientHandles, ref Array ItemValues, ref Array Qualities, ref Array TimeStamps)
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


            for (int i = 1; i <= NumItems; i++)
            {
                int id = Convert.ToInt32(ClientHandles.GetValue(i));
                if (id < numNum)
                {
                    numinf[id].val = Convert.ToSingle(ItemValues.GetValue(i));
                    numinf[id].dtm = Convert.ToDateTime( TimeStamps.GetValue(i));
                    
                }
                else
                {
                    strinf[id - numNum].val = ItemValues.GetValue(i).ToString();
                    strinf[id - numNum].dtm = Convert.ToDateTime( TimeStamps.GetValue(i));
                    
                }
                //try
                //{
                //    sw = File.AppendText(logfile);
                //    sw.WriteLine("itemID：{0}   value：{1}", id.ToString(), ItemValues.GetValue(i).ToString());
                //    sw.Close();
                //}
                //catch { }
                //Console.WriteLine("item值：{0}", ItemValues.GetValue(i).ToString());
                //Console.WriteLine("item句柄：{0}", ClientHandles.GetValue(i).ToString());
                //Console.WriteLine("item质量：{0}", Qualities.GetValue(i).ToString());
                //Console.WriteLine("item时间戳：{0}", TimeStamps.GetValue(i).ToString());
                //Console.WriteLine("item类型：{0}", ItemValues.GetValue(i).GetType().FullName);
            }
        }
        /// <summary>
        /// 异步读完成
        /// 运行时，Array数组从下标1开始而非0！
        /// </summary>
        /// <param name="TransactionID"></param>
        /// <param name="NumItems"></param>
        /// <param name="ClientHandles"></param>
        /// <param name="ItemValues"></param>
        /// <param name="Qualities"></param>
        /// <param name="TimeStamps"></param>
        /// <param name="Errors"></param>
        void GroupAsyncReadComplete(int TransactionID, int NumItems, ref System.Array ClientHandles, ref System.Array ItemValues, ref System.Array Qualities, ref System.Array TimeStamps, ref System.Array Errors)
        {
            Console.WriteLine("****************GroupAsyncReadComplete*******************");
            for (int i = 1; i <= NumItems; i++)
            {
                //Console.WriteLine("Tran：{0}   ClientHandles：{1}   Error：{2}", TransactionID.ToString(), ClientHandles.GetValue(i).ToString(), Errors.GetValue(i).ToString());
                Console.WriteLine("Vaule：{0}", Convert.ToString(ItemValues.GetValue(i)));
                try
                {
                    sw = File.AppendText(logfile);
                    sw.WriteLine("value：{0}", ItemValues.GetValue(i).ToString());
                    sw.Close();
                }
                catch { }
            }
        }

        public bool Connected
        {
            get
            {
                try
                {
                    if (MyServer.ServerState == (int)OPCServerState.OPCRunning)
                    {
                        return true;
                    }
                    else
                    {
                        return false;
                    }
                }
                catch { return false; }
            }
        }
        /// <summary>
        /// 定时发送心跳检测
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void tm_Connect(object sender, System.Timers.ElapsedEventArgs e)
        {
            //心跳检测
            TimeSpan currentSpan = DateTime.Now.Subtract(check);
            if (currentSpan.Seconds > 10)
            {
                try
                {
                    if (MyServer.ServerState == (int)OPCServerState.OPCRunning)
                    {
                        check = DateTime.Now;
                    }
                    else
                    {
                        Reconnect();
                    }
                }
                catch { Reconnect(); }
            }
        }
        public void Reconnect()
        {
            try
            {
                DisConnect();
                Connect();
            }
            catch (Exception e)
            {
                Console.WriteLine(e.ToString());
                return;
            }
        }
        public void SetRtValue(int nNum, numInf[] nrst, int sNum, strInf[] srst)
        {
        }
        public bool GetRtValue(int nNum, numInf[] nrst, int sNum, strInf[] srst)
        {
            try
            {
                for (int i = 0; i < nNum; i++)
                {
                    try
                    {
                        nrst[i].val = numinf[i].val * Convert.ToSingle(nrst[i].ratio);
                        nrst[i].dtm = numinf[i].dtm.AddHours(add_hours);
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
                for (int j = 0; j < sNum; j++)
                {
                    try
                    {
                        if (strinf[j].val == null)
                        {
                            srst[j].val = "0";
                            srst[j].dtm = DateTime.Now;
                        }
                        else
                        {
                            srst[j].val = strinf[j].val;
                            srst[j].dtm = strinf[j].dtm.AddHours(add_hours);
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
                
            }
            catch (Exception e)
            {
                Console.WriteLine(e.ToString());
                try
                {
                    sw = File.AppendText(logfile);
                    sw.WriteLine(DateTime.Now.ToString() + e.ToString());
                    sw.Close();
                }
                catch { }
                return false;
            }
            try
            {
                sw = File.AppendText(logfile);
                sw.WriteLine(DateTime.Now.ToString()+"\tUpdate Value!");
                sw.Close();
            }
            catch { }
            return true;

        }

        public void DisConnect()
        {
            try
            {
                MyServer.Disconnect();
                opc_connected = false;
            }
            catch (Exception e)
            {
                Console.WriteLine(e.ToString());
                return;
            }
        }
    }
}

  