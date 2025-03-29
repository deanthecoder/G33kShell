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
using System.Linq;
using Newtonsoft.Json;

namespace G33kShell.Desktop.Console.Screensavers.Snake;

/// <summary>
/// Builds a collection of game states, allowing a sample to be used for training.
/// </summary>
public class ReplayBuffer
{
    [JsonProperty]
    private Queue<(double[] state, int action, double reward, double[] nextState)> m_buffer = new();
    [JsonProperty]
    private int m_capacity;
    private readonly Random m_rand = new Random();

    public ReplayBuffer(int capacity)
    {
        m_capacity = capacity;
    }

    public void Add(double[] state, int action, double reward, double[] nextState)
    {
        if (m_buffer.Count >= m_capacity)
            m_buffer.Dequeue(); // O(1)
        m_buffer.Enqueue((state, action, reward, nextState));
    }
    
    public List<(double[] state, int action, double reward, double[] nextState)> Sample(int count) =>
        m_buffer.OrderBy(_ => m_rand.Next()).Take(count).ToList();

    public int Count => m_buffer.Count;
}