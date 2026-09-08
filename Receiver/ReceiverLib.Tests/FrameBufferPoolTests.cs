using ReceiverLib;

namespace ReceiverLib.Tests;

public class FrameBufferPoolTests
{
    private static FrameBufferPool NewPool(int w = 8, int h = 4) => new(w, h);

    private static int WriteIndexOf(FrameBufferPool pool)
    {
        IntPtr scan0 = pool.CurrentWriteTarget().Scan0;
        for (int i = 0; i < pool.Count; i++)
        {
            if (pool.BufferAddress(i) == scan0)
            {
                return i;
            }
        }

        throw new InvalidOperationException("write target address matched no buffer");
    }

    [Fact]
    public void Layout_IsPackedBgra32_WithThreeDistinctStableBuffers()
    {
        FrameBufferPool pool = NewPool(64, 48);

        Assert.Equal(3, pool.Count);
        Assert.Equal(64, pool.Width);
        Assert.Equal(48, pool.Height);
        Assert.Equal(64 * 4, pool.Stride);
        Assert.Equal(FramePixelFormat.Bgra32, pool.PixelFormat);

        var addresses = new[] { pool.BufferAddress(0), pool.BufferAddress(1), pool.BufferAddress(2) };
        Assert.Equal(3, addresses.Distinct().Count());
        Assert.DoesNotContain(IntPtr.Zero, addresses);
        Assert.Equal(addresses[0], pool.BufferAddress(0)); // stable across calls
    }

    [Fact]
    public void TryAcquire_WithNothingCommitted_ReturnsFalse()
    {
        FrameBufferPool pool = NewPool();

        Assert.False(pool.TryAcquireFrame(out RentedFrame frame));
        Assert.Equal(default, frame);
    }

    [Fact]
    public void Commit_ThenAcquire_HandsBackThatFrame_ThenNothingNew()
    {
        FrameBufferPool pool = NewPool();

        pool.Commit(sequenceNumber: 42, TimeSpan.FromMilliseconds(500));

        Assert.True(pool.TryAcquireFrame(out RentedFrame frame));
        Assert.Equal(42, frame.SequenceNumber);
        Assert.Equal(TimeSpan.FromMilliseconds(500), frame.Timestamp);
        Assert.InRange(frame.BufferIndex, 0, 2);
        Assert.Equal(1, pool.PresentedCount);

        Assert.False(pool.TryAcquireFrame(out _)); // already taken, nothing newer
    }

    [Fact]
    public void Commit_TwiceWithoutAcquire_SecondSupersedesFirst()
    {
        FrameBufferPool pool = NewPool();

        pool.Commit(1, TimeSpan.Zero);
        pool.Commit(2, TimeSpan.FromSeconds(1));

        Assert.Equal(1, pool.SupersededCount);
        Assert.True(pool.TryAcquireFrame(out RentedFrame frame));
        Assert.Equal(2, frame.SequenceNumber); // the latest, not the stale one
        Assert.Equal(1, pool.PresentedCount);
    }

    [Fact]
    public void WriteBuffer_IsNeverTheOneTheConsumerHolds_OverManyCycles()
    {
        FrameBufferPool pool = NewPool();
        int heldByConsumer = -1;

        for (int i = 0; i < 500; i++)
        {
            int writeIndex = WriteIndexOf(pool);
            Assert.NotEqual(heldByConsumer, writeIndex); // never scale into the buffer being drawn

            pool.Commit(i, TimeSpan.FromMilliseconds(i));

            if (i % 3 != 0) // a slow painter that skips a third of the frames
            {
                Assert.True(pool.TryAcquireFrame(out RentedFrame frame));
                heldByConsumer = frame.BufferIndex;
            }
        }

        Assert.True(pool.SupersededCount > 0);
        Assert.True(pool.PresentedCount > 0);
    }

    [Fact]
    public void ConcurrentProducerAndConsumer_NeverThrow_AndKeepAdvancing()
    {
        FrameBufferPool pool = NewPool(160, 120);
        long committed = 0;
        long acquired = 0;
        Exception? failure = null;
        var stop = new ManualResetEventSlim(false);

        var producer = new Thread(() =>
        {
            try
            {
                long seq = 0;
                while (!stop.IsSet)
                {
                    pool.Commit(seq++, TimeSpan.FromMilliseconds(seq));
                    Interlocked.Increment(ref committed);
                }
            }
            catch (Exception ex) { failure = ex; }
        });

        var consumer = new Thread(() =>
        {
            try
            {
                while (!stop.IsSet)
                {
                    if (pool.TryAcquireFrame(out _))
                    {
                        Interlocked.Increment(ref acquired);
                    }
                }
            }
            catch (Exception ex) { failure = ex; }
        });

        producer.Start();
        consumer.Start();
        Thread.Sleep(750);
        stop.Set();
        producer.Join();
        consumer.Join();

        Assert.Null(failure);
        Assert.True(Interlocked.Read(ref committed) > 100);
        Assert.True(Interlocked.Read(ref acquired) > 10);
        Assert.Equal(pool.PresentedCount, Interlocked.Read(ref acquired));
    }
}
