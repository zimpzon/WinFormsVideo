using System.Drawing.Imaging;
using ReceiverLib;
using ReceiverLib.WinForms;

namespace ReceiverLib.WinForms.Tests;

public class WinFormsFrameViewTests
{
    private static WinFormsFrameView View(Func<FrameBufferPool?> pool) =>
        new(pool, (out RentedFrame f) =>
        {
            FrameBufferPool? p = pool();
            if (p is null)
            {
                f = default;
                return false;
            }

            return p.TryAcquireFrame(out f);
        });

    [Fact]
    public void TryGetBitmap_BeforeAnyFrame_ReturnsFalse()
    {
        FrameBufferPool? pool = null;
        using WinFormsFrameView view = View(() => pool);

        Assert.False(view.TryGetBitmap(out _)); // no pool yet

        pool = new FrameBufferPool(64, 48);
        Assert.False(view.TryGetBitmap(out _)); // pool, but nothing committed
    }

    [Fact]
    public void TryGetBitmap_AfterCommit_ReturnsACachedBitmapOfThePoolSize()
    {
        var pool = new FrameBufferPool(64, 48);
        using WinFormsFrameView view = View(() => pool);

        pool.Commit(sequenceNumber: 1, timestamp: TimeSpan.Zero);

        Assert.True(view.TryGetBitmap(out System.Drawing.Bitmap bitmap));
        Assert.Equal(64, bitmap.Width);
        Assert.Equal(48, bitmap.Height);
        Assert.Equal(PixelFormat.Format32bppPArgb, bitmap.PixelFormat);
    }

    [Fact]
    public void TryGetBitmap_WhenNothingNew_KeepsReturningTheSameInstance()
    {
        var pool = new FrameBufferPool(32, 32);
        using WinFormsFrameView view = View(() => pool);
        pool.Commit(1, TimeSpan.Zero);

        Assert.True(view.TryGetBitmap(out System.Drawing.Bitmap first));
        Assert.True(view.TryGetBitmap(out System.Drawing.Bitmap again)); // nothing new since
        Assert.Same(first, again);
    }

    [Fact]
    public void TryGetBitmap_ReusesOneBitmapPerPoolBuffer_NoAllocationPerFrame()
    {
        var pool = new FrameBufferPool(16, 16);
        using WinFormsFrameView view = View(() => pool);
        var seen = new HashSet<System.Drawing.Bitmap>(ReferenceEqualityComparer.Instance);

        for (int i = 0; i < 50; i++)
        {
            pool.Commit(i, TimeSpan.FromMilliseconds(i));
            Assert.True(view.TryGetBitmap(out System.Drawing.Bitmap bmp));
            seen.Add(bmp);
        }

        Assert.True(seen.Count <= pool.Count); // only the 3 cached wrappers, rotated
    }

    [Fact]
    public void TryGetBitmap_AfterAResolutionChange_RebuildsAtTheNewSize()
    {
        FrameBufferPool pool = new(64, 48);
        using WinFormsFrameView view = View(() => pool);
        pool.Commit(1, TimeSpan.Zero);
        Assert.True(view.TryGetBitmap(out System.Drawing.Bitmap small));
        Assert.Equal(64, small.Width);

        pool = new FrameBufferPool(96, 72) { Generation = 1 }; // reconnect at a new resolution
        pool.Commit(2, TimeSpan.Zero);

        Assert.True(view.TryGetBitmap(out System.Drawing.Bitmap large));
        Assert.Equal(96, large.Width);
        Assert.Equal(72, large.Height);
    }

    [Fact]
    public void Dispose_IsIdempotent_AndReleasesTheBitmaps()
    {
        var pool = new FrameBufferPool(8, 8);
        var view = View(() => pool);
        pool.Commit(1, TimeSpan.Zero);
        Assert.True(view.TryGetBitmap(out System.Drawing.Bitmap bmp));

        view.Dispose();
        view.Dispose(); // no throw

        Assert.Throws<ArgumentException>(() => _ = bmp.Width); // disposed GDI+ object
        Assert.False(view.TryGetBitmap(out _));
    }
}
