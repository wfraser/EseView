using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace EseView
{
    public class MainViewModel
    {
        public MainViewModel()
        {
            m_db = null;
            m_tables = new Lazy<List<string>>();
            m_indexes = new Dictionary<string, List<string>>();
        }

        public Task OpenDatabaseAsync(string filename, bool recoveryEnabled = false)
        {
            return Task.Run(() =>
            {
                if (m_db != null)
                {
                    m_db.Close();
                }

                m_db = new DBReader(filename);
                m_db.Init(recoveryEnabled);
                m_tables = new Lazy<List<string>>(() => new List<string>(m_db.Tables));
            });
        }

        public List<string> Tables
        {
            get { return m_tables.Value; }
        }

        public IEnumerable<DBRow> Rows(string tableName, string indexName)
        {
            if (m_db == null)
            {
                yield break;
            }
            else
            {
                var columnIndexByName = new Dictionary<string, int>();

                int i = 0;
                foreach (var col in m_db.GetColumnNamesAndTypes(tableName))
                {
                    columnIndexByName.Add(col.Key, i++);
                }

                i = 0;
                foreach (List<object> row in m_db.GetRows(tableName, indexName))
                {
                    yield return new DBRow(columnIndexByName, row, i++);
                }
            }
        }

        public VirtualizedReadOnlyList<DBRow> VirtualRows(string tableName, string indexName)
        {
            return new VirtualizedReadOnlyList<DBRow>(new DatabaseVirtualizedProvider(m_db, tableName, indexName));
        }

        public int GetRowCount(string tableName, string indexName)
        {
            return m_db.GetRowCount(tableName, indexName);
        }

        public IEnumerable<KeyValuePair<string, Type>> GetColumnNamesAndTypes(string tableName)
        {
            if (m_db == null || string.IsNullOrEmpty(tableName))
            {
                return null;
            }
            return m_db.GetColumnNamesAndTypes(tableName);
        }

        public IEnumerable<string> GetIndexes(string tableName)
        {
            if (!m_indexes.ContainsKey(tableName))
            {
                m_indexes.Add(tableName, new List<string>(m_db.GetIndexes(tableName)));
            }

            return m_indexes[tableName];
        }

        public IEnumerable<DBRow> GetIndexInfo(string tableName, string indexName)
        {
            if (m_db == null)
            {
                return null;
            }
            return m_db.GetIndexInfo(tableName, indexName);
        }

        public IEnumerable<KeyValuePair<string, Type>> GetIndexColumnNamesAndTypes(string tableName, string indexName)
        {
            if (m_db == null)
            {
                return null;
            }
            return m_db.GetIndexColumnNamesAndTypes(tableName, indexName);
        }

        public void DumpTable(string tableName, System.IO.TextWriter output)
        {
            string indexName = null;
            int slashIndex = tableName.IndexOf('/');
            if (slashIndex != -1)
            {
                indexName = tableName.Substring(slashIndex + 1);
                tableName = tableName.Substring(0, slashIndex);
            }

            var columns = new List<KeyValuePair<string, Type>>(m_db.GetColumnNamesAndTypes(tableName));

            output.WriteLine("<Table Name=\"{0}\" RowCount=\"{1}\">", tableName, m_db.GetRowCount(tableName, indexName));

            foreach (var row in m_db.GetRows(tableName, indexName))
            {
                output.WriteLine("\t<Row>");
                for (int i = 0; i < columns.Count; i++)
                {
                    output.Write("\t\t<Column Name=\"{0}\"", columns[i].Key);
                    if (row[i] != null)
                    {
                        output.WriteLine();
                        output.WriteLine("\t\t\tValue=\"{0}\"/>", row[i].ToString());
                    }
                    else
                    {
                        output.WriteLine("/>");
                    }
                }
                output.WriteLine("\t</Row>");
            }

            output.WriteLine("</Table>");
        }

        private DBReader m_db;
        private Lazy<List<string>> m_tables;
        private Dictionary<string, List<string>> m_indexes;
    }
}
