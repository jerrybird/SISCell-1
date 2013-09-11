using System;
using RTDBApi;

namespace SISCell
{
    public class O_HS : oProtocol
    {
        private INI cfg;
        private LOG err;
        private string _serverIP, _serverPort, _instance, _user, _passWord;

        public O_HS()
        {
            cfg = new INI(AppDomain.CurrentDomain.SetupInformation.ApplicationBase + "config\\hs.ini");
            err = new LOG();
            _serverIP = cfg.GetVal("Advanced", "IPAddress");
            _serverPort = cfg.GetVal("Advanced", "Port");
            _user = cfg.GetVal("Advanced", "UserId");
            _passWord = cfg.GetVal("Advanced", "PassWord");
        }

        public bool Connect()
        {
            string server = string.Format("{0}:{1}", _serverIP, _serverPort);
            string[] instances = { "" };

            if (!HSApi.CS_GetServices(server, out instances)) return false;
            _instance = instances[0];
            if ("".Equals(instances[0])) return false;
            return HSApi.CS_LogOnServer(instances[0], _user, _passWord);
        }

        public void DisConnect()
        {
            HSApi.CS_CloseConnection();
        }

        public bool Connected
        {
            get { return (0 == HSApi.CS_GetConnectionStatus(_instance)); }
        }

        public void InitPt(int nNum, numInf[] nrst, int sNum, strInf[] srst) { }

        public void SetRtValue(int nNum, numInf[] nrst, int sNum, strInf[] srst)
        {
            string[] pt = new string[nNum];
            DateTime[] dt = new DateTime[nNum];
            uint[] stat = new uint[nNum];
            float[] val = new float[nNum];
            int i = 0;
            foreach (numInf nr in nrst)
            {
                pt[i] = nr.dstId;
                dt[i] = nr.dtm;
                stat[i] = 0;
                val[i] = nr.val;
                ++i;
            }
            HSApi.CS_UpdateValueByName(RTDBApi.HSApi.ConnStr + "@" + RTDBApi.HSApi.CurrentInstance, pt, dt, stat, val, 0);

            pt = new string[sNum];
            dt = new DateTime[sNum];
            stat = new uint[sNum];
            string[] sval = new string[sNum];
            i = 0;
            foreach (strInf sr in srst)
            {
                pt[i] = sr.dstId;
                dt[i] = sr.dtm;
                stat[i] = 0;
                sval[i] = sr.val;
                ++i;
            }
            HSApi.CS_UpdateValueByName_S(pt, dt, stat, sval);
        }

    }
}