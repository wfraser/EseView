using System;
using System.Collections;
using System.Collections.Generic;

namespace EseView
{
    public class VirtualizedReadOnlyList<T> : IList<T>, IList
    {
        public VirtualizedReadOnlyList(IVirtualizedProvider<T> provider)
        {
            m_provider = provider;
            m_pageMap = new Dictionary<int, Tuple<LinkedListNode<int>, List<T>>>();
            m_pageList = new LinkedList<int>();
        }

        public int Count
        {
            get { return m_provider.Count; }
        }

        public T this[int index]
        {
            get
            {
                int pageIndex = index / PageSize;
                int pageOffset = index % PageSize;

                if (m_pageMap.ContainsKey(pageIndex))
                {
                    LinkedListNode<int> node = m_pageMap[pageIndex].Item1;
                    m_pageList.Remove(node);
                    m_pageList.AddFirst(node);

                    return m_pageMap[pageIndex].Item2[pageOffset];
                }

                if (m_pageMap.Count == MaxPagesCached)
                {
                    // Evict a cached page.
                    LinkedListNode<int> node = m_pageList.Last;
                    m_pageList.Remove(node);
                    m_pageMap.Remove(node.Value);
                }

                {
                    List<T> page = new List<T>(m_provider.FetchRange(pageIndex * PageSize, PageSize));
                    LinkedListNode<int> node = m_pageList.AddFirst(pageIndex);
                    m_pageMap.Add(pageIndex, new Tuple<LinkedListNode<int>, List<T>>(node, page));
                    return page[pageOffset];
                }
            }
            set { throw new NotSupportedException(); }
        }

        #region Unsupported IList<T>, IList, ICollection<T>, ICollection methods

        object IList.this[int index]
        {
            get { return this[index]; }
            set { throw new NotSupportedException(); }
        }

        public int IndexOf(T value)
        {
            // Hack alert: special case for DBRow, which knows its own index.
            if (typeof(T) == typeof(DBRow))
                return (value as DBRow).RowIndex;

            // Otherwise, this is expensive to compute. Don't bother.
            return -1;
        }

        int IList.IndexOf(object value)
        {
            return IndexOf((T)value);
        }

        public void Insert(int index, T value)
        {
            throw new NotSupportedException();
        }

        void IList.Insert(int index, object value)
        {
            Insert(index, (T)value);
        }

        public void RemoveAt(int index)
        {
            throw new NotSupportedException();
        }

        public void Add(T value)
        {
            throw new NotSupportedException();
        }

        int IList.Add(object value)
        {
            throw new NotSupportedException();
        }

        public void Clear()
        {
            throw new NotSupportedException();
        }

        public bool Contains(T value)
        {
            return false;
        }

        bool IList.Contains(object value)
        {
            return Contains((T)value);
        }

        public bool Remove(T value)
        {
            throw new NotSupportedException();
        }

        void IList.Remove(object value)
        {
            Remove((T)value);
        }

        public void CopyTo(T[] array, int arrayIndex)
        {
            throw new NotSupportedException();
        }

        void ICollection.CopyTo(Array array, int index)
        {
            throw new NotSupportedException();
        }

        #endregion

        #region Misc Properties

        public bool IsReadOnly
        {
            get { return true; }
        }

        public bool IsFixedSize
        {
            get { return true; }
        }

        public bool IsSynchronized
        {
            get { return false; }
        }

        public object SyncRoot
        {
            get { return this; }
        }

        #endregion

        #region IEnumerable<T>, IEnumerable

        public IEnumerator<T> GetEnumerator()
        {
            for (int i = 0, n = Count; i < n; i++)
            {
                yield return this[i];
            }
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }

        #endregion

        private IVirtualizedProvider<T> m_provider;
        private Dictionary<int, Tuple<LinkedListNode<int>, List<T>>> m_pageMap;
        private LinkedList<int> m_pageList;

        public const int PageSize = 100;
        private const int MaxPagesCached = 10;
    }
}
