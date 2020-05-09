namespace Neuralia.Blockchains.Core.Compression {
	public enum CompressionLevelByte : byte {
		None = 0,
		Level1 = 1,
		Fastest = Level1,
		Level2 = 2,
		Level3 = 3,
		Level4 = 4,
		Level5 = 5,
		Level6 = 6,
		Default = Level6,
		Level7 = 7,
		Level8 = 8,
		Level9 = 9,
		Optimal = Level9,
		Level10 = 10,
		Level11 = 11,
		Maximum = Level11
	}

	public enum CompressionLevelByte2 : byte {
		None = 0,
		Level1 = 1,
		Fastest = Level1,
		Level2 = 2,
		Level3 = 3,
		Level4 = 4,
		Level5 = 5,
		Level6 = 6,
		Default = Level6,
		Optimal = Default,
		Level7 = 7,
		Level8 = 8,
		Level9 = 9,
		Maximum = Level9
	}
}