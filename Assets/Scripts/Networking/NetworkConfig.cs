﻿using System.Text;

namespace Networking
{
    public static class NetworkConfig
    {
        public static UTF8Encoding encoding = new UTF8Encoding();
        public static readonly float[] EncoderPrecisionScales = {1.0f, 10.0f, 100.0f, 1000.0f};
        public static readonly float[] DecoderPrecisionScales = {1.0f, 0.1f, 0.01f, 0.001f};
    }
}