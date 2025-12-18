using System;
using System.Collections.Generic;
using System.ComponentModel;

namespace TRA_Lib
{
    /// <summary>
    /// BindingList mit Sortierunterstützung (PropertyDescriptor, ListSortDirection).
    /// Kann mit einer vorhandenen IList<T> initialisiert werden (die Referenz wird verwendet).
    /// </summary>
    public class SortableBindingList<T> : BindingList<T>
    {
        bool isSortedCore;
        ListSortDirection sortDirectionCore = ListSortDirection.Ascending;
        PropertyDescriptor sortPropertyCore;

        public SortableBindingList() : base() { }

        public SortableBindingList(IList<T> list) : base(list) { }

        protected override bool SupportsSortingCore => true;
        protected override bool IsSortedCore => isSortedCore;
        protected override ListSortDirection SortDirectionCore => sortDirectionCore;
        protected override PropertyDescriptor SortPropertyCore => sortPropertyCore;

        protected override void ApplySortCore(PropertyDescriptor prop, ListSortDirection direction)
        {
            if (prop == null) return;

            sortPropertyCore = prop;
            sortDirectionCore = direction;

            var itemsList = Items as List<T>;
            if (itemsList == null)
            {
                // fallback: copy to list, sort, then replace items
                itemsList = new List<T>(Items);
            }

            itemsList.Sort(Compare);

            // If the underlying Items is not a List<T>, replace items by clearing and re-adding
            if (!ReferenceEquals(Items, itemsList))
            {
                RaiseListChangedEvents = false;
                try
                {
                    Items.Clear();
                    foreach (var item in itemsList) Items.Add(item);
                }
                finally
                {
                    RaiseListChangedEvents = true;
                }
            }

            isSortedCore = true;
            OnListChanged(new ListChangedEventArgs(ListChangedType.Reset, -1));
        }

        protected override void RemoveSortCore()
        {
            isSortedCore = false;
            sortPropertyCore = null;
            OnListChanged(new ListChangedEventArgs(ListChangedType.Reset, -1));
        }

        int Compare(T a, T b)
        {
            object aValue = sortPropertyCore.GetValue(a);
            object bValue = sortPropertyCore.GetValue(b);

            if (aValue == null && bValue == null) return 0;
            if (aValue == null) return sortDirectionCore == ListSortDirection.Ascending ? -1 : 1;
            if (bValue == null) return sortDirectionCore == ListSortDirection.Ascending ? 1 : -1;

            if (aValue is IComparable ac && bValue is object)
            {
                int cmp = ac.CompareTo(bValue);
                return sortDirectionCore == ListSortDirection.Ascending ? cmp : -cmp;
            }

            // fallback to string compare
            int s = StringComparer.Ordinal.Compare(aValue.ToString(), bValue.ToString());
            return sortDirectionCore == ListSortDirection.Ascending ? s : -s;
        }

        // Optional: enable searching
        protected override bool SupportsSearchingCore => false;
    }
}