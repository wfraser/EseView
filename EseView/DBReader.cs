using Microsoft.Isam.Esent.Interop;
using Microsoft.Isam.Esent.Interop.Vista;
using System;
using System.Collections.Generic;
using Esent = Microsoft.Isam.Esent.Interop.Api;

namespace EseView
{
    public class DBReader
    {
        private Instance m_jetInstance;
        private Session m_sesid;
        private JET_DBID m_dbid;
        private string m_filename;
        private Dictionary<string, List<ColSpec>> m_tableDefs;

        struct ColSpec
        {
            public string Name;
            public Type Type;
            public Func<JET_SESID, JET_TABLEID, JET_COLUMNID, object> Fetch;
        }

        public DBReader(string filename)
        {
            m_filename = filename;
            m_tableDefs = new Dictionary<string, List<ColSpec>>();

            int pageSize;
            Esent.JetGetDatabaseFileInfo(filename, out pageSize, JET_DbInfo.PageSize);

            string dir = System.IO.Path.GetDirectoryName(filename) + "\\";

            Esent.JetSetSystemParameter(JET_INSTANCE.Nil, JET_SESID.Nil, JET_param.DatabasePageSize, pageSize, null);
            Esent.JetSetSystemParameter(JET_INSTANCE.Nil, JET_SESID.Nil, JET_param.LogFilePath, 0, dir);
            Esent.JetSetSystemParameter(JET_INSTANCE.Nil, JET_SESID.Nil, JET_param.SystemPath, 0, dir);

            m_jetInstance = new Instance("ESEVIEW");
            m_jetInstance.Init();

            m_sesid = new Session(m_jetInstance);

            Esent.JetAttachDatabase(m_sesid, filename, AttachDatabaseGrbit.ReadOnly);
            Esent.JetOpenDatabase(m_sesid, filename, null, out m_dbid, OpenDatabaseGrbit.ReadOnly);
        }

        public void Close()
        {
            Esent.JetCloseDatabase(m_sesid, m_dbid, CloseDatabaseGrbit.None);
            m_sesid.End();
            m_jetInstance.Close();
        }

        public IEnumerable<string> Tables
        {
            get
            {
                return Esent.GetTableNames(m_sesid, m_dbid);
            }
        }

        public int GetRowCount(string tableName)
        {
            using (var table = new Table(m_sesid, m_dbid, tableName, OpenTableGrbit.ReadOnly))
            {
                int numRecords;
                Esent.JetIndexRecordCount(m_sesid, table, out numRecords, 0);
                return numRecords;
            }
        }

