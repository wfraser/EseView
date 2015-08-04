using Microsoft.Isam.Esent.Interop;
using Microsoft.Isam.Esent.Interop.Vista;
using System;
using System.Collections.Generic;
using System.IO;
using Esent = Microsoft.Isam.Esent.Interop.Api;

namespace EseView
{
    public class DBReader : IDisposable
    {
        private Instance m_jetInstance;
        private Session m_sesid;
        private JET_DBID m_dbid;
        private string m_filename;
        private Dictionary<string, IEnumerable<ColSpec>> m_tableDefs;
        private Dictionary<Tuple<string, string>, IEnumerable<ColSpec>> m_indexDefs;

        struct ColSpec
        {
            public string Name;
            public Type Type;
            public DBColumnRetriever Retriever;
            public JET_COLUMNID ColumnId;
        }

        public DBReader(string filename)
        {
            m_filename = filename;
            m_tableDefs = new Dictionary<string, IEnumerable<ColSpec>>();
            m_indexDefs = new Dictionary<Tuple<string, string>, IEnumerable<ColSpec>>();
            m_dbid = JET_DBID.Nil;
        }

        public void Init(bool recoveryEnabled)
        {
            int pageSize;
            Esent.JetGetDatabaseFileInfo(m_filename, out pageSize, JET_DbInfo.PageSize);

            string dir = Path.GetDirectoryName(m_filename) + Path.DirectorySeparatorChar;
            Esent.JetSetSystemParameter(JET_INSTANCE.Nil, JET_SESID.Nil, JET_param.DatabasePageSize, pageSize, null);
            Esent.JetSetSystemParameter(JET_INSTANCE.Nil, JET_SESID.Nil, JET_param.LogFilePath, 0, dir);
            Esent.JetSetSystemParameter(JET_INSTANCE.Nil, JET_SESID.Nil, JET_param.SystemPath, 0, dir);

            // Put the temp DB in our working directory.
            Esent.JetSetSystemParameter(JET_INSTANCE.Nil, JET_SESID.Nil, JET_param.TempPath, 0, Directory.GetCurrentDirectory() + Path.DirectorySeparatorChar);

            // Set recovery option.
            Esent.JetSetSystemParameter(JET_INSTANCE.Nil, JET_SESID.Nil, JET_param.Recovery, 0, recoveryEnabled ? "On" : "Off");

            m_jetInstance = new Instance("ESEVIEW");
            m_jetInstance.Init();

            m_sesid = new Session(m_jetInstance);

            Esent.JetAttachDatabase(m_sesid, m_filename, recoveryEnabled ? AttachDatabaseGrbit.None : AttachDatabaseGrbit.ReadOnly);
            Esent.JetOpenDatabase(m_sesid, m_filename, null, out m_dbid, OpenDatabaseGrbit.ReadOnly);
        }

        ~DBReader()
        {
            Close();
        }

        public void Dispose()
        {
            Close();
        }

        public void Close()
        {
            if ((m_sesid != null) && (m_sesid.JetSesid != JET_SESID.Nil))
            {
                if (!m_dbid.Equals(JET_DBID.Nil))
                {
                    Esent.JetCloseDatabase(m_sesid, m_dbid, CloseDatabaseGrbit.None);
                }
                m_sesid.End();
            }

            if ((m_jetInstance != null) && !m_jetInstance.Equals(JET_INSTANCE.Nil))
            {
                m_jetInstance.Close();
            }
        }

        public IEnumerable<string> Tables
        {
            get
            {
                return Esent.GetTableNames(m_sesid, m_dbid);
            }
        }

        public int GetRowCount(string tableName, string indexName = null)
        {
            if (string.IsNullOrEmpty(tableName))
                return 0;

            using (var table = new Table(m_sesid, m_dbid, tableName, OpenTableGrbit.ReadOnly))
            {
                if (!string.IsNullOrEmpty(indexName))
                {
                    // This is needed because an index can be over a nullable column and exclude
                    // the nulls, resulting in fewer records than when not using the index.
                    Esent.JetSetCurrentIndex2(m_sesid, table, indexName, SetCurrentIndexGrbit.MoveFirst);
                }

                int numRecords;
                Esent.JetIndexRecordCount(m_sesid, table, out numRecords, 0);
                return numRecords;
            }
        }

        private void LoadTableDef(string tableName)
        {
            using (var table = new Table(m_sesid, m_dbid, tableName, OpenTableGrbit.ReadOnly))
            {
                m_tableDefs[tableName] = GetTableDef(table);
            }
        }

