using System.Diagnostics.CodeAnalysis;
using AndroidX.Leanback.Widget;
using CibMedia.Core.Common;
using Object = Java.Lang.Object;

namespace CibMedia.AndroidTv.Leanback.Support;

// One rail: a Leanback ListRow plus what turns a Load<T> snapshot into its contents. A row
// that resolved to nothing reports itself empty so the screen can drop it from the adapter.
[SuppressMessage(
    "Design",
    "CA1001:Types that own disposable fields should be disposable",
    Justification = "The adapter and diff callback are Java peers held by the ListRow handed "
                    + "to Leanback. Their lifetime belongs to the Java side, and disposing them from here "
                    + "would tear down objects the row still references.")]
public sealed class CardRow<TItem>
    where TItem : class
{
    private readonly List<TItem> _current = [];
    private readonly JavaRefDiff<TItem> _diff;
    private readonly long _id;
    private readonly ArrayObjectAdapter _items;
    private string _title;

    public CardRow(long id, string title, Presenter presenter, Func<TItem, object> identity)
    {
        _id = id;
        _title = title;
        _items = new ArrayObjectAdapter(presenter);
        _diff = new JavaRefDiff<TItem>(identity);
        Row = new ListRow(new HeaderItem(id, title), _items);
    }

    public ListRow Row { get; private set; }

    public bool HasItems { get; private set; }

    // A HeaderItem's name is fixed at construction, so a retitled rail is a new Row over the
    // same items adapter; the caller swaps it into its adapter when this reports true.
    public bool Retitle(string title)
    {
        if (_title == title) return false;

        _title = title;
        Row = new ListRow(new HeaderItem(_id, title), _items);

        return true;
    }

    // The common case is the same list with more on the end, and only the tail is appended:
    // resubmitting wraps every card in a new Java peer, which makes paging quadratic and
    // relies on the diff to put the focused card back.
    public void Set(Load<IReadOnlyList<TItem>> load)
    {
        var items = load is Load<IReadOnlyList<TItem>>.Ready ready ? ready.Value : [];

        HasItems = items.Count > 0;

        if (_current.Count == _items.Size() && StartsWithCurrent(items))
        {
            if (items.Count == _current.Count) return;

            // object, not Java.Lang.Object: SetItems and AddAll are bound against different
            // collection element types.
            var added = new List<object>(items.Count - _current.Count);

            for (var i = _current.Count; i < items.Count; i++)
            {
                added.Add(JavaRef.Wrap(items[i]));
                _current.Add(items[i]);
            }

            _items.AddAll(_items.Size(), added);
            return;
        }

        _items.SetItems(new List<Object>(items.Select(JavaRef.Wrap)), _diff);

        _current.Clear();
        _current.AddRange(items);
    }

    // Reference equality: a feed's snapshot and a details record's collection both hand back
    // the very same instances. Anything else falls through to the diff.
    private bool StartsWithCurrent(IReadOnlyList<TItem> items)
    {
        if (items.Count < _current.Count) return false;

        for (var i = 0; i < _current.Count; i++)
            if (!ReferenceEquals(_current[i], items[i]))
                return false;

        return true;
    }

    // For state that lives outside the row's own list, such as which season is selected.
    public void Rebind()
    {
        _items.NotifyArrayItemRangeChanged(0, _items.Size());
    }
}
