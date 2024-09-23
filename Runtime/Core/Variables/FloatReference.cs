using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace SmitePackage.Core.Variables
{
    [System.Serializable]
    public class FloatReference
    {
        public bool useConstant = true;
        public float constantValue;
        public FloatVariable variable;

        public FloatReference()
        { }
        public FloatReference(float value)
        {
            useConstant = true;
            constantValue = value;
        }


        public float Value
        {
            get { return useConstant ? constantValue : variable.Value; }
        }


        public static implicit operator float(FloatReference reference)
        {
            return reference.Value;
        }
    }
}

