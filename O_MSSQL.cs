using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Data;
using System.Data.SqlClient;

namespace SISCell
{
    class O_MSSQL : oProtocol
    {
        private INI cfg;
        private LOG err;
        private string _server, _dataBase, _user, _password, _concmd;
        private SqlConnection _con;
        private string _tblName, _tagField, _valField, _dtmField;
        private bool _connected = false;

        public O_MSSQL()
        {
            cfg = new INI(AppDomain.CurrentDomain.SetupInformation.ApplicationBase + "config\\sql.ini");
            err = new LOG();
            _server = cfg.GetVal("Connect", "Server");
            _dataBase = cfg.GetVal("Connect", "Database");
            _user = cfg.GetVal("Connect", "User");
            _password = cfg.GetVal("Connect", "Password");
            StringBuilder cmd = new StringBuilder(string.Format("Data Source={0};Initial Catalog={1};User ID={2};Password={3}", _server, _dataBase, _user, _password));
            _concmd = cmd.ToString();

            _tblName = cfg.GetVal("Output", "Table");
            _tagField = cfg.GetVal("Output", "tag");
            _valField = cfg.GetVal("Output", "val");
            _dtmField = cfg.GetVal("Output", "dtm");
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

        public void SetRtValue(int nNum, numInf[] nrst, int sNum, strInf[] srst)
        {
            SqlCommand cmd = new SqlCommand(string.Format("TRUNCATE TABLE {0}", _tblName), _con);
            try
            {
                cmd.ExecuteNonQuery();
            }
            catch (Exception ex)
            {
                err.WrtMsg(ex.Message);
                _connected = false;
            }

            using (DataTable dtbl = new DataTable())
            {
                dtbl.Columns.Add("tag", typeof(string));
                dtbl.Columns.Add("val", typeof(string));
                dtbl.Columns.Add("dtm", typeof(DateTime));
                DataRow drow;
                foreach (numInf nr in nrst)
                {
                    drow = dtbl.NewRow();
                    drow["tag"] = nr.dstId;
                    drow["val"] = nr.val.ToString();
                    drow["dtm"] = nr.dtm;
                    dtbl.Rows.Add(drow);
                }

                foreach (strInf sr in srst)
                {
                    drow = dtbl.NewRow();
                    drow["tag"] = sr.dstId;
                    drow["val"] = sr.val;
                    drow["dtm"] = sr.dtm;
                    dtbl.Rows.Add(drow);
                }

                using (SqlBulkCopy bcp = new SqlBulkCopy(_con))
                {
                    bcp.DestinationTableName = _tblName;
                    bcp.ColumnMappings.Add("tag", _tagField);
                    bcp.ColumnMappings.Add("val", _valField);
                    bcp.ColumnMappings.Add("dtm", _dtmField);
                    try
                    {
                        bcp.WriteToServer(dtbl);
                    }
                    catch (Exception ex)
                    {
                        err.WrtMsg(ex.Message);
                        _connected = false;
                    }
                }
            }
        }

        #endregion
    }
}
