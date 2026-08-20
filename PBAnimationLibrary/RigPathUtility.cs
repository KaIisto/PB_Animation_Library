using System.Text;

namespace PB_AnimationLibrary
{
    internal static class RigPathUtility
    {
        internal static uint ComputeCrc32(string value)
        {
            byte[] bytes = Encoding.UTF8.GetBytes(value ?? string.Empty);
            uint crc = 0xFFFFFFFFu;

            for (int i = 0; i < bytes.Length; ++i)
            {
                crc ^= bytes[i];

                for (int bit = 0; bit < 8; ++bit)
                {
                    uint mask = (crc & 1u) != 0u ? 0xFFFFFFFFu : 0u;
                    crc = (crc >> 1) ^ (0xEDB88320u & mask);
                }
            }

            return ~crc;
        }
    }
}
