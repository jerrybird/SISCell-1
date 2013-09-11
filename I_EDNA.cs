using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using InStep.eDNA.EzDNAApiNet;

namespace SISCell
{
    class I_EDNA : iProtocol
    {
        #region iProtocol 成员

        public bool Connected
        {
            get { return true; }
        }

        public bool Connect()
        {
            return true;
        }

        public void DisConnect()
        {
        }

        public void InitPt(int nNum, numInf[] nrst, int sNum, strInf[] srst)
        {
        }

        public bool GetRtValue(int nNum, numInf[] nrst, int sNum, strInf[] srst)
        {
            for (int i = 0; i < nNum; ++i)
            {
                double dval = 0;
                int time = 0;
                ushort stat = 0;

                int ret = RealTime.DNAGetRTShort(nrst[i].srcId, out dval, out time,out stat);
                if (0 == ret)
                {
                    nrst[i].val = (float)dval;
                    nrst[i].dtm = Utility.GetDateTime(time);
                }
            }
            return true;
        }

        #endregion
    }
}
