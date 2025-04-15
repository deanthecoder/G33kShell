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
        lock (m_qNet)
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
        lock (m_qNet)
            return JsonConvert.SerializeObject(this).Compress();
    }

    public void Load(byte[] brainBytes)
    {
        lock (m_qNet)
            JsonConvert.PopulateObject(brainBytes.DecompressToString(), this);
    }

    public AiBrainBase InitWithLerp(AiBrainBase first, AiBrainBase second, double mix)
    {
        lock (m_qNet)
        lock (first.m_qNet)
        lock (second.m_qNet)
            m_qNet = first.m_qNet.CreateLerped(second.m_qNet, mix);
        return this;
    }

    public AiBrainBase InitWithSpliced(AiBrainBase first, AiBrainBase second)
    {
        lock (m_qNet)
        lock (first.m_qNet)
        lock (second.m_qNet)
            m_qNet = first.m_qNet.CreateSpliced(second.m_qNet);
        return this;
    }
    
    public AiBrainBase InitWithNudgedWeights(AiBrainBase brain, NeuralNetwork.NudgeFactor nudge)
    {
        lock (m_qNet)
        lock (brain.m_qNet)
            m_qNet = brain.m_qNet.CloneWithNudgeWeights(nudge);
        return this;
    }
    
    public AiBrainBase NudgeWeights(NeuralNetwork.NudgeFactor nudge)
    {
        lock (m_qNet)
            m_qNet = m_qNet.CloneWithNudgeWeights(nudge);
        return this;
    }
    
    public AiBrainBase InitWithBrain(AiBrainBase brain)
    {
        lock (m_qNet)
        lock (brain.m_qNet)
            m_qNet = brain.m_qNet.Clone();
        return this;
    }
}