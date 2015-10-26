using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml;
using System.Xml.Serialization;

namespace EseView
{
    [XmlRoot(Namespace=XmlDump.Namespace)]
    public class XmlDump
    {
        public const string Namespace = "http://www.github.com/wfraser/EseView/XmlDump/1/0";

        public XmlDump()
        {
        }

        public void AddTable(DBReader database, string tableName)
        {
            string indexName = null;
            int slashIndex = tableName.IndexOf('/');
            if (slashIndex != -1)
            {
                indexName = tableName.Substring(slashIndex + 1);
                tableName = tableName.Substring(0, slashIndex);
            }

            var tableXml = new Table();
            tableXml.Rows = new List<Row>();

            tableXml.Name = tableName;
            tableXml.RowCount = database.GetRowCount(tableName, indexName);

            var columns = database.GetColumnNamesAndTypes(tableName).ToList();
            foreach (var row in database.GetRows(tableName, indexName))
            {
                var rowXml = new Row();
                rowXml.Columns = new List<Column>();

                for (int i = 0; i < columns.Count; i++)
                {
                    var columnXml = new Column();

                    columnXml.Name = columns[i].Key;

                    if (row[i] != null)
                    {
                        columnXml.Value = row[i].ToString();

                        // Strip off trailing embedded nulls.
                        // These are caused by applications that insert C-strings into the database with their null terminator included.
                        if (columnXml.Value.Last() == '\0')
                        {
                            columnXml.Value = columnXml.Value.Substring(0, columnXml.Value.Length - 1);
                        }
                    }

                    rowXml.Columns.Add(columnXml);
                }

                tableXml.Rows.Add(rowXml);
            }

            if (Tables == null)
                Tables = new List<Table>();

            Tables.Add(tableXml);
        }

        public void Serialize(Stream stream)
        {
            var settings = new XmlWriterSettings();
            settings.Indent = true;

            // Strings can contain binary data, so escape all the things.
            settings.NewLineHandling = NewLineHandling.Entitize;

            // Without this, things like embedded nulls cause an exception. With this, it entitizes them.
            settings.CheckCharacters = false;

            using (var writer = XmlWriter.Create(stream, settings))
            {
                var serializer = new XmlSerializer(typeof(XmlDump), Namespace);
                serializer.Serialize(writer, this);
            }
        }

        public static XmlDump Deserialize(Stream stream)
        {
            var settings = new XmlReaderSettings();

            // Don't choke on "&#x0;" caused by embedded nulls.
            settings.CheckCharacters = false;

            using (var reader = XmlReader.Create(stream, settings))
            {
                var serializer = new XmlSerializer(typeof(XmlDump));
                var dump = (XmlDump)serializer.Deserialize(reader);
                return dump;
            }
        }

        [XmlElement("Table")]
        public List<Table> Tables
        {
            get;
            set;
        }
        
        public class Table
        {
            [XmlAttribute]
            public string Name
            {
                get;
                set;
            }

            [XmlAttribute]
            public int RowCount
            {
                get;
                set;
            }

            [XmlElement("Row")]
            public List<Row> Rows
            {
                get;
                set;
            }
        }

        public class Row
        {
            [XmlElement("Column")]
            public List<Column> Columns
            {
                get;
                set;
            }
        }

        public class Column
        {
            [XmlAttribute]
            public string Name
            {
                get;
                set;
            }

            [XmlAttribute]
            public string Value
            {
                get;
                set;
            }

            [XmlIgnore]
            public bool ValueSpecified
            {
                get { return (Value != null); }
            }
        }
    }
}
