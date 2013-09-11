using System;
using System.Net;
using System.Net.Sockets;
using System.Threading;

namespace SISCell
{
    class I_TCP : iProtocol
    {

        private INI cfg;
        private LOG err;
        private string _remoteIP;
        private EndPoint ep;
        private Socket stReceive;
        private Thread th;
        private PACK rtVal;
        private const int MAXLEN = 1024 * 128;

        public I_TCP()
        {
            cfg = new INI(AppDomain.CurrentDomain.SetupInformation.ApplicationBase + "config\\tcp.ini");
            err = new LOG();
            rtVal = new PACK();
            _remoteIP = cfg.GetVal("Remote", "IP");
            ep = (EndPoint)new IPEndPoint(IPAddress.Any, int.Parse(cfg.GetVal("Local", "Port")));
        }

        private void Listen()
        {
            Socket stRead;
            EndPoint remoteEP;
            byte[] _receivebyte;

            while (true)
            {
                try
                {
                    stRead = stReceive.Accept();
                    remoteEP = stRead.RemoteEndPoint;
                    IPEndPoint remoteIP = (IPEndPoint)remoteEP;
                    if (_remoteIP.Length > 0 && _remoteIP != remoteIP.Address.ToString()) continue;

                    while (true)
                    {
                        byte[] read = new byte[MAXLEN];
                        int iRead = stRead.ReceiveFrom(read, ref remoteEP);
                        if (0 == iRead) break;
                        _receivebyte = new byte[iRead];
                        Buffer.BlockCopy(read,0,_receivebyte,0,iRead);
                        rtVal.Verify(_receivebyte, iRead);                       
                    }
                }
                catch (Exception ex)
                {
                    err.WrtMsg(ex.Message);
                    continue;
                }
            }
        }

        #region iProtocol 成员

        public bool Connected
        {
            get { return ThreadState.Background == th.ThreadState;}
        }

        public bool Connect()
        {
            if (null != stReceive) stReceive.Close();
            stReceive = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
            stReceive.Bind(ep);
            stReceive.Listen(0);
            th = new Thread(new ThreadStart(Listen));
            th.IsBackground = true;
            th.Start();
            return true;
        }

        public void DisConnect()
        {
            if (stReceive.Connected)
            {
                stReceive.Shutdown(SocketShutdown.Both);
                stReceive.Close();
            }
        }

        public void InitPt(int nNum, numInf[] nrst, int sNum, strInf[] srst)
        {
        }

        public bool GetRtValue(int nNum, numInf[] nrst, int sNum, strInf[] srst)
        {
            return rtVal.GetData(nNum, nrst, sNum, srst);
        }

        #endregion
    }
}
