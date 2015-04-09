using System;
using System.Collections.Generic;

namespace EseView
{
    public class DatabaseVirtualizedProvider : IVirtualizedProvider<DBRow>
    {
        public DatabaseVirtualizedProvider(DBReader db, string tableName, string indexName)
        {
            m_db = db;
            m_tableName = tableName;
            m_indexName = indexName;
            m_count = new Lazy<int>(() => db.GetRowCount(tableName, indexName));

            m_columnIndexByName = new Dictionary<string, int>();

            int i = 0;
            foreach (var col in m_db.GetColumnNamesAndTypes(tableName))
            {
                m_columnIndexByName.Add(col.Key, i);
                i++;
            }
        }

        public int Count
        {
            get { return m_count.Value; }
        }

        public IEnumerable<DBRow> FetchRange(int startIndex, int count)
        {
            int i = 0;
            foreach (List<object> row in m_db.GetRows(m_tableName, m_indexName, startIndex, count))
            {
                yield return new DBRow(m_columnIndexByName, row, startIndex + i);
                i++;
            }
        }

        private DBReader m_db;
        private string m_tableName;
        private string m_indexName;
        private Lazy<int> m_count;
        private Dictionary<string, int> m_columnIndexByName;
    }
}
