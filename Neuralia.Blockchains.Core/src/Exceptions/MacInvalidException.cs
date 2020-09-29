using System;
using System.Runtime.Serialization;

namespace Neuralia.Blockchains.Core.Exceptions
{
    public class MacInvalidException : ApplicationException
    {
        public MacInvalidException() : base("Verified Poly1305 Mac is not valid")
        {
        }

    }
}