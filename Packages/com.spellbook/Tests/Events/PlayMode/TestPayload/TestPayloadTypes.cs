using System;

namespace SpellBook.Events.Tests
{
    /// <summary>Payload próprio dos testes, com campos de mais de um tipo.</summary>
    [Serializable]
    public struct TestPayload
    {
        public int Amount;
        public string Label;
    }

    /// <summary>Resposta ao <see cref="TestPayloadEventChannel"/>, sem corpo, como no manual.</summary>
    [Serializable]
    public sealed class TestPayloadEventResponse : EventResponse<TestPayload>
    {
    }
}
