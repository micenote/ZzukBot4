﻿using System.Runtime.InteropServices;
using System.Security;

namespace ZzukBot.Core.Utilities.Extensions
{
    internal static class SecureStringExtensions
    {
        internal static string SecureStringToString(this SecureString value)
        {
            var bstr = Marshal.SecureStringToBSTR(value);

            try
            {
                return Marshal.PtrToStringBSTR(bstr);
            }
            finally
            {
                Marshal.FreeBSTR(bstr);
            }
        }
    }
}
