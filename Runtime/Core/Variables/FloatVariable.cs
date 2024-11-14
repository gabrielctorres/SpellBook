using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace SmitePackage.Core.Variables
{
    [CreateAssetMenu(fileName = "FloatVariable", menuName = "SmitePackage/Variables/Float", order = 0)]
    public class FloatVariable : ScriptableObject
    {
        public float Value;

        public void SetValue(float value)
        {
            Value = value;
        }

        public void SetValue(FloatVariable value)
        {
            Value = value.Value;
        }

        public void ApplyChange(float amount)
        {
            Value += amount;
        }

        public void ApplyChange(FloatVariable amount)
        {
            Value += amount.Value;
        }
    }
}
