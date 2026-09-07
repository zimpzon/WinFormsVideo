namespace Protocol;

/// <summary>Helpers for reusing byte buffers on the per-frame hot path.</summary>
public static class BufferUtil
{
    /// <summary>
    /// Ensure <paramref name="buffer"/> is at least <paramref name="needed"/> bytes, growing it to
    /// the next power of two if not. Grow-only — never shrinks. Contents are not preserved.
    /// </summary>
    public static void EnsureCapacity(ref byte[] buffer, int needed)
    {
        if (buffer.Length >= needed)
        {
            return;
        }

        int size = buffer.Length == 0 ? 4096 : buffer.Length;
        while (size < needed)
        {
            size *= 2;
        }

        buffer = new byte[size];
    }
}
