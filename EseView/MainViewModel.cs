using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace EseView
{
    class MainViewModel
    {
        public MainViewModel()
        {
            m_db = null;
            m_tables = new Lazy<List<string>>();
            m_indexes = new Dictionary<string, List<string>>();
        }

        public Task OpenDatabaseAsync(string filename)
        {
            return Task.Run(() =>
            {
                if (m_db != null)
                {
                    m_db.Close();
                }

                m_db = new DBReader(filename);
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
                    columnIndexByName.Add(col.Key, i);
                }

                foreach (List<object> row in m_db.GetRows(tableName, indexName))
                {
                    yield return new DBRow(columnIndexByName, row);
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

        private DBReader m_db;
        private Lazy<List<string>> m_tables;
        private Dictionary<string, List<string>> m_indexes;
    }
}
