using AndroidX.Media3.DataSource;

namespace CibMedia.AndroidTv.Playback;

public sealed class PngWrappedDataSourceFactory(IDataSourceFactory inner) : Java.Lang.Object, IDataSourceFactory
{
    public IDataSource? CreateDataSource()
    {
        return new PngWrappedDataSource(inner.CreateDataSource()!);
    }
}
