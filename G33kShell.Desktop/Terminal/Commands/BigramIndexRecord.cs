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
using System.IO;

namespace G33kShell.Desktop.Terminal.Commands;

/// <summary>
/// Represents a single index record for fast bigram-based grep prefiltering.
/// Each record stores a 65,536-bit presence bitset (8,192 bytes) of all byte-pair bigrams found in a file.
/// </summary>
internal sealed class BigramIndexRecord
{
    // Record layout: PathHashMd5 (16) + FileSize (8) + LastWriteTimeUtcTicks (8) + BigramBitset (8192) = 8224 bytes
    public const int RecordSize = 8224;
    private const int BigramBitsetSize = 8192; // 65536 bits / 8 = 8192 bytes

    /// <summary>Bitset of all bigrams present in the file (65,536 bits).</summary>
    private readonly byte[] m_bigramBitset;

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
        m_bigramBitset = new byte[BigramBitsetSize];
    }

    /// <summary>
    /// Checks if a specific bigram bit is set.
    /// </summary>
    public bool IsBigramPresent(int bigramKey)
    {
        var byteIndex = bigramKey >> 3;
        var bitMask = 1 << (bigramKey & 7);
        return (m_bigramBitset[byteIndex] & bitMask) != 0;
    }

    /// <summary>
    /// Sets a specific bigram bit.
    /// </summary>
    private void SetBigram(int bigramKey)
    {
        var byteIndex = bigramKey >> 3;
        var bitMask = 1 << (bigramKey & 7);
        m_bigramBitset[byteIndex] |= (byte)bitMask;
    }

    /// <summary>
    /// Rebuilds the bigram bitset by scanning file content.
    /// </summary>
    public void RebuildFromFile(FileInfo file)
    {
        Array.Clear(m_bigramBitset, 0, m_bigramBitset.Length);
        m_fileSize = file.Length;
        m_lastWriteTimeUtcTicks = file.LastWriteTimeUtc.Ticks;

        // Stream file content and build bigram bitset
        using var stream = file.OpenRead();
        var prevByte = stream.ReadByte();
        if (prevByte == -1) return; // Empty file

        int currByte;
        while ((currByte = stream.ReadByte()) != -1)
        {
            var bigramKey = (prevByte << 8) | currByte;
            SetBigram(bigramKey);
            prevByte = currByte;
        }
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
        writer.Write(m_bigramBitset);
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
        reader.Read(record.m_bigramBitset, 0, BigramBitsetSize);
        return record;
    }
}
