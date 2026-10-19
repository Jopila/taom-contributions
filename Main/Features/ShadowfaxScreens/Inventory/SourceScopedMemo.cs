using System;

namespace TAOM.Features.ShadowfaxScreens.Inventory;

/// <summary>
/// A one-slot memo valid for one source instance: a value stored for a source is returned only for that
/// same instance, and a new source misses so the caller recomputes. The source is held weakly, so the memo
/// never keeps a finished campaign's list alive. A stored null is a hit. It stores values only: a caller
/// that computes the value outside (and lets an exception propagate) never caches a failure.
/// </summary>
internal sealed class SourceScopedMemo<TSource, TValue> where TSource : class
{
    private WeakReference? _source;
    private TValue _value = default!;

    public bool TryGet(TSource source, out TValue value)
    {
        if (_source != null && ReferenceEquals(_source.Target, source))
        {
            value = _value;
            return true;
        }

        value = default!;
        return false;
    }

    public void Store(TSource source, TValue value)
    {
        _source = new WeakReference(source);
        _value = value;
    }
}
