using Object = Java.Lang.Object;

namespace CibMedia.AndroidTv.Leanback.Support;

// Carries a C# record across Leanback's ObjectAdapter, which stores Java.Lang.Object.
public sealed class JavaRef<T> : Object
    where T : class
{
    public JavaRef(T value)
    {
        Value = value;
    }

    public T Value { get; }

    // Compared by payload: wrappers are rebuilt on every render and are never reference-equal,
    // and this is what lets SetItems diff a reload down to the items that changed.
    public override bool Equals(Object? obj)
    {
        return obj is JavaRef<T> other && EqualityComparer<T>.Default.Equals(Value, other.Value);
    }

    public override int GetHashCode()
    {
        return Value.GetHashCode();
    }
}

public static class JavaRef
{
    public static JavaRef<T> Wrap<T>(T value)
        where T : class
    {
        return new JavaRef<T>(value);
    }

    public static T? Unwrap<T>(Object? item)
        where T : class
    {
        return (item as JavaRef<T>)?.Value;
    }
}