        private List<ColSpec> GetTableDef(JET_TABLEID table)
        {
            var columns = new List<ColumnInfo>(Esent.GetTableColumns(m_sesid, table));
            var tableDef = new List<ColSpec>();

            foreach (var column in columns)
            {
                var colspec = new ColSpec();
                colspec.Name = column.Name;
                colspec.ColumnId = column.Columnid;
                colspec.Retriever = null;

                switch (column.Coltyp)
                {
                    case JET_coltyp.Text:
                    case JET_coltyp.LongText:
                        colspec.Type = typeof(string);
                        break;
                    case JET_coltyp.Long:
                        colspec.Type = typeof(int?);
                        break;
                    case JET_coltyp.Short:
                        colspec.Type = typeof(short?);
                        break;
                    case JET_coltyp.UnsignedByte:
                        colspec.Type = typeof(byte?);
                        break;
                    case JET_coltyp.Bit:
                        colspec.Type = typeof(bool?);
                        break;
                    case JET_coltyp.DateTime:
                        colspec.Type = typeof(DateTime?);
                        break;
                    case JET_coltyp.IEEEDouble:
                        colspec.Type = typeof(double?);
                        break;
                    case JET_coltyp.IEEESingle:
                        colspec.Type = typeof(float?);
                        break;
                    case JET_coltyp.Binary:
                    case JET_coltyp.LongBinary:
                        colspec.Retriever = (s, t, c) =>
                            {
                                byte[] value = Esent.RetrieveColumn(s, t, c);
                                if (value != null)
                                    return Convert.ToBase64String(value);
                                else
                                    return value;
                            };
                        colspec.Type = typeof(string);
                        break;
                    case VistaColtyp.UnsignedLong:
                        colspec.Type = typeof(UInt32?);
                        break;
                    case VistaColtyp.LongLong:
                        colspec.Type = typeof(Int64?);
                        break;
                    case VistaColtyp.GUID:
                        colspec.Type = typeof(Guid?);
                        break;
                    case VistaColtyp.UnsignedShort:
                        colspec.Type = typeof(UInt16?);
                        break;
                    default:
                        colspec.Retriever = (s, t, c) => "ERROR: unhandled type " + Enum.GetName(typeof(JET_coltyp), column.Coltyp) + "(" + (int)column.Coltyp + ")";
                        colspec.Type = typeof(string);
                        break;
                }

                if (colspec.Retriever == null)
                {
                    colspec.Retriever = ColumnRetrievers.ForType(colspec.Type);
                }

                tableDef.Add(colspec);
            }

            return tableDef;
        }

        public IEnumerable<KeyValuePair<string, Type>> GetColumnNamesAndTypes(string tableName)
        {
            if (string.IsNullOrEmpty(tableName))
            {
                yield break;
            }

            if (!m_tableDefs.ContainsKey(tableName))
            {
                LoadTableDef(tableName);
            }

            foreach (var colspec in m_tableDefs[tableName])
            {
                yield return new KeyValuePair<string, Type>(colspec.Name, colspec.Type);
            }
        }

        public IEnumerable<List<object>> GetRows(string tableName, string indexName = null, int startRow = 0, int rowCount = -1)
        {
            if (!m_tableDefs.ContainsKey(tableName))
                LoadTableDef(tableName);

            using (var table = new Table(m_sesid, m_dbid, tableName, OpenTableGrbit.ReadOnly))
            {
                var colspec = new List<KeyValuePair<string, Type>>();

                try
                {
                    Esent.JetSetCurrentIndex2(m_sesid, table, indexName, SetCurrentIndexGrbit.MoveFirst);

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

                    foreach (var column in m_tableDefs[tableName])
                    {
                        value = column.Retriever(m_sesid, table, column.ColumnId);
                        values.Add(value);
                    }

                    yield return values;

                    rowNumber++;
                    if (rowNumber == rowCount)
                        yield break;
                }
                while (Esent.TryMoveNext(m_sesid, table));
            }
        } // GetRows

        public IEnumerable<string> GetIndexes(string tableName)
        {
            foreach (IndexInfo info in Esent.GetTableIndexes(m_sesid, m_dbid, tableName))
            {
                yield return info.Name;
            }
        }