        private void LoadTableDef(string tableName)
        {
            using (var table = new Table(m_sesid, m_dbid, tableName, OpenTableGrbit.ReadOnly))
            {
                var columns = new List<ColumnInfo>(Esent.GetTableColumns(m_sesid, table));
                var tableDef = new List<ColSpec>();

                foreach (var column in columns)
                {
                    ColSpec colspec;
                    colspec.Name = column.Name;

                    switch (column.Coltyp)
                    {
                        case JET_coltyp.Text:
                        case JET_coltyp.LongText:
                            colspec.Fetch = Esent.RetrieveColumnAsString;
                            colspec.Type = typeof(string);
                            break;
                        case JET_coltyp.Long:
                            colspec.Fetch = (s, t, c) => Esent.RetrieveColumnAsInt32(s, t, c);
                            colspec.Type = typeof(int?);
                            break;
                        case JET_coltyp.Short:
                            colspec.Fetch = (s, t, c) => Esent.RetrieveColumnAsInt16(s, t, c);
                            colspec.Type = typeof(short?);
                            break;
                        case JET_coltyp.UnsignedByte:
                            colspec.Fetch = (s, t, c) => Esent.RetrieveColumnAsByte(s, t, c);
                            colspec.Type = typeof(byte?);
                            break;
                        case JET_coltyp.Bit:
                            colspec.Fetch = (s, t, c) => Esent.RetrieveColumnAsBoolean(s, t, c);
                            colspec.Type = typeof(bool?);
                            break;
                        case JET_coltyp.DateTime:
                            colspec.Fetch = (s, t, c) => Esent.RetrieveColumnAsDateTime(s, t, c);
                            colspec.Type = typeof(DateTime?);
                            break;
                        case JET_coltyp.IEEEDouble:
                            colspec.Fetch = (s, t, c) => Esent.RetrieveColumnAsDouble(s, t, c);
                            colspec.Type = typeof(double?);
                            break;
                        case JET_coltyp.IEEESingle:
                            colspec.Fetch = (s, t, c) => Esent.RetrieveColumnAsFloat(s, t, c);
                            colspec.Type = typeof(float?);
                            break;
                        case JET_coltyp.Binary:
                        case JET_coltyp.LongBinary:
                            colspec.Fetch = (s, t, c) =>
                                {
                                    byte[] value = Esent.RetrieveColumn(m_sesid, table, column.Columnid);
                                    if (value != null)
                                        return Convert.ToBase64String(value);
                                    else
                                        return value;
                                };
                            colspec.Type = typeof(string);
                            break;
                        case VistaColtyp.UnsignedLong:
                            colspec.Fetch = (s, t, c) => Esent.RetrieveColumnAsUInt32(s, t, c);
                            colspec.Type = typeof(UInt32?);
                            break;
                        case VistaColtyp.LongLong:
                            colspec.Fetch = (s, t, c) => Esent.RetrieveColumnAsInt64(s, t, c);
                            colspec.Type = typeof(Int64?);
                            break;
                        case VistaColtyp.GUID:
                            colspec.Fetch = (s, t, c) => Esent.RetrieveColumnAsGuid(s, t, c);
                            colspec.Type = typeof(Guid?);
                            break;
                        case VistaColtyp.UnsignedShort:
                            colspec.Fetch = (s, t, c) => Esent.RetrieveColumnAsUInt16(s, t, c);
                            colspec.Type = typeof(UInt16?);
                            break;
                        default:
                            colspec.Fetch = (s, t, c) => "ERROR: unhandled type " + Enum.GetName(typeof(JET_coltyp), column.Coltyp) + "(" + (int)column.Coltyp + ")";
                            colspec.Type = typeof(string);
                            break;
                    }

                    tableDef.Add(colspec);
                }

                m_tableDefs[tableName] = tableDef;
            } // using
        }

        public IEnumerable<KeyValuePair<string, Type>> GetColumnNamesAndTypes(string tableName)
        {
            if (!m_tableDefs.ContainsKey(tableName))
            {
                LoadTableDef(tableName);
            }

            foreach (var colspec in m_tableDefs[tableName])
            {
                yield return new KeyValuePair<string, Type>(colspec.Name, colspec.Type);
            }
        }

        // TODO: allow there to be row number bounds on this, and use a virtualizing grid to do it right.
        public IEnumerable<List<object>> GetRows(string tableName, int startRow = 0, int rowCount = -1)
        {
            if (!m_tableDefs.ContainsKey(tableName))
                LoadTableDef(tableName);

            using (var table = new Table(m_sesid, m_dbid, tableName, OpenTableGrbit.ReadOnly))
            {
                var colspec = new List<KeyValuePair<string, Type>>();
                var columns = new List<ColumnInfo>(Esent.GetTableColumns(m_sesid, table));

                try
                {
                    Esent.JetMove(m_sesid, table, JET_Move.First, MoveGrbit.None);

                    if (startRow != 0)
                    {
                       Esent.JetMove(m_sesid, table, startRow, MoveGrbit.None);
                    }
                }
                catch (EsentNoCurrentRecordException)
                {
                    // Return an empty set.
                    yield break;
                }

                int rowNumber = 0;
                do
                {
                    var values = new List<object>();

                    object value = null;

                    int i = 0;
                    foreach (var column in columns)
                    {
                        value = m_tableDefs[tableName][i].Fetch(m_sesid, table, column.Columnid);
                        values.Add(value);
                        i++;
                    }

                    yield return values;

                    rowNumber++;
                    if (rowNumber == rowCount)
                        yield break;
                }
                while (Esent.TryMoveNext(m_sesid, table));
            }
        } // GetRows
    }
}
