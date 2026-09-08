namespace CibMedia.Core.Abstractions;

public interface IImageLoader
{
    void Load(string? url, object target, int widthPx, int heightPx);

    void Cancel(object target);
}
