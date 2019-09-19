using System;
using System.Linq;
using Neuralia.Blockchains.Core.Compression;
using Neuralia.Blockchains.Core.Network.Protocols.SplitMessages;
using Neuralia.Blockchains.Core.Network.Protocols.V1;
using Neuralia.Blockchains.Tools.Cryptography.Hash;
using Neuralia.Blockchains.Tools.Data;
using Neuralia.Blockchains.Tools.General.ExclusiveOptions;
using Neuralia.Blockchains.Tools.Serialization;

namespace Neuralia.Blockchains.Core.Network.Protocols {
	public class ProtocolFactory {

		public delegate void CompressedMessageBytesReceived(SafeArrayHandle compressedMessageBytes, ISplitMessageEntry splitMessageEntry = null);

		private const int REPORTED_UUID_SIZE = 16;
		public const int HANDSHAKE_PROTOCOL_SIZE = 5 + REPORTED_UUID_SIZE;

		public static readonly byte[] HANDSHAKE_COUNTERCONNECT_BYTES = BitConverter.GetBytes(0xA21DB945DC90A422L);

		/// <summary>
		///     Our session unique protocol uuid
		/// </summary>
		public static readonly Guid PROTOCOL_UUID = Guid.NewGuid();

		private readonly object locker = new object();

		private readonly xxHasher64 xxhasher = new xxHasher64();

		private IMessageFactory messageFactory;
		private ProtocolCompression sharedProtocolCompression = MessageBuilder.ProtocolCompression;

		private ProtocolVersion? sharedProtocolVersion;

		private ICompression NetworkMessageCompressor { get; set; } = DeflateCompression.Instance;

		private IMessageFactory MessageFactory {
			get {
				lock(this.locker) {
					if(this.messageFactory == null) {
						if(!this.sharedProtocolVersion.HasValue) {
							throw new ApplicationException("Peer protocol has not been set");
						}

						if(this.sharedProtocolVersion.Value.Version == 1) {
							this.messageFactory = new MessageFactory();
						} else {
							throw new ApplicationException("Invalid protocol version");
						}

						Array values = Enum.GetValues(typeof(ProtocolCompression.CompressionAlgorithm));
						var levels = new byte[values.Length];
						int index = 0;

						foreach(byte entry in values) {
							levels[index++] = entry;
						}

						if((byte) this.sharedProtocolCompression.Type > levels.Max()) {
							throw new ApplicationException("Peer protocol compression type has an invalid value");
						}

						// get the list of valid levels
						values = Enum.GetValues(typeof(CompressionLevelByte));
						levels = new byte[values.Length];
						index = 0;

						foreach(byte entry in values) {
							levels[index++] = entry;
						}

						if((byte) this.sharedProtocolCompression.Level > levels.Max()) {
							throw new ApplicationException("Peer protocol compression level has an invalid value");
						}
					}
				}

				return this.messageFactory;
			}
		}

		public ByteArray CreateHandshake() {
			ByteArray handShakeSimpleBytes = ByteArray.Create(HANDSHAKE_PROTOCOL_SIZE);
			handShakeSimpleBytes[0] = 13; // the magic start number
			handShakeSimpleBytes[1] = MessageBuilder.ProtocolVersion.Version;
			handShakeSimpleBytes[2] = MessageBuilder.ProtocolVersion.Revision;

			lock(this.locker) {
				handShakeSimpleBytes[3] = (byte) this.sharedProtocolCompression.Type;
				handShakeSimpleBytes[4] = (byte) this.sharedProtocolCompression.Level;
			}

			TypeSerializer.Serialize(PROTOCOL_UUID, handShakeSimpleBytes.Span.Slice(5, REPORTED_UUID_SIZE));

			return handShakeSimpleBytes;
		}

		/// <summary>
		///     Set the protocol the peer uses, and agree to use the lowest common denominator
		/// </summary>
		/// <param name="peerProtocolVersion"></param>
		public void SetPeerProtocolVersion(ProtocolVersion peerProtocolVersion) {

			lock(this.locker) {
				if(peerProtocolVersion.Version > MessageBuilder.ProtocolVersion.Version) {
					// the peer uses a newer protocol which we dotn support. we will agree to use our own which is not as recent
					this.sharedProtocolVersion = MessageBuilder.ProtocolVersion;
				} else {
					// we agree on the same version
					this.sharedProtocolVersion = peerProtocolVersion;
				}
			}
		}

		public void SetPeerProtocolCompression(ProtocolCompression peerProtocolCompression) {
			lock(this.locker) {
				// accept the peer's compression settings
				this.sharedProtocolCompression = peerProtocolCompression;

				switch(this.sharedProtocolCompression.Type) {
					case ProtocolCompression.CompressionAlgorithm.None:
						this.NetworkMessageCompressor = NullCompression.Instance;

						break;

					case ProtocolCompression.CompressionAlgorithm.Deflate:
						this.NetworkMessageCompressor = new DeflateCompression();

						break;

					case ProtocolCompression.CompressionAlgorithm.Gzip:
						this.NetworkMessageCompressor = new GzipCompression();

						break;

					default:

						throw new ApplicationException("Invalid compression algorithm supplied");
				}
			}
		}

		public (ProtocolVersion version, ProtocolCompression compression, Guid uuid) ParseVersion(in Span<byte> buffer) {

			if(buffer.Length != HANDSHAKE_PROTOCOL_SIZE) {
				throw new ApplicationException("Invalid handshake buffer size");
			}

			if(buffer[0] != 13) {
				throw new ApplicationException("Initial trigger code is invalid");
			}

			var subBuffer = buffer.Slice(5, REPORTED_UUID_SIZE);
			TypeSerializer.Deserialize(in subBuffer, out Guid reportedUuid);

			return (new ProtocolVersion(buffer[1], buffer[2]), new ProtocolCompression((ProtocolCompression.CompressionAlgorithm) buffer[3], (CompressionLevelByte) buffer[4]), reportedUuid);
		}

