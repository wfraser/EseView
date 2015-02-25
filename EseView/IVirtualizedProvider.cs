using System.Collections.Generic;

namespace EseView
{
    public interface IVirtualizedProvider<T>
    {
        int Count
        {
            get;
        }

        IEnumerable<T> FetchRange(int startIndex, int count);
    }
}
