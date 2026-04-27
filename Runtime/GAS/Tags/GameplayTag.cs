using System;
using UnityEngine;

namespace SpellBook.GAS.Tags
{
    [Serializable]
    public struct GameplayTag : IEquatable<GameplayTag>
    {
        [SerializeField] private GameplayTagDefinition definition;

        public GameplayTag(GameplayTagDefinition definition) => this.definition = definition;

        public bool IsValid => definition != null;
        public string Name => IsValid ? definition.name : "None";

        public bool Matches(GameplayTag other) => 
            IsValid && other.IsValid && definition.Matches(other.GetDefinition());

        public bool Equals(GameplayTag other) => definition == other.definition;
        public override bool Equals(object obj) => obj is GameplayTag other && Equals(other);
        public override int GetHashCode() => definition != null ? definition.GetHashCode() : 0;
        public static bool operator ==(GameplayTag a, GameplayTag b) => a.Equals(b);
        public static bool operator !=(GameplayTag a, GameplayTag b) => !a.Equals(b);
        public override string ToString() => Name;
        
        // Internal helper to get definition for ASC logic
        internal GameplayTagDefinition GetDefinition() => definition;
    }
}

