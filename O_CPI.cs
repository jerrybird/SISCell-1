using System;
using System.Data;
using System.Data.SqlClient;
using System.Text;
using System.Threading;

namespace SISCell
{
    class O_CPI : oProtocol
    {
        private INI cfg;
        private LOG err;
        private string _server, _dataBase, _user, _password, _concmd, _interface;
        private SqlConnection _con;

        private int rtNnum, rtSnum;
        private int[] rtNid, rtSid;
        private bool _connected = false;

        public O_CPI()
        {
            cfg = new INI(AppDomain.CurrentDomain.SetupInformation.ApplicationBase + "config\\cpi.ini");
            err = new LOG();
            _server = cfg.GetVal("Connect", "Server");
            _dataBase = cfg.GetVal("Connect", "Database");
            _user = cfg.GetVal("Connect", "User");
            _password = cfg.GetVal("Connect", "Password");
            _interface = cfg.GetVal("Connect", "Interface");
            StringBuilder cmd = new StringBuilder(string.Format("Data Source={0},1433;Network Library=DBMSSOCN;Initial Catalog={1};User ID={2};Password={3};", _server, _dataBase, _user, _password));
            _concmd = cmd.ToString();
        }

        #region oProtocol 成员

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
            _con.Close();
            _connected = false;
        }

        public void InitPt(int nNum, numInf[] nrst, int sNum, strInf[] srst)
        {
            SqlCommand cmd;

            cmd = new SqlCommand(string.Format("SELECT COUNT(*) FROM TAGCONFIG WHERE INTERFACE={0} AND TYPE=0 AND SENDFLAG=1", _interface), _con);
            rtNnum = (int)cmd.ExecuteScalar();
            rtNid = new int[rtNnum];

            cmd = new SqlCommand(string.Format("SELECT COUNT(*) FROM TAGCONFIG WHERE INTERFACE={0} AND TYPE=1 AND SENDFLAG=1", _interface), _con);
            rtSnum = (int)cmd.ExecuteScalar();
            rtSid = new int[rtSnum];

            cmd = new SqlCommand(string.Format("SELECT ID_KEY,TAG,DSC,SENDFLAG FROM TAGCONFIG WHERE INTERFACE={0} AND TYPE=0 ORDER BY ID_KEY", _interface), _con);
            using (SqlDataReader dtr = cmd.ExecuteReader())
            {
                int iN = 0;
                int i = 0;
                while (dtr.Read())
                {
                    nrst[i].sn = i;
                    nrst[i].srcId = dtr.GetString(1);
                    nrst[i].dstId = dtr.GetString(1);
                    nrst[i].desc = dtr.GetString(2);
                    if (1 == dtr.GetByte(3)) rtNid[iN++] = i;
                    ++i;
                }
            }

            cmd = new SqlCommand(string.Format("SELECT ID_KEY,TAG,DSC,SENDFLAG FROM TAGCONFIG WHERE INTERFACE={0} AND TYPE=1 ORDER BY ID_KEY", _interface), _con);
            using (SqlDataReader dtr = cmd.ExecuteReader())
            {
                int iS = 0;
                int i = 0;
                while (dtr.Read())
                {
                    srst[i].sn = nNum + i;
                    srst[i].srcId = dtr.GetString(1);
                    srst[i].dstId = dtr.GetString(1);
                    srst[i].desc = dtr.GetString(2);
                    if (1 == dtr.GetByte(3)) rtSid[iS++] = i;
                    ++i;
                }
            }
            cmd.Dispose();
        }

        public void SetRtValue(int nNum, numInf[] nrst, int sNum, strInf[] srst)
        {
            SqlCommand cmd = new SqlCommand(string.Format("DELETE t1 FROM REALVALUE t1 INNER JOIN TAGCONFIG t2 ON t1.TAG=t2.TAG AND t2.INTERFACE={0}", _interface), _con);
            try
            {
                cmd.ExecuteNonQuery();
            }
            catch (Exception ex)
            {
                DisConnect();
                Connect();
                err.WrtMsg(ex.Message);
            }

            using (DataTable dtbl = new DataTable())
            {
                dtbl.Columns.Add("tag", typeof(string));
                dtbl.Columns.Add("val", typeof(string));
                dtbl.Columns.Add("dtm", typeof(DateTime));

                foreach (int id in rtNid)
                {
                    DataRow drow = dtbl.NewRow();
                    drow["tag"] = nrst[id].dstId;
                    drow["val"] = nrst[id].val.ToString();
                    drow["dtm"] = nrst[id].dtm;
                    dtbl.Rows.Add(drow);
                }

                foreach (int id in rtSid)
                {
                    DataRow drow = dtbl.NewRow();
                    drow["tag"] = srst[id].dstId;
                    drow["val"] = srst[id].val;
                    drow["dtm"] = srst[id].dtm;
                    dtbl.Rows.Add(drow);
                }

                using (SqlBulkCopy bcp = new SqlBulkCopy(_con))
                {
                    bcp.DestinationTableName = "REALVALUE";
                    bcp.ColumnMappings.Add("tag", "TAG");
                    bcp.ColumnMappings.Add("val", "VALUE");
                    bcp.ColumnMappings.Add("dtm", "TIME");
                    try
                    {
                        bcp.WriteToServer(dtbl);
                    }
                    catch (Exception ex)
                    {
                        DisConnect();
                        Connect();
                        err.WrtMsg(ex.Message);
                    }
                }
            }

            if (0 == frmMain.bufMod / 2) return;

            using (DataTable dtbl = new DataTable())
            {
                dtbl.Columns.Add("tag", typeof(string));
                dtbl.Columns.Add("val", typeof(string));
                dtbl.Columns.Add("dtm", typeof(DateTime));

                foreach (numInf nr in nrst)
                {
                    DataRow drow = dtbl.NewRow();
                    drow["tag"] = nr.dstId;
                    drow["val"] = nr.val.ToString();
                    drow["dtm"] = nr.dtm;
                    dtbl.Rows.Add(drow);
                }

                foreach (strInf sr in srst)
                {
                    DataRow drow = dtbl.NewRow();
                    drow["tag"] = sr.dstId;
                    drow["val"] = sr.val;
                    drow["dtm"] = sr.dtm;
                    dtbl.Rows.Add(drow);
                }

                using (SqlBulkCopy bcp = new SqlBulkCopy(_con))
                {
                    bcp.DestinationTableName = "HISTVALUE";
                    bcp.ColumnMappings.Add("tag", "TAG");
                    bcp.ColumnMappings.Add("val", "VALUE");
                    bcp.ColumnMappings.Add("dtm", "TIME");
                    try
                    {
                        bcp.WriteToServer(dtbl);
                    }
                    catch (Exception ex)
                    {
                        DisConnect();
                        Connect();
                        err.WrtMsg(ex.Message);
                    }
                }
            }
        }

        #endregion
    }
}