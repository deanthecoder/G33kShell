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
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using DTC.Core.Extensions;

namespace G33kShell.Desktop.Terminal.Commands;

/// <summary>
/// Manages a bigram-based index for fast grep prefiltering.
/// Index file is stored alongside application settings as "index.dat".
/// Singleton pattern ensures index is loaded once and reused across grep commands.
/// </summary>
internal sealed class BigramIndex
{
    private static BigramIndex s_instance;
    private static readonly object s_lock = new object();

    private readonly FileInfo m_indexFile;
    private readonly Dictionary<string, BigramIndexRecord> m_records = new();
    private bool m_modified;

    /// <summary>
    /// Gets the singleton instance of the bigram index.
    /// </summary>
    public static BigramIndex Instance
    {
        get
        {
            if (s_instance == null)
            {
                lock (s_lock)
                    s_instance ??= new BigramIndex();
            }
            return s_instance;
        }
    }

    private BigramIndex()
    {
        var settingsDir = Assembly.GetEntryAssembly().GetAppSettingsPath();
        m_indexFile = settingsDir.GetFile("index.dat");
        Load();
    }

    /// <summary>
    /// Gets the number of entries in the index.
    /// </summary>
    public int EntryCount => m_records.Count;

    /// <summary>
    /// Gets the number of files added/updated during this grep operation.
    /// </summary>
    public int NewFilesIndexed { get; private set; }

    /// <summary>
    /// Gets the number of files skipped by bigram prefilter during this grep operation.
    /// </summary>
    public int FilesSkippedByPrefilter { get; private set; }

    /// <summary>
    /// Resets per-operation statistics. Call this at the start of each grep.
    /// </summary>
    public void ResetStats()
    {
        NewFilesIndexed = 0;
        FilesSkippedByPrefilter = 0;
    }

    /// <summary>
    /// Gets the size of the index file on disk.
    /// </summary>
    public long IndexFileSize => m_indexFile.Exists ? m_indexFile.Length : 0;

    /// <summary>
    /// Gets the uncompressed size of the index in memory.
    /// </summary>
    public long UncompressedSize => m_records.Count * (long)BigramIndexRecord.RecordSize;

    /// <summary>
    /// Loads the index from disk (LZ4 compressed).
    /// Only called once when singleton is first accessed.
    /// </summary>
    private void Load()
    {
        m_records.Clear();
        if (!m_indexFile.Exists)
            return;

        try
        {
            // Read and decompress
            var compressedData = m_indexFile.ReadAllBytes();
            var decompressedData = compressedData.Decompress();

            using var stream = new MemoryStream(decompressedData);
            using var reader = new BinaryReader(stream);

            while (stream.Position < stream.Length)
            {
                var record = BigramIndexRecord.ReadFrom(reader);
                var hashKey = Convert.ToHexString(record.PathHashMd5);
                m_records[hashKey] = record;
            }
        }
        catch (Exception)
        {
            // Corrupted/old format index - clear it
            m_records.Clear();
        }
    }

    /// <summary>
    /// Saves the index to disk if modified (LZ4 compressed).
    /// </summary>
    public void Save()
    {
        if (!m_modified)
            return;

        try
        {
            m_indexFile.Directory?.Create();

            // Write to memory stream
            using var memStream = new MemoryStream();
            using (var writer = new BinaryWriter(memStream, Encoding.UTF8, leaveOpen: true))
            {
                foreach (var record in m_records.Values)
                    record.WriteTo(writer);
            }

            // Compress and write to disk
            var uncompressedData = memStream.ToArray();
            var compressedData = uncompressedData.Compress();
            m_indexFile.WriteAllBytes(compressedData);
            m_indexFile.Refresh(); // Refresh cached file info (length, exists, etc.)

            m_modified = false;
        }
        catch (Exception)
        {
            // Ignore save errors
        }
    }

    /// <summary>
    /// Deletes the index file from disk and resets singleton.
    /// </summary>
    public void Clear()
    {
        m_records.Clear();
        m_modified = false;

        try
        {
            if (m_indexFile.Exists)
                m_indexFile.Delete();
        }
        catch (Exception)
        {
            // Ignore delete errors
        }

        // Reset singleton
        lock (s_lock)
        {
            s_instance = null;
        }
    }

    /// <summary>
    /// Computes MD5 hash of normalized file path.
    /// </summary>
    private static byte[] ComputePathHash(string fullPath)
    {
        // Normalize: use absolute path, replace separators with '/'
        var normalized = Path.GetFullPath(fullPath).Replace('\\', '/');
        var bytes = Encoding.UTF8.GetBytes(normalized);
        return MD5.HashData(bytes);
    }

    /// <summary>
    /// Checks if a file is already indexed with current metadata (and therefore known to be text-scannable).
    /// </summary>
    public bool IsIndexed(FileInfo file)
    {
        var pathHash = ComputePathHash(file.FullName);
        var hashKey = Convert.ToHexString(pathHash);
        return m_records.TryGetValue(hashKey, out var record) && record.IsStillValid(file);
    }

    /// <summary>
    /// Gets or creates an index record for a file, rebuilding if stale.
    /// </summary>
    private BigramIndexRecord GetOrUpdateRecord(FileInfo file)
    {
        var pathHash = ComputePathHash(file.FullName);
        var hashKey = Convert.ToHexString(pathHash);

        if (m_records.TryGetValue(hashKey, out var record) && record.IsStillValid(file))
            return record; // Valid cached record

        // Rebuild record
        record = new BigramIndexRecord(pathHash);
        record.RebuildFromFile(file);
        m_records[hashKey] = record;
        m_modified = true;
        NewFilesIndexed++;

        return record;
    }

    /// <summary>
    /// Checks if a file can possibly contain the search string using bigram prefilter.
    /// Returns true if file should be scanned, false if it definitely cannot match.
    /// </summary>
    private bool CanContain(FileInfo file, string searchText)
    {
        if (searchText.Length < 2)
            return true; // Cannot prefilter short strings

        try
        {
            var record = GetOrUpdateRecord(file);

            // Convert search string to bytes using single-byte encoding (Latin-1)
            var searchBytes = Encoding.Latin1.GetBytes(searchText);

            // Check all bigrams from search string
            for (var i = 0; i < searchBytes.Length - 1; i++)
            {
                var bigramKey = (searchBytes[i] << 8) | searchBytes[i + 1];
                if (!record.IsBigramPresent(bigramKey))
                {
                    FilesSkippedByPrefilter++;
                    return false; // Definitely no match
                }
            }

            return true; // All bigrams present - might match
        }
        catch (Exception)
        {
            // On error, assume file might contain the text
            return true;
        }
    }

    /// <summary>
    /// Filters a list of files to only those that might contain the search text.
    /// </summary>
    public IEnumerable<FileInfo> FilterCandidates(IEnumerable<FileInfo> files, string searchText)
    {
        if (searchText.Length < 2)
            return files; // Cannot prefilter

        return files.Where(file => CanContain(file, searchText));
    }
}