        private IEnumerable<ColSpec> GetIndexTableDef(string tableName, string indexName)
        {
            JET_INDEXLIST indexList;
            Esent.JetGetIndexInfo(m_sesid, m_dbid, tableName, indexName, out indexList, JET_IdxInfo.List);

            // Unfortunately, Esent.GetTableColumns doesn't work on the temporary table returned by
            // JetGetIndexInfo, but the JET_INDEXLIST and the documentation have all the
            // information we need.
            var tableDef = new ColSpec[] {
                new ColSpec
                {
                    Name = "Columns",
                    Type = typeof(Int32?),
                    ColumnId = indexList.columnidcColumn
                },
                new ColSpec
                {
                    Name = "Entries",
                    Type = typeof(Int32?),
                    ColumnId = indexList.columnidcEntry
                },
                new ColSpec
                {
                    Name = "UniqueKeys",
                    Type = typeof(Int32?),
                    ColumnId = indexList.columnidcKey
                },
                new ColSpec {
                    Name = "Coltyp",
                    Type = typeof(Int32?),
                    ColumnId = indexList.columnidcoltyp
                },
                new ColSpec {
                    Name = "ColumnId",
                    Type = typeof(Int32?),
                    ColumnId = indexList.columnidcolumnid
                },
                new ColSpec {
                    Name = "ColumnName",
                    Type = typeof(string),
                    ColumnId = indexList.columnidcolumnname
                },
                new ColSpec {
                    Name = "CodePage",
                    Type = typeof(Int16?),
                    ColumnId = indexList.columnidCp
                },
                new ColSpec {
                    Name = "NumPages",
                    Type = typeof(Int32?),
                    ColumnId = indexList.columnidcPage
                },
                new ColSpec {
                    Name = "grbitColumn",
                    Type = typeof(Int32?),
                    ColumnId = indexList.columnidgrbitColumn
                },
                new ColSpec {
                    Name = "grbitIndex",
                    Type = typeof(Int32?),
                    ColumnId = indexList.columnidgrbitIndex
                },
                new ColSpec {
                    Name = "iColumn",
                    Type = typeof(Int32?),
                    ColumnId = indexList.columnidiColumn
                },
                new ColSpec {
                    Name = "IndexName",
                    Type = typeof(string),
                    ColumnId = indexList.columnidindexname
                },
                new ColSpec {
                    Name = "LangId",
                    Type = typeof(Int16?),
                    ColumnId = indexList.columnidLangid
                },
                new ColSpec {
                    Name = "LCMapFlags",
                    Type = typeof(Int32?),
                    ColumnId = indexList.columnidLCMapFlags
                }
            };

            for (int i = 0, n = tableDef.Length; i < n; i++)
            {
                tableDef[i].Retriever = ColumnRetrievers.ForType(tableDef[i].Type);
            }

            Esent.JetCloseTable(m_sesid, indexList.tableid);

            return tableDef;
        }

        public IEnumerable<KeyValuePair<string, Type>> GetIndexColumnNamesAndTypes(string tableName, string indexName)
        {
            var key = new Tuple<string, string>(tableName, indexName);

            if (!m_indexDefs.ContainsKey(new Tuple<string, string>(tableName, indexName)))
            {
                IEnumerable<ColSpec> tableDef = GetIndexTableDef(tableName, indexName);
                m_indexDefs.Add(key, tableDef);
            }

            foreach (ColSpec column in m_indexDefs[key])
            {
                yield return new KeyValuePair<string, Type>(column.Name, column.Type);
            }
        }

        public IEnumerable<DBRow> GetIndexInfo(string tableName, string indexName)
        {
            IEnumerable<ColSpec> tableDef = GetIndexTableDef(tableName, indexName);

            JET_INDEXLIST indexList;
            Esent.JetGetIndexInfo(m_sesid, m_dbid, tableName, indexName, out indexList, JET_IdxInfo.List);

            var columnsByName = new Dictionary<string, int>();
            int i = 0;
            foreach (ColSpec column in tableDef)
            {
                columnsByName.Add(column.Name, i++);
            }

            Esent.JetMove(m_sesid, indexList.tableid, JET_Move.First, MoveGrbit.None);

            i = 0;
            do
            {
                var row = new List<object>();

                foreach (ColSpec column in tableDef)
                {
                    row.Add(column.Retriever(m_sesid, indexList.tableid, column.ColumnId));
                }

                yield return new DBRow(columnsByName, row, i++);
            }
            while (Esent.TryMoveNext(m_sesid, indexList.tableid));

            Esent.JetCloseTable(m_sesid, indexList.tableid);
        }
    }
}