		public IMessageParser CreateMessageParser(SafeArrayHandle bytes) {

			// first, lets get the protocol version
			byte protocolVersion = bytes[0];

			IMessageParser messageParser = null;

			if(protocolVersion == MessageBuilder.ProtocolVersion.Version) {
				messageParser = new MessageParser(bytes);
			} else {
				throw new NotSupportedException("Unsupported protocol version");
			}

			return messageParser;
		}

		/// <summary>
		///     Hash the raw message. must not be compressed yet!
		/// </summary>
		/// <param name="bytes"></param>
		/// <returns></returns>
		private long HashRawMessage(SafeArrayHandle bytes) {
			lock(this.locker) {
				return this.xxhasher.Hash(bytes);
			}
		}

		/// <summary>
		///     Here we compress the message if we have to. if it is in the cache, we simply reuse it
		/// </summary>
		/// <param name="bytes"></param>
		/// <returns></returns>
		private SafeArrayHandle CompressMessage(SafeArrayHandle bytes) {

			if(this.sharedProtocolCompression.Level == CompressionLevelByte.NoCompression) {
				return bytes; // its not compressed!
			}

			lock(this.locker) {

				return this.NetworkMessageCompressor.Compress(bytes, this.sharedProtocolCompression.Level);
			}
		}

		private SafeArrayHandle DecompressMessage(SafeArrayHandle compressedMessage) {

			if(this.sharedProtocolCompression.Level == CompressionLevelByte.NoCompression) {
				return compressedMessage.Branch(); // its not compressed! clone it because the original will get returned
			}

			lock(this.locker) {
				return this.NetworkMessageCompressor.Decompress(compressedMessage);
			}
		}

		public MessageInstance WrapMessage(SafeArrayHandle bytes, ShortExclusiveOption<TcpConnection.ProtocolMessageTypes> protocolMessageFilters) {

			long messageHash = this.HashRawMessage(bytes);

			MessageInstance messageInstance = this.WrapMessage(messageHash);

			// message was not in the cache, so we compress it and cache it if possible
			if(messageInstance == null) {

				using(SafeArrayHandle compressedBytes = this.CompressMessage(bytes)) {

					messageInstance = this.WrapCompressedMessage(compressedBytes, messageHash, protocolMessageFilters);
				}
			}

			return messageInstance;
		}

		public MessageInstance WrapMessage(long messageHash) {

			return CompressedMessageCache.Instance.Get(messageHash);
		}

		/// <summary>
		///     Wrap the compressed message and store it in the cache, ready to go
		/// </summary>
		/// <param name="compressedBytes"></param>
		/// <param name="messageHash"></param>
		/// <param name="protocolMessageFilters"></param>
		/// <returns></returns>
		private MessageInstance WrapCompressedMessage(SafeArrayHandle compressedBytes, long messageHash, ShortExclusiveOption<TcpConnection.ProtocolMessageTypes> protocolMessageFilters) {

			MessageInstance messageInstance = new MessageInstance();
			messageInstance.Size = compressedBytes.Length; // carfull, this is the original message size, but not the pretocol message size. it will get larger with the dehydrator metadata.
			messageInstance.Hash = messageHash;

			lock(this.locker) {
				messageInstance.MessageBytes.Entry = this.MessageFactory.CreateMessage(compressedBytes, protocolMessageFilters).Entry;
			}

			if(messageInstance.MessageBytes.IsEmpty) {

				// was too big, its most probably a split message
				lock(this.locker) {
					messageInstance.SplitMessage = this.MessageFactory.WrapBigMessage(compressedBytes, protocolMessageFilters);
				}

				// add it to the sending cache too
				if(!MessageCaches.SendCaches.Exists(messageInstance.SplitMessage.Hash)) {
					// ensure it is cached
					MessageCaches.SendCaches.AddEntry(messageInstance.SplitMessage);
				}
			}

			// lets cache it
			CompressedMessageCache.Instance.AddMessageEntry(messageInstance);

			return messageInstance;
		}

		public ISplitMessageEntry WrapBigMessage(SafeArrayHandle bytes, ShortExclusiveOption<TcpConnection.ProtocolMessageTypes> protocolMessageFilters) {
			lock(this.locker) {
				return this.MessageFactory.WrapBigMessage(bytes, protocolMessageFilters);
			}
		}

		public void HandleCompetedMessage(IMessageEntry entry, TcpConnection.MessageBytesReceived callback, IProtocolTcpConnection connection) {

			IMessageRouter router = null;

			switch(entry.Version) {
				case 1:
					router = new MessageRouter();

					break;

				default:

					throw new NotSupportedException("Unsupported protocol version");
			}

			router.HandleCompletedMessage(entry, (compressedMessageBytes, splitMessageEntry) => {
				SafeArrayHandle originalMessage = this.DecompressMessage(compressedMessageBytes);

				if(splitMessageEntry != null) {
					// well, its a split message entry its big, so we want to salvage it in case we will need to forward it too
					MessageInstance messageInstance = new MessageInstance();
					messageInstance.Hash = this.HashRawMessage(originalMessage);
					messageInstance.SplitMessage = splitMessageEntry;
					messageInstance.Size = compressedMessageBytes.Length;

					CompressedMessageCache.Instance.AddMessageEntry(messageInstance);
				}

				// thats it, thats our final message!
				callback(originalMessage);

			}, connection);
		}
	}
}