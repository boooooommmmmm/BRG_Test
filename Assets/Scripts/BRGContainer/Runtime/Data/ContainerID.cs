using System.Diagnostics;
using System.Runtime.InteropServices;

namespace BRGContainer.Runtime
{
    [StructLayout(LayoutKind.Sequential)]
    [DebuggerDisplay("Value = {Value}")]
    internal readonly struct ContainerID
    {
        public readonly long Value;

        public ContainerID(long value)
        {
            Value = value;
        }

        public bool Equals(ContainerID other)
        {
            return Value == other.Value;
        }

        public override bool Equals(object obj)
        {
            return obj is ContainerID other && Equals(other);
        }

        public override int GetHashCode()
        {
            return Value.GetHashCode();
        }

        public static bool operator ==(ContainerID left, ContainerID right)
        {
            return left.Equals(right);
        }

        public static bool operator !=(ContainerID left, ContainerID right)
        {
            return !left.Equals(right);
        }
    }
}