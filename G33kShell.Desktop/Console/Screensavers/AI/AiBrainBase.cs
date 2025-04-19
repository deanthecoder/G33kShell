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

    protected AiBrainBase(AiBrainBase toCopy) =>
        m_qNet = toCopy.m_qNet.Clone();

    protected int ChooseHighestOutput(IAiGameState state) => ArgMax(GetOutputs(state));

    protected double[] GetOutputs(IAiGameState state) => m_qNet.Predict(state.ToInputVector());

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

    public byte[] Save() => JsonConvert.SerializeObject(this).Compress();

    public void Load(byte[] brainBytes) => JsonConvert.PopulateObject(brainBytes.DecompressToString(), this);

    public AiBrainBase Randomize()
    {
        m_qNet.Randomize();
        return this;
    }

    public AiBrainBase CrossWith(AiBrainBase second, double crossoverRate)
    {
        m_qNet.CrossWith(second.m_qNet, crossoverRate);
        return this;
    }

    public AiBrainBase Mutate(double mutationRate)
    {
        m_qNet.Mutate(mutationRate);
        return this;
    }

    public abstract AiBrainBase Clone();
}