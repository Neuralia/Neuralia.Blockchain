using System;
using Neuralia.Blockchains.Core;

namespace Neuralia.Blockchains.Common.Classes.Tools {
	public struct TierDataSet<T> {
		public TierDataSet(T firstTier, T secondTier, T thirdTier) {
			this.FirstTier = firstTier;
			this.SecondTier = secondTier;
			this.ThirdTier = thirdTier;
		}

		public T FirstTier { get; set; }
		public T SecondTier { get; set; }
		public T ThirdTier { get; set; }
		
		public static implicit operator TierDataSet<T>((T a, T b, T c) other) {
			return new TierDataSet<T>(other.a, other.b, other.c);
		}
		
		public T this[Enums.MiningTiers tier]
		{
			get {
				T t = default;

				if(tier == Enums.MiningTiers.FirstTier) {
					return this.FirstTier;
				}
				else if(tier == Enums.MiningTiers.SecondTier) {
					return this.SecondTier;
				}
				else if(tier == Enums.MiningTiers.ThirdTier) {
					return this.ThirdTier;
				}

				return default;
			}
			set {
				if(tier == Enums.MiningTiers.FirstTier) {
					this.FirstTier = value;
				}
				else if(tier == Enums.MiningTiers.SecondTier) {
					this.SecondTier = value;
				}
				else if(tier == Enums.MiningTiers.ThirdTier) {
					this.ThirdTier = value;
				}
			}
		}
	}
}