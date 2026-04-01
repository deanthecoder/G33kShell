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
using DTC.Core.AI;
using DTC.Core.Extensions;
using Newtonsoft.Json;
// ReSharper disable once RedundantUsingDirective
using System.Linq;

namespace G33kShell.Desktop.Console.Screensavers.AI;

public abstract class AiBrainBase
{
    private sealed class BrainSaveEnvelope
    {
        public string BrainType { get; set; }
        public int Version { get; set; }
        public string Payload { get; set; }
    }

    [JsonProperty] private NeuralNetwork m_qNet;
    private double[] m_inputVector;

    public int InputSize { get; }
    public int[] HiddenLayers { get; }
    public int OutputSize { get; }
    protected virtual int BrainVersion => 1;

    protected AiBrainBase(int inputSize, int[] hiddenLayers, int outputSize, NeuralNetwork qNet = null)
    {
        InputSize = inputSize;
        HiddenLayers = (int[])hiddenLayers.Clone();
        OutputSize = outputSize;
        m_qNet = qNet?.Clone() ?? new NeuralNetwork(inputSize, hiddenLayers, outputSize, learningRate: 0.05);
        m_inputVector = new double[inputSize];
    }

    protected AiBrainBase(AiBrainBase toCopy)
        : this(toCopy.InputSize, toCopy.HiddenLayers, toCopy.OutputSize, toCopy.m_qNet)
    {
    }

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

    public byte[] Save() =>
        JsonConvert.SerializeObject(new BrainSaveEnvelope
        {
            BrainType = GetType().FullName,
            Version = BrainVersion,
            Payload = JsonConvert.SerializeObject(this)
        }).Compress();

    public AiBrainBase Load(byte[] brainBytes)
    {
        if (brainBytes == null || brainBytes.Length == 0)
            return this;

        var json = brainBytes.DecompressToString();
        var envelope = JsonConvert.DeserializeObject<BrainSaveEnvelope>(json);
        if (!string.IsNullOrWhiteSpace(envelope?.Payload))
        {
            if (!string.Equals(envelope.BrainType, GetType().FullName, StringComparison.Ordinal))
            {
                System.Console.WriteLine($"Skipping incompatible brain type '{envelope.BrainType}' for '{GetType().FullName}'.");
                return this;
            }

            if (envelope.Version != BrainVersion)
            {
                System.Console.WriteLine($"Skipping saved brain version {envelope.Version} for '{GetType().Name}' because this build expects version {BrainVersion}.");
                return this;
            }

            JsonConvert.PopulateObject(envelope.Payload, this);
            m_inputVector = new double[InputSize];
            return this;
        }

        if (BrainVersion != 1)
        {
            System.Console.WriteLine($"Skipping legacy brain data for '{GetType().Name}' because this build expects version {BrainVersion}.");
            return this;
        }

        JsonConvert.PopulateObject(json, this);
        m_inputVector = new double[InputSize];
        return this;
    }

    public AiBrainBase CrossWith(AiBrainBase second, double crossoverRate, Random random = null)
    {
        m_qNet.CrossWith(second.m_qNet, crossoverRate, random);
        return this;
    }

    public AiBrainBase Mutate(double mutationRate, Random random = null)
    {
        m_qNet.Mutate(mutationRate, random);
        return this;
    }

    public abstract AiBrainBase Clone();
}
