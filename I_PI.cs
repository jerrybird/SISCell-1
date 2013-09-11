using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Runtime.InteropServices;

namespace SISCell
{
    class I_PI : iProtocol
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
        private static extern int pisn_getsnapshot(int pt, out float rval, out int istat, out int timedate);

        [DllImport("piapi32.dll")]
        private static extern int pipt_pointtype(int pt, ref char type);

        [DllImport("piapi32.dll")]
        private static extern void pitm_secint(int timedate, out int[] timearray);

        private INI cfg;
        private LOG err;
        private string _server, _user, _password;
        private int[] numID = null, strID = null;
        private char[] numT = null, strT = null;

        public I_PI()
        {
            cfg = new INI(AppDomain.CurrentDomain.SetupInformation.ApplicationBase + "config\\pi.ini");
            err = new LOG();
            _server = cfg.GetVal("Connect", "Server");
            _user = cfg.GetVal("Connect", "User");
            _password = cfg.GetVal("Connect", "Password");
        }

        private DateTime GetSysTime(int dt)
        {
            int[] lTimeArray;
            pitm_secint(dt, out lTimeArray);
            DateTime dtm = new DateTime(lTimeArray[2], lTimeArray[0], lTimeArray[1], lTimeArray[3], lTimeArray[4], lTimeArray[5]);
            return dtm;
        }

        #region iProtocol 成员

        public bool Connected
        {
            get { return (1 == piut_isconnected()); }
        }

        public bool Connect()
        {
            int valid = -1;
            if (piut_setservernode(_server) != 0) return false;
            return 0 == piut_login(_user, _password, ref valid);
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
                pipt_findpoint(nr.srcId, ref numID[i]);
                if (-5 != numID[i]) pipt_pointtype(numID[i], ref numT[i]);
                else err.WrtMsg(string.Format("测点{0}不存在.", nr.srcId));
                ++i;
            }

            i = 0;
            foreach (strInf sr in srst)
            {
                pipt_findpoint(sr.srcId, ref strID[i]);
                if (-5 != strID[i]) pipt_pointtype(strID[i], ref strT[i]);
                else err.WrtMsg(string.Format("测点{0}不存在.", sr.srcId));
                ++i;
            }
        }

        public bool GetRtValue(int nNum, numInf[] nrst, int sNum, strInf[] srst)
        {
            for (int i = 0; i < nNum; ++i)
            {
                float rval = 0;
                int istat = 0, timedate = 0;

                int ret = pisn_getsnapshot(numID[i], out rval, out istat, out timedate);
                if (0 == ret)
                {
                    switch (numT[i])
                    {
                        case 'R':
                            nrst[i].val = rval;
                            break;
                        case 'I':
                        case 'D':
                            nrst[i].val = (float)istat;
                            break;
                        default:
                            err.WrtMsg(string.Format("测点{0}类型不匹配.", nrst[i].srcId));
                            break;
                    }
                    nrst[i].dtm = GetSysTime(timedate);
                }
            }
            return true;
        }

        #endregion
    }
}
