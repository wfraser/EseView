using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Isam.Esent.Interop;
using Esent = Microsoft.Isam.Esent.Interop.Api;

namespace EseView
{
    public delegate object ColumnRetriever(JET_SESID sesid, JET_TABLEID table, JET_COLUMNID column);

    public static class ColumnRetrievers
    {
        private static Dictionary<Type, ColumnRetriever> s_fetchers = new Dictionary<Type, ColumnRetriever>
        {
            { typeof(string),   Esent.RetrieveColumnAsString },
            { typeof(long?),    (s,t,c) => Esent.RetrieveColumnAsInt64(s,t,c) },
            { typeof(ulong?),   (s,t,c) => Esent.RetrieveColumnAsUInt64(s,t,c) },
            { typeof(int?),     (s,t,c) => Esent.RetrieveColumnAsInt32(s,t,c) },
            { typeof(uint?),    (s,t,c) => Esent.RetrieveColumnAsUInt32(s,t,c) },
            { typeof(short?),   (s,t,c) => Esent.RetrieveColumnAsInt16(s,t,c) },
            { typeof(ushort?),  (s,t,c) => Esent.RetrieveColumnAsUInt16(s,t,c) },
            { typeof(byte?),    (s,t,c) => Esent.RetrieveColumnAsByte(s,t,c) },
            { typeof(bool?),    (s,t,c) => Esent.RetrieveColumnAsBoolean(s,t,c) },
            { typeof(double?),  (s,t,c) => Esent.RetrieveColumnAsDouble(s,t,c) },
            { typeof(float?),   (s,t,c) => Esent.RetrieveColumnAsFloat(s,t,c) },

            { typeof(DateTime?), (s,t,c) => Esent.RetrieveColumnAsDateTime(s,t,c) },
            { typeof(Guid?),    (s,t,c) => Esent.RetrieveColumnAsGuid(s,t,c) },
            { typeof(byte[]),   Esent.RetrieveColumn },
           
        };
        
        public static ColumnRetriever ForType(Type t)
        {
            if (!s_fetchers.ContainsKey(t))
            {
                return (x, y, z) => "Error: unhandled type.";
            }
            
            return s_fetchers[t];
        }

        public static T Retrieve<T>(JET_SESID sesid, JET_TABLEID table, JET_COLUMNID column)
        {
            return (T)ForType(typeof(T))(sesid, table, column);
        }
    }
}
