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
    private readonly double[] m_inputVector;

    public int InputSize { get; }
    public int[] HiddenLayers { get; private set; }
    public int OutputSize { get; private set; }

    protected AiBrainBase(int inputSize, int[] hiddenLayers, int outputSize)
    {
        InputSize = inputSize;
        HiddenLayers = hiddenLayers;
        OutputSize = outputSize;
        m_qNet = new NeuralNetwork(inputSize, hiddenLayers, outputSize, learningRate: 0.05);
        m_inputVector = new double[inputSize];
    }

    protected AiBrainBase(AiBrainBase toCopy) =>
        m_qNet = toCopy.m_qNet.Clone();

    protected int ChooseHighestOutput(IAiGameState state) => ArgMax(GetOutputs(state));

    protected double[] GetOutputs(IAiGameState state)
    {
#if DEBUG
        Array.Fill(m_inputVector, 0xDE);
#endif
        state.FillInputVector(m_inputVector);
#if DEBUG
        if (m_inputVector.Contains(0xDE))
            throw new Exception("Input vector contains uninitialized data.");
#endif
        return m_qNet.Predict(m_inputVector);
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

    public byte[] Save() => JsonConvert.SerializeObject(this).Compress();

    public AiBrainBase Load(byte[] brainBytes)
    {
        if (brainBytes != null)
            JsonConvert.PopulateObject(brainBytes.DecompressToString(), this);
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