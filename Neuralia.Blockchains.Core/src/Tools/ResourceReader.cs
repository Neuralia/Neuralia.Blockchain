using System;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Resources;
using System.Text;

namespace Neuralia.Blockchains.Core.Tools {
	public class ResourceReader {

		private readonly ResourceManager resourceManager;

		public ResourceReader(Type assemblyType) {

			string resourceName = this.FindResourceName("translations.resources", assemblyType);

			this.resourceManager = new ResourceManager(resourceName.Replace(".resources", ""), this.GetAssembly(assemblyType));
		}

		public string GetString(string key) {
			return this.GetString(key, CultureInfo.DefaultThreadCurrentUICulture);
		}

		public string GetString(string key, CultureInfo cultureInfo) {
			return this.resourceManager.GetString(key, cultureInfo);
		}

		private string FindResourceName(string name, Type assemblyType) {
			return this.GetAssembly(assemblyType).GetManifestResourceNames().SingleOrDefault(n => n.EndsWith(name));
		}

		private string LoadResource(string name, Type assemblyType) {
			using(Stream stream = this.GetAssembly(assemblyType).GetManifestResourceStream(name)) {
				using(StreamReader reader = new StreamReader(stream, Encoding.UTF8)) {
					return reader.ReadToEnd();
				}
			}
		}

		private Assembly GetAssembly(Type assemblyType) {
			return assemblyType.GetTypeInfo().Assembly;
		}
	}
}