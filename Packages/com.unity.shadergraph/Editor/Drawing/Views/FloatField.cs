
using System.Globalization;
using UnityEditor.UIElements;
#if UNITY_2022_1_OR_NEWER
using UnityEngine.UIElements;
#endif

namespace UnityEditor.ShaderGraph.Drawing
{
    class FloatField : DoubleField
    {
        protected override string ValueToString(double v)
        {
            return ((float)v).ToString(CultureInfo.InvariantCulture.NumberFormat);
        }
    }
}
