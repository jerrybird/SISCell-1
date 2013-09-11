using System;
using System.Data;
using System.Data.SqlClient;
using System.Text;

namespace SISCell
{
    class I_MSSQL : iProtocol
    {
        private INI cfg;
        private LOG err;
        private string _server, _dataBase, _user, _password, _concmd;
        private SqlConnection _con;
        private string _tblName, _tagField, _valField, _dtmField, _rtcmd;
        private bool _connected = false;

        public I_MSSQL()
        {
            cfg = new INI(AppDomain.CurrentDomain.SetupInformation.ApplicationBase + "config\\sql.ini");
            err = new LOG();
            _server = cfg.GetVal("Connect", "Server");
            _dataBase = cfg.GetVal("Connect", "Database");
            _user = cfg.GetVal("Connect", "User");
            _password = cfg.GetVal("Connect", "Password");
            StringBuilder cmd = new StringBuilder(string.Format("Data Source={0};Initial Catalog={1};User ID={2};Password={3}", _server, _dataBase, _user, _password));
            _concmd = cmd.ToString();

            _tblName = cfg.GetVal("Input", "Table");
            _tagField = cfg.GetVal("Input", "tag");
            _valField = cfg.GetVal("Input", "val");
            _dtmField = cfg.GetVal("Input", "dtm");
            cmd = new StringBuilder(string.Format("SELECT {0},{1},{2} FROM {3} ORDER BY {2} DESC", _tagField, _valField, _dtmField, _tblName));
            _rtcmd = cmd.ToString();
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
                err.WrtMsg(ex.Message);
                return false;
            }
        }

        public void DisConnect()
        {
            _con.Close();
            _connected = false;
        }

        public void InitPt(int nNum, numInf[] nrst, int sNum, strInf[] srst)
        {
        }

        public bool GetRtValue(int nNum, numInf[] nrst, int sNum, strInf[] srst)
        {
            try
            {
                using (SqlCommand cmd = new SqlCommand(_rtcmd, _con))
                using (SqlDataAdapter dad = new SqlDataAdapter(cmd))
                using (DataTable dtbl = new DataTable())
                {
                    dad.Fill(dtbl);
                    using (DataView dvw = new DataView(dtbl, "", "T_TAG", DataViewRowState.CurrentRows))
                    {
                        for (int i = 0; i < nNum; ++i)
                        {
                            int idx = dvw.Find(nrst[i].srcId);
                            if (-1 == idx) continue;
                            nrst[i].val = Convert.ToSingle(dvw[idx][1]);
                            nrst[i].dtm = Convert.ToDateTime(dvw[idx][2]);
                        }
                        for (int i = 0; i < sNum; ++i)
                        {
                            int idx = dvw.Find(srst[i].srcId);
                            if (-1 == idx) continue;
                            srst[i].val = dvw[idx][1].ToString();
                            srst[i].dtm = Convert.ToDateTime(dvw[idx][2]);
                        }
                    }
                }
                return true;
            }
            catch (Exception ex)
            {
                err.WrtMsg(ex.Message);
                return false;
            }
        }

        #endregion
    }
}
