using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Neuralia.Blockchains.Core;
using Neuralia.Blockchains.Core.Compression;
using Neuralia.Blockchains.Tools.Data;

namespace Neuralia.Blockchains.Common.Classes.Blockchains.Common.Tools.Appointments {
	public abstract class AppointmentPuzzleEngineBase {

		protected const string PUZZLE_ROOT_FOLDER_NAME = "puzzles";
		protected const string LIBRARIES_FOLDER_NAME = "libraries";
		protected const string ENGINE_FILE_NAME = "frame.dat";
		protected static string EmbeddedResourceRoot { get => FileUtilities.GetEmbeddedResourceRootLocation(); }
		protected static Assembly ResourcesAssembly { get => FileUtilities.GetEmbeddedResourceAssembly(); }
		protected abstract string GetPath { get; }
		protected abstract int EngineVersion { get; }

		protected string GetBasePath() {
			return Path.Combine(FileUtilities.GetExecutingDirectory(), PUZZLE_ROOT_FOLDER_NAME);
		}

		protected string GetBaseResource()
        {
			return $"{EmbeddedResourceRoot}.{PUZZLE_ROOT_FOLDER_NAME}";
        }
		
		protected string GetEnginePath() {
			return Path.Combine(GetBasePath(), this.GetPath);
		}

		protected string GetEngineResource()
		{
			string pathToResource = Regex.Replace(this.GetPath, @"([\/\\])", match => ".");
			return $"{GetBaseResource()}.{pathToResource}";
		}

		protected virtual string GetLibrariesPath() {
			return Path.Combine(GetEnginePath(), LIBRARIES_FOLDER_NAME);
		}

		protected string GetLibrariesResource()
		{
			return $"{GetEngineResource()}.{LIBRARIES_FOLDER_NAME}";
		}

		public string PackageInstructions(string instructions) {
			return instructions;
		}
		
		public async Task<string> PackagePuzzle(int index, int appointmentKeyHash, string localeTable, string puzzleCode, List<string> libraries) {

			string frame = await this.LoadFrame().ConfigureAwait(false);
			List<string> libraryFiles = new List<string>();
			foreach(var library in libraries) {
				
				libraryFiles.Add(await this.LoadLibrary(library).ConfigureAwait(false));
			}
			
			return string.Format(frame, index, appointmentKeyHash, string.Join(" ", libraryFiles.Select(l => $"<script type=\"text/javascript\">{l}</script>")), localeTable, puzzleCode);
		}

		protected virtual async Task<string> LoadFrame() {
			try
            {
				return await LoadDecompressFromPath(Path.Combine(GetEnginePath(), ENGINE_FILE_NAME)).ConfigureAwait(false);
			}
			catch (Exception)
            {
				//Something went wrong while looking for the file using a path (maybe the path does not exist). Let's try with resources instead.
				return await LoadDecompressFromResource($"{GetEngineResource()}.{ENGINE_FILE_NAME}").ConfigureAwait(false);
			}
		}
		
		protected virtual async Task<string> LoadLibrary(string key) {
			try
			{
				return await LoadDecompressFromPath(Path.Combine(GetLibrariesPath(), $"{key}.dat")).ConfigureAwait(false);
			}
			catch (Exception)
			{
				try
				{
					//Something went wrong while looking for the file using a path (maybe the path does not exist). Let's try with resources instead.
					return await LoadDecompressFromResource($"{GetLibrariesResource()}.{key}.dat").ConfigureAwait(false);
				}
				catch (Exception)
				{
					//It's probably because it's an external library, so let's search for it there instead.
					return await LoadDecompressFromBytes(await FileUtilities.GetExternalJsLibraryResource($"{key}.dat", this.EngineVersion).ConfigureAwait(false)).ConfigureAwait(false);
				}
			}
		}

		protected async Task<string> LoadDecompressFromPath(string file) {
			byte[] array = await File.ReadAllBytesAsync(file).ConfigureAwait(false);
			return await LoadDecompressFromBytes(array).ConfigureAwait(false);
		}

		protected async Task<string> LoadDecompressFromResource(string resource)
		{
			if (ResourcesAssembly == null)
				throw new InvalidOperationException(
					$"Cannot fetch an embedded resource if the assembly is not set using {nameof(FileUtilities)}.{nameof(FileUtilities.ConfigureEmbeddedResourcesLocation)}."
					);

			await using var memoryStream = new MemoryStream();

			await using (var sourceStream = ResourcesAssembly.GetManifestResourceStream(resource))
			{				
				await sourceStream.CopyToAsync(memoryStream).ConfigureAwait(false);
			}

			return await LoadDecompressFromBytes(memoryStream.ToArray()).ConfigureAwait(false);
		}

		protected Task<string> LoadDecompressFromBytes(byte[] array)
        {
			using var compressedBytes = SafeArrayHandle.Wrap(array);

			BrotliCompression brotli = new BrotliCompression();
			using var innerSecretPackageBytes = brotli.Decompress(compressedBytes);

			string result = System.Text.Encoding.UTF8.GetString(innerSecretPackageBytes.ToExactByteArray());
			return Task.FromResult(result);
		}
	}
}