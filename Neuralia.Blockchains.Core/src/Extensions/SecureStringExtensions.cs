using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Security;
using System.Text;
using Neuralia.Blockchains.Tools.Data;

namespace Neuralia.Blockchains.Core.Extensions {
	public static class SecureStringExtensions {

		public static string ConvertToUnsecureString(this SecureString securePassword) {
			if(securePassword == null) {
				throw new ArgumentNullException(nameof(securePassword));
			}

			IntPtr unmanagedString = IntPtr.Zero;

			try {
				unmanagedString = Marshal.SecureStringToGlobalAllocUnicode(securePassword);

				return Marshal.PtrToStringUni(unmanagedString);
			} finally {
				Marshal.ZeroFreeGlobalAllocUnicode(unmanagedString);
			}
		}

		public static IByteArray ConvertToUnsecureBytes(this SecureString securePassword) {
			return (ByteArray) Encoding.UTF8.GetBytes(securePassword.ConvertToUnsecureString());
		}

		public static SecureString ConvertToSecureString(this string password) {
			if(password == null) {
				throw new ArgumentNullException(nameof(password));
			}

			SecureString secure = new SecureString();

			foreach(char c in password) {
				secure.AppendChar(c);
			}

			secure.MakeReadOnly();

			return secure;
		}

		public static SecureString ConvertToSecureString(this IByteArray password) {
			return Encoding.UTF8.GetString(password.ToExactByteArray()).ConvertToSecureString();
		}

		public static bool SecureStringEqual(this SecureString source, SecureString secureString2) {
			if(source == null) {
				throw new ArgumentNullException("s1");
			}

			if(secureString2 == null) {
				throw new ArgumentNullException("s2");
			}

			if(source.Length != secureString2.Length) {
				return false;
			}

			IntPtr ss_bstr1_ptr = IntPtr.Zero;
			IntPtr ss_bstr2_ptr = IntPtr.Zero;

			try {
				ss_bstr1_ptr = Marshal.SecureStringToBSTR(source);
				ss_bstr2_ptr = Marshal.SecureStringToBSTR(secureString2);

				string str1 = Marshal.PtrToStringBSTR(ss_bstr1_ptr);
				string str2 = Marshal.PtrToStringBSTR(ss_bstr2_ptr);

				return str1.Equals(str2);
			} finally {
				if(ss_bstr1_ptr != IntPtr.Zero) {
					Marshal.ZeroFreeBSTR(ss_bstr1_ptr);
				}

				if(ss_bstr2_ptr != IntPtr.Zero) {
					Marshal.ZeroFreeBSTR(ss_bstr2_ptr);
				}
			}
		}

		public static void Clear(this MemoryStream source) {
			var buffer = source.GetBuffer();
			Array.Clear(buffer, 0, (int) source.Length);
			source.Position = 0;
			source.SetLength(0);
		}

		private static byte[] ToByteArray(this SecureString secureString, Encoding encoding = null) {
			if(secureString == null) {
				throw new ArgumentNullException(nameof(secureString));
			}

			encoding = encoding ?? Encoding.UTF8;

			IntPtr unmanagedString = IntPtr.Zero;

			try {
				unmanagedString = Marshal.SecureStringToGlobalAllocUnicode(secureString);

				return encoding.GetBytes(Marshal.PtrToStringUni(unmanagedString));
			} finally {
				if(unmanagedString != IntPtr.Zero) {
					Marshal.ZeroFreeBSTR(unmanagedString);
				}
			}
		}
	}
}