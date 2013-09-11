using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Runtime.InteropServices;

namespace SISCell
{
    class O_PI : oProtocol
    {
        [DllImport("piapi32.dll")]
        private static extern int piut_setservernode(string servername);

        [DllImport("piapi32.dll")]
        private static extern int piut_login(string username, string password, ref int valid);

        [DllImport("piapi32.dll")]
        private static extern int piut_disconnect();

        [DllImport("piapi32.dll")]
        private static extern int piut_isconnected();

        [DllImport("piapi32.dll")]
        private static extern int pipt_findpoint(string tagname, ref int pt);

        [DllImport("piapi32.dll")]
        private static extern int pisn_putsnapshot(int pt, float rval, int istat, int timedate);

        [DllImport("piapi32.dll")]
        private static extern int pipt_pointtype(int pt, ref char type);

        [DllImport("piapi32.dll")]
        private static extern void pitm_intsec(out int timedate, int[] timearray);

        private INI cfg;
        private LOG err;
        private string _server, _user, _password;
        private int[] numID = null, strID = null;
        private char[] numT = null, strT = null;

        public O_PI()
        {
            cfg = new INI(AppDomain.CurrentDomain.SetupInformation.ApplicationBase + "config\\pi.ini");
            err = new LOG();
            _server = cfg.GetVal("Connect", "Server");
            _user = cfg.GetVal("Connect", "User");
            _password = cfg.GetVal("Connect", "Password");
        }

        private int GetIntTime(DateTime dt)
        {
            int ltm;
            int[] lTimeArray = { dt.Month, dt.Day, dt.Year, dt.Hour, dt.Minute, dt.Second };
            pitm_intsec(out ltm, lTimeArray);
            return ltm;
        }

        #region oProtocol 成员

        public bool Connected
        {
            get { return (1 == piut_isconnected()); }
        }

        public bool Connect()
        {
            int LoginValid = -1;
            if (piut_setservernode(_server) != 0) return false;
            return 0 == piut_login(_user, _password, ref LoginValid);
        }

        public void DisConnect()
        {
            piut_disconnect();
        }

        public void InitPt(int nNum, numInf[] nrst, int sNum, strInf[] srst)
        {
            numID = new int[nNum];
            strID = new int[sNum];
            numT = new char[nNum];
            strT = new char[sNum];

            int i = 0;
            foreach (numInf nr in nrst)
            {
                pipt_findpoint(nr.dstId, ref numID[i]);
                if (-5 != numID[i]) pipt_pointtype(numID[i], ref numT[i]);
                else err.WrtMsg(string.Format("测点{0}不存在.", nr.dstId));
                ++i;
            }
        }

        public void SetRtValue(int nNum, numInf[] nrst, int sNum, strInf[] srst)
        {
            int i = 0;
            foreach (numInf nr in nrst)
            {
                int ret = 0;
                switch (numT[i])
                {
                    case 'R':
                        ret = pisn_putsnapshot(numID[i], nr.val, 0, GetIntTime(nr.dtm));
                        break;
                    case 'I':
                    case 'D':
                        ret = pisn_putsnapshot(numID[i], 0, (int)nr.val, GetIntTime(nr.dtm));
                        break;
                    default:
                        break;
                }
                if (0 != ret) err.WrtMsg(string.Format("测点{0}写入失败,错误号:{1}.", nr.dstId,ret.ToString()));
                ++i;
            }
        }

        #endregion
    }
}
