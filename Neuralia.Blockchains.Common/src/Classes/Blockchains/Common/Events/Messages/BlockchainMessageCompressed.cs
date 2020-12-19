using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Events.Serialization;
using Neuralia.Blockchains.Core.Compression;
using Neuralia.Blockchains.Core.Services;
using Neuralia.Blockchains.Tools.Data;
using Neuralia.Blockchains.Tools.Serialization;

namespace Neuralia.Blockchains.Common.Classes.Blockchains.Common.Events.Messages {

	public interface IBlockchainMessageCompressed : IBlockchainMessage {
		
	}
	
	
	/// <summary>
	/// a special version of the message where the content will be independently compressed. Suitable for very large message that may need to be stored in message form.
	/// </summary>
	public abstract class BlockchainMessageCompressed : BlockchainMessage, IBlockchainMessageCompressed {

		protected override sealed void RehydrateContents(IDataRehydrator rehydrator, IMessageRehydrationFactory rehydrationFactory) {
			base.RehydrateContents(rehydrator, rehydrationFactory);
			
			bool compressed = rehydrator.ReadBool();
			
			SafeArrayHandle messageBytes = (SafeArrayHandle) rehydrator.ReadNonNullableArray();
			if(compressed) {
				BrotliCompression compression = new BrotliCompression();
				
				var decompressedBytes = compression.Decompress(messageBytes);
				messageBytes.Dispose();
				messageBytes = decompressedBytes;
			} 

			using IDataRehydrator contentsRehydrator = DataSerializationFactory.CreateRehydrator(messageBytes);
			
			this.RehydrateCompressedContents(contentsRehydrator, rehydrationFactory);
		}

		protected override sealed void DehydrateContents(IDataDehydrator dehydrator) {
			base.DehydrateContents(dehydrator);
			
			using IDataDehydrator contentsDehydrator = DataSerializationFactory.CreateDehydrator();
			this.DehydrateCompressedContents(contentsDehydrator);
			var bytes = contentsDehydrator.ToArray();

			bool compress = bytes.Length > GlobalsService.MINIMUM_COMPRESS_SIZE;

			dehydrator.Write(compress);
			
			if(compress) {
				BrotliCompression compression = new BrotliCompression();
				var compressedBytes = compression.Compress(bytes);
				bytes.Dispose();
				bytes = compressedBytes;
			} 
			
			dehydrator.WriteNonNullable(bytes);
			
			using var messageBytes = dehydrator.ToArray();

			dehydrator.Write(messageBytes);
		}
		
		
		protected virtual void RehydrateCompressedContents(IDataRehydrator rehydrator, IMessageRehydrationFactory rehydrationFactory) {

		}

		protected virtual void DehydrateCompressedContents(IDataDehydrator dehydrator) {

		}
	}
}