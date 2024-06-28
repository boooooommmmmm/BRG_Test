using System;
using System.Diagnostics;
using System.Runtime.InteropServices;

namespace BRGContainer.Runtime
{
    [StructLayout(LayoutKind.Sequential)]
    [DebuggerDisplay("Value = {Value}")]
    public readonly struct BatchLODGroupID : IEquatable<BatchLODGroupID>
    {
        public readonly long Value;

        public BatchLODGroupID(long value)
        {
            Value = value;
        }

        public bool Equals(BatchLODGroupID other)
        {
            return Value == other.Value;
        }

        public override bool Equals(object obj)
        {
            return obj is BatchLODGroupID other && Equals(other);
        }

        public override int GetHashCode()
        {
            return Value.GetHashCode();
        }

        public static bool operator ==(BatchLODGroupID left, BatchLODGroupID right)
        {
            return left.Equals(right);
        }

        public static bool operator !=(BatchLODGroupID left, BatchLODGroupID right)
        {
            return !left.Equals(right);
        }
    }
}