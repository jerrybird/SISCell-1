using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Data;
using System.Data.SqlClient;

namespace SISCell
{
    class I_CPI : iProtocol
    {
        private INI cfg;
        private LOG err;
        private string _server, _dataBase, _user, _password, _concmd;
        private SqlConnection _con;

        private bool _connected = false;

        public I_CPI()
        {
            cfg = new INI(AppDomain.CurrentDomain.SetupInformation.ApplicationBase + "config\\cpi.ini");
            err = new LOG();
            _server = cfg.GetVal("Connect", "Server");
            _dataBase = cfg.GetVal("Connect", "Database");
            _user = cfg.GetVal("Connect", "User");
            _password = cfg.GetVal("Connect", "Password");
            StringBuilder cmd = new StringBuilder(string.Format("Data Source={0},1433;Network Library=DBMSSOCN;Initial Catalog={1};User ID={2};Password={3};", _server, _dataBase, _user, _password));
            _concmd = cmd.ToString();
        }

        #region iProtocol 成员

        public bool Connected
        {
            get { return _connected; }
        }

        public bool Connect()
        {
            try
            {
                _con = new SqlConnection(_concmd);
                _con.Open();
                _connected = true;
                return true;
            }
            catch (Exception ex)
            {
                _connected = false;
                err.WrtMsg(ex.Message);
                return false;
            }
        }

        public void DisConnect()
        {
            if (ConnectionState.Closed != _con.State) _con.Close();
            _connected = false;
        }

        public void InitPt(int nNum, numInf[] nrst, int sNum, strInf[] srst)
        {
            SqlCommand cmd;
            cmd = new SqlCommand("SELECT ID_KEY,TAG,DSC FROM TAGCONFIG WHERE TYPE=0 AND SENDFLAG=1 ORDER BY ID_KEY", _con);
            using (SqlDataReader dtr = cmd.ExecuteReader())
            {
                int i = 0;
                while (dtr.Read())
                {
                    nrst[i].sn = i;
                    nrst[i].srcId = dtr.GetString(1);
                    nrst[i].dstId = dtr.GetString(1);
                    nrst[i].desc = dtr.GetString(2);
                    ++i;
                }
            }
            cmd = new SqlCommand("SELECT ID_KEY,TAG,DSC FROM TAGCONFIG WHERE TYPE=1 AND SENDFLAG=1 ORDER BY ID_KEY", _con);
            using (SqlDataReader dtr = cmd.ExecuteReader())
            {
                int i = 0;
                while (dtr.Read())
                {
                    srst[i].sn = nNum + i;
                    srst[i].srcId = dtr.GetString(1);
                    srst[i].dstId = dtr.GetString(1);
                    srst[i].desc = dtr.GetString(2);
                    ++i;
                }
            }
            cmd.Dispose();
        }

        public bool GetRtValue(int nNum, numInf[] nrst, int sNum, strInf[] srst)
        {
            try
            {
                using (SqlCommand cmd = new SqlCommand("SELECT a.TAG,a.VALUE,a.TIME FROM REALVALUE a INNER JOIN TAGCONFIG b ON b.TYPE=0 AND a.TAG=b.TAG ORDER BY b.ID_KEY", _con))
                using (SqlDataAdapter dad = new SqlDataAdapter(cmd))
                using (DataTable dtbl = new DataTable())
                {
                    dad.Fill(dtbl);
                    using (DataView dvw = new DataView(dtbl, "", "TAG", DataViewRowState.CurrentRows))
                    {
                        for (int i = 0; i < nrst.Length; ++i)
                        {
                            int idx = dvw.Find(nrst[i].srcId);
                            if (-1 != idx)
                            {
                                nrst[i].val = Convert.ToSingle(dvw[idx][1]);
                                nrst[i].dtm = Convert.ToDateTime(dvw[idx][2]);
                            }
                        }
                    }
                }

                using (SqlCommand cmd = new SqlCommand("SELECT a.TAG,a.VALUE,a.TIME FROM REALVALUE a INNER JOIN TAGCONFIG b ON b.TYPE=1 AND a.TAG=b.TAG ORDER BY b.ID_KEY", _con))
                using (SqlDataAdapter dad = new SqlDataAdapter(cmd))
                using (DataTable dtbl = new DataTable())
                {
                    dad.Fill(dtbl);
                    using (DataView dvw = new DataView(dtbl, "", "TAG", DataViewRowState.CurrentRows))
                    {
                        for (int i = 0; i < srst.Length; ++i)
                        {
                            int idx = dvw.Find(srst[i].srcId);
                            if (-1 != idx)
                            {
                                srst[i].val = dvw[idx][1].ToString();
                                srst[i].dtm = Convert.ToDateTime(dvw[idx][2]);
                            }
                        }
                    }
                }
                return true;
            }
            catch (Exception ex)
            {
                DisConnect();
                Connect();
                err.WrtMsg(ex.Message);
                return false;
            }
        }

        #endregion
    }
}
