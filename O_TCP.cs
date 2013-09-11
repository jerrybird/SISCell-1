using System;
using System.Net;
using System.Net.Sockets;

namespace SISCell
{
    class O_TCP : oProtocol
    {
        private INI cfg;
        private LOG err;
        private PACK rtVal;
        private EndPoint ep;
        private Socket stSend;

        public O_TCP()
        {
            cfg = new INI(AppDomain.CurrentDomain.SetupInformation.ApplicationBase + "config\\tcp.ini");
            err = new LOG();
            rtVal = new PACK();
            ep = (EndPoint)new IPEndPoint(IPAddress.Parse(cfg.GetVal("Remote", "IP")), int.Parse(cfg.GetVal("Remote", "Port")));
        }


        #region oProtocol 成员

        public bool Connected
        {
            get
            {
                try
                {
                    if (stSend.Connected && stSend.Poll(0, SelectMode.SelectRead))
                    {
                        byte[] test = new byte[1];
                        return (0 != stSend.Receive(test, 0, 1, SocketFlags.Peek));
                    }
                    return stSend.Connected;
                }
                catch (Exception)
                {
                    return false;
                }
            }
        }

        public bool Connect()
        {
            try
            {
                if (null != stSend) stSend.Close();
                stSend = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
                stSend.Connect(ep);
                return true;
            }
            catch (SocketException ex)
            {
                if (10061 != ex.ErrorCode) err.WrtMsg(ex.Message);
                return false;
            }
        }

        public void DisConnect()
        {
            if (stSend.Connected)
            {
                stSend.Shutdown(SocketShutdown.Both);
                stSend.Close();
            }
        }

        public void InitPt(int nNum, numInf[] nrst, int sNum, strInf[] srst)
        {
        }

        public void SetRtValue(int nNum, numInf[] nrst, int sNum, strInf[] srst)
        {
            byte[] pack = rtVal.PutData(nNum, nrst, sNum, srst);

            try
            {stSend.Send(pack);}
            catch (Exception)
            {}
        }

        #endregion
    }
}
