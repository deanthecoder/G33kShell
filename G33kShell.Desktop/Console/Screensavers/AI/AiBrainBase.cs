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
using CSharp.Core.AI;
using CSharp.Core.Extensions;
using Newtonsoft.Json;

namespace G33kShell.Desktop.Console.Screensavers.AI;

public abstract class AiBrainBase
{
    private readonly object m_brainLock = new object();
    [JsonProperty] private NeuralNetwork m_qNet;

    protected AiBrainBase(int inputSize, int[] hiddenLayers, int outputSize)
    {
        m_qNet = new NeuralNetwork(inputSize, hiddenLayers, outputSize, learningRate: 0.05);
    }

    protected int ChooseHighestOutput(IAiGameState state)
    {
        var outputs = GetOutputs(state);
        return ArgMax(outputs);
    }

    protected double[] GetOutputs(IAiGameState state)
    {
        lock (m_brainLock)
            return m_qNet.Predict(state.ToInputVector());
    }

    /// <summary>
    /// Finds the index of the maximum value in the array.
    /// </summary>
    private static int ArgMax(double[] values)
    {
        var bestIndex = 0;
        var bestValue = values[0];
        for (var i = 1; i < values.Length; i++)
        {
            if (values[i] > bestValue)
            {
                bestValue = values[i];
                bestIndex = i;
            }
        }
        return bestIndex;
    }

    public byte[] Save()
    {
        lock (m_brainLock)
            return JsonConvert.SerializeObject(this).Compress();
    }

    public void Load(byte[] brainBytes)
    {
        lock (m_brainLock)
            JsonConvert.PopulateObject(brainBytes.DecompressToString(), this);
    }

    public void AverageWith(AiBrainBase other)
    {
        lock (m_brainLock)
            m_qNet = m_qNet.AverageWith(other.m_qNet).NudgeWeights();
    }

    public void MixWith(AiBrainBase other)
    {
        lock (m_brainLock)
            m_qNet = m_qNet.MixWith(other.m_qNet).NudgeWeights();
    }

    public T Clone<T>() where T : AiBrainBase, new()
    {
        var clone = new T();
        lock (m_brainLock)
            clone.m_qNet = m_qNet.Clone();
        return clone;
    }
}