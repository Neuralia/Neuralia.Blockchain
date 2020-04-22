using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using Neuralia.Blockchains.Core.Extensions;
using Neuralia.Blockchains.Core.P2p.Connections;
using Neuralia.Blockchains.Core.P2p.Messages.Components;

namespace Neuralia.Blockchains.Core.Types {

	/// <summary>
	/// This class is meant to be a helper for heuristics application
	/// </summary>
	public static class NodeSelectionHeuristicTools {
		public enum NodeSelectionHeuristics {
			Default
		}

		private static readonly ConcurrentDictionary<Enums.ChainSharingTypes, List<(HeuristicsChainSharingTypes type, double percentage)>> DefaultTypeMaps = new ConcurrentDictionary<Enums.ChainSharingTypes, List<(HeuristicsChainSharingTypes, double)>>();

		private static readonly HeuristicsChainSharingTypes[] AllTypes = new[] {HeuristicsChainSharingTypes.None, HeuristicsChainSharingTypes.BlockOnly, HeuristicsChainSharingTypes.DigestThenBlocks, HeuristicsChainSharingTypes.DigestAndBlocks};

		static NodeSelectionHeuristicTools() {

			var map = new List<(HeuristicsChainSharingTypes, double)>();
			DefaultTypeMaps.AddSafe(Enums.ChainSharingTypes.DigestAndBlocks, map);
			map.Add((HeuristicsChainSharingTypes.DigestAndBlocks, 0.5));
			map.Add((HeuristicsChainSharingTypes.BlockOnly, 0.2));
			map.Add((HeuristicsChainSharingTypes.DigestThenBlocks, 0.2));
			map.Add((HeuristicsChainSharingTypes.Rest, 0));

			map = new List<(HeuristicsChainSharingTypes, double)>();
			DefaultTypeMaps.AddSafe(Enums.ChainSharingTypes.DigestThenBlocks, map);
			map.Add((HeuristicsChainSharingTypes.DigestThenBlocks, 0.5));
			map.Add((HeuristicsChainSharingTypes.DigestAndBlocks, 0.2));
			map.Add((HeuristicsChainSharingTypes.BlockOnly, 0.2));
			map.Add((HeuristicsChainSharingTypes.Rest, 0));

			map = new List<(HeuristicsChainSharingTypes, double)>();
			DefaultTypeMaps.AddSafe(Enums.ChainSharingTypes.BlockOnly, map);
			map.Add((HeuristicsChainSharingTypes.BlockOnly, 0.5));
			map.Add((HeuristicsChainSharingTypes.DigestAndBlocks, 0.3));
			map.Add((HeuristicsChainSharingTypes.DigestThenBlocks, 0.1));
			map.Add((HeuristicsChainSharingTypes.Rest, 0));

			map = new List<(HeuristicsChainSharingTypes, double)>();
			DefaultTypeMaps.AddSafe(Enums.ChainSharingTypes.None, map);
			map.Add((HeuristicsChainSharingTypes.None, 0.8));
			map.Add((HeuristicsChainSharingTypes.Rest, 0));
		}

		public enum HeuristicsChainSharingTypes : byte {
			None = 1,
			BlockOnly = 2,
			DigestThenBlocks = 3,
			DigestAndBlocks = 4,
			Rest = 5
		}

		//Enums.ChainSharingTypes

		private static Enums.ChainSharingTypes Convert(HeuristicsChainSharingTypes type) {
			if(type == HeuristicsChainSharingTypes.DigestAndBlocks) {
				return Enums.ChainSharingTypes.DigestAndBlocks;
			}

			if(type == HeuristicsChainSharingTypes.DigestThenBlocks) {
				return Enums.ChainSharingTypes.DigestThenBlocks;
			}

			if(type == HeuristicsChainSharingTypes.BlockOnly) {
				return Enums.ChainSharingTypes.BlockOnly;
			}

			if(type == HeuristicsChainSharingTypes.None) {
				return Enums.ChainSharingTypes.None;
			}

			return Enums.ChainSharingTypes.None;
		}

		/// <summary>
		/// select a list of nodes with a somewhat reasonably itnelligent routine
		/// </summary>
		/// <param name="nodes"></param>
		/// <param name="blockchainTypes"></param>
		/// <param name="heuristics"></param>
		/// <param name="excludeAddresses"></param>
		/// <param name="limit"></param>
		/// <returns></returns>
		public static NodeAddressInfoList SelectNodes(NodeInfo targetNode, List<NodeAddressInfo> nodes, List<BlockchainType> blockchainTypes, NodeSelectionHeuristics heuristics, List<NodeAddressInfo> excludeAddresses, int? limit = null) {

			//TODO: this method can be highly improved. this is basic functionality
			if(!nodes.Any()) {
				return new NodeAddressInfoList();
			}

			NodeAddressInfoList result = new NodeAddressInfoList();

			// ensure randomness
			nodes = nodes.Shuffle().ToList();

			// we may want a certain limited amount
			var nodesFiltered = new List<NodeAddressInfo>();

			if(heuristics == NodeSelectionHeuristics.Default) {

				if(blockchainTypes != null) {

					bool foundBlockchains = false;
					// lets filter by blockchain
					foreach(BlockchainType bc in blockchainTypes) {

						int remaining = 10;

						var chainSettings = targetNode.GetChainSettings();

						if(!chainSettings.ContainsKey(bc)) {
							continue;
						}

						foundBlockchains = true;
						var targetChainShareType = chainSettings[bc].ShareType;

						var usableList = nodes.Where(n => n.PeerInfo.GetSupportedBlockchains().Any(c => c == bc)).ToList();

						var map = DefaultTypeMaps[targetChainShareType];

						foreach((HeuristicsChainSharingTypes type, double percentage) in map) {

							int need = (int) Math.Ceiling(remaining * percentage);
							List<NodeAddressInfo> picked = null;

							if(type == HeuristicsChainSharingTypes.Rest) {

								var restTypes = AllTypes.Where(e => !map.Select(r => r.type).Where(d => d != HeuristicsChainSharingTypes.Rest).Contains(e)).Select(w => Convert(w)).ToList();

								double rest = 0.1;
								var restList = map.Where(e => !restTypes.Contains(Convert(e.type))).ToList();

								if(restList.Any()) {
									rest = restList.Sum(e => e.percentage);
								}

								need = (int) Math.Ceiling(remaining * (1 - rest));

								var sublist = usableList.Where(n => n.PeerInfo.GetSupportedBlockchains().Any(c => c == bc) && restTypes.Contains(n.PeerInfo.GetChainSettings()[bc].ShareType));

								picked = sublist.Take(need).ToList();

							} else {
								picked = usableList.Where(n => n.PeerInfo.GetSupportedBlockchains().Any(c => c == bc)).Where(e => e.PeerInfo.GetChainSettings()[bc].ShareType == Convert(type)).Take(need).ToList();
							}

							nodesFiltered.AddRange(picked);

							remaining -= picked.Count;

							if(remaining <= 0) {
								break;
							}
						}

						if(remaining != 0) {
							nodesFiltered.AddRange(usableList.Where(e => !nodesFiltered.Select(s => s.AdjustedIp).Contains(e.AdjustedIp)).Take(remaining));
						}

					}

					if(!foundBlockchains) {
						nodesFiltered.AddRange(nodes.Take(10));
					}
				}
			}

			nodesFiltered = nodesFiltered.Distinct().ToList();

			if(limit.HasValue) {

				nodesFiltered = nodesFiltered.Take(limit.Value).ToList();
			}

			return new NodeAddressInfoList(nodesFiltered);
		}
	}
}