// Code authored by Dean Edis (DeanTheCoder).
// Anyone is free to copy, modify, use, compile, or distribute this software,
// either in source code form or as a compiled binary, for any non-commercial
//  purpose.
// 
// If you modify the code, please retain this copyright header,
// and consider contributing back to the repository or letting us know
// about your modifications. Your contributions are valued!
// 
// THE SOFTWARE IS PROVIDED AS IS, WITHOUT WARRANTY OF ANY KIND.
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace G33kShell.Desktop.Terminal.Commands;

/// <summary>
/// Represents a single index record for fast bigram-based grep prefiltering.
/// Each record stores only the byte-pair bigrams found in a file.
/// </summary>
internal sealed class BigramIndexRecord
{
    private ushort[] m_bigrams = [];

    /// <summary>MD5 hash of the normalized file path.</summary>
    public byte[] PathHashMd5 { get; }

    /// <summary>File size in bytes at index time.</summary>
    private long m_fileSize;

    /// <summary>Last write time UTC ticks at index time.</summary>
    private long m_lastWriteTimeUtcTicks;

    public BigramIndexRecord(byte[] pathHashMd5)
    {
        if (pathHashMd5.Length != 16)
            throw new ArgumentException("Path hash must be 16 bytes (MD5).", nameof(pathHashMd5));
        
        PathHashMd5 = pathHashMd5;
    }

    public int SerializedSize => 16 + sizeof(long) + sizeof(long) + sizeof(int) + m_bigrams.Length * sizeof(ushort);

    /// <summary>
    /// Checks if a specific bigram is present.
    /// </summary>
    public bool IsBigramPresent(int bigramKey) =>
        Array.BinarySearch(m_bigrams, unchecked((ushort)bigramKey)) >= 0;

    /// <summary>
    /// Rebuilds the bigram bitset by scanning file content.
    /// </summary>
    public void RebuildFromFile(FileInfo file)
    {
        m_fileSize = file.Length;
        m_lastWriteTimeUtcTicks = file.LastWriteTimeUtc.Ticks;
        var bigrams = new HashSet<ushort>();

        // Stream file content and build bigram bitset
        using var stream = file.OpenRead();
        var prevByte = stream.ReadByte();
        if (prevByte == -1) return; // Empty file

        int currByte;
        while ((currByte = stream.ReadByte()) != -1)
        {
            var bigramKey = (prevByte << 8) | currByte;
            bigrams.Add(unchecked((ushort)bigramKey));
            prevByte = currByte;
        }

        m_bigrams = bigrams.OrderBy(o => o).ToArray();
    }

    /// <summary>
    /// Checks if file metadata matches this record (indicating index is still valid).
    /// </summary>
    public bool IsStillValid(FileInfo file) =>
        file.Length == m_fileSize && file.LastWriteTimeUtc.Ticks == m_lastWriteTimeUtcTicks;

    /// <summary>
    /// Serializes this record to a binary stream.
    /// </summary>
    public void WriteTo(BinaryWriter writer)
    {
        writer.Write(PathHashMd5);
        writer.Write(m_fileSize);
        writer.Write(m_lastWriteTimeUtcTicks);
        writer.Write(m_bigrams.Length);
        foreach (var bigram in m_bigrams)
            writer.Write(bigram);
    }

    /// <summary>
    /// Deserializes a record from a binary stream.
    /// </summary>
    public static BigramIndexRecord ReadFrom(BinaryReader reader)
    {
        var pathHashMd5 = reader.ReadBytes(16);
        var record = new BigramIndexRecord(pathHashMd5)
        {
            m_fileSize = reader.ReadInt64(),
            m_lastWriteTimeUtcTicks = reader.ReadInt64()
        };
        var count = reader.ReadInt32();
        record.m_bigrams = new ushort[count];
        for (var i = 0; i < count; i++)
            record.m_bigrams[i] = reader.ReadUInt16();
        return record;
    }
}
