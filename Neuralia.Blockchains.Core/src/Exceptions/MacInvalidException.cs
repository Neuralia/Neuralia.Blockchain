﻿using System;

namespace Neuralia.Blockchains.Core.Exceptions
{
    public class MacInvalidException : ApplicationException
    {
        public MacInvalidException() : base("Verified Poly1305 Mac is not valid")
        {
        }

    }
}