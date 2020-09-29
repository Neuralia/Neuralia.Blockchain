using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Events.Envelopes;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Events.Messages.Serialization;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Events.Messages.Specialization.Moderation.Appointments;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Providers;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Tools;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Tools.Appointments;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Wallet.Account;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Workflows.Bases;
using Neuralia.Blockchains.Core;
using Neuralia.Blockchains.Core.Compression;
using Neuralia.Blockchains.Core.Configuration;
using Neuralia.Blockchains.Core.Cryptography.POW.V1;
using Neuralia.Blockchains.Core.Logging;
using Neuralia.Blockchains.Core.Network;
using Neuralia.Blockchains.Core.Network.AppointmentValidatorProtocol.V1;
using Neuralia.Blockchains.Core.Services;
using Neuralia.Blockchains.Core.Tools;
using Neuralia.Blockchains.Core.Workflows.Base;
using Neuralia.Blockchains.Core.Workflows.Tasks.Routing;
using Neuralia.Blockchains.Tools;
using Neuralia.Blockchains.Tools.Data;
using Neuralia.Blockchains.Tools.Data.Arrays;
using Neuralia.Blockchains.Tools.Locking;
using Neuralia.Blockchains.Tools.Serialization;
using Neuralia.Blockchains.Tools.Threading;
using Serilog;

namespace Neuralia.Blockchains.Common.Classes.Blockchains.Common.Workflows.Chain {
	public interface IPuzzleExecutionWorkflow : IChainWorkflow {

		void SetAnswers(List<int> answers);
		event Action<int, List<(string puzzle, string instructions)>> PuzzleBeginEvent;
	}

	public interface IPuzzleExecutionWorkflow<out CENTRAL_COORDINATOR, CHAIN_COMPONENT_PROVIDER> : IChainWorkflow<CENTRAL_COORDINATOR, CHAIN_COMPONENT_PROVIDER>, IPuzzleExecutionWorkflow
		where CENTRAL_COORDINATOR : ICentralCoordinator<CENTRAL_COORDINATOR, CHAIN_COMPONENT_PROVIDER>
		where CHAIN_COMPONENT_PROVIDER : IChainComponentProvider<CENTRAL_COORDINATOR, CHAIN_COMPONENT_PROVIDER> {

	}

	/// <summary>
	///     This workflow will ensure that the wallet is in sync with the chain.
	/// </summary>
	/// <typeparam name="CENTRAL_COORDINATOR"></typeparam>
	/// <typeparam name="CHAIN_COMPONENT_PROVIDER"></typeparam>
	public class PuzzleExecutionWorkflow<CENTRAL_COORDINATOR, CHAIN_COMPONENT_PROVIDER> : ChainWorkflow<CENTRAL_COORDINATOR, CHAIN_COMPONENT_PROVIDER>, IPuzzleExecutionWorkflow<CENTRAL_COORDINATOR, CHAIN_COMPONENT_PROVIDER>
		where CENTRAL_COORDINATOR : ICentralCoordinator<CENTRAL_COORDINATOR, CHAIN_COMPONENT_PROVIDER>
		where CHAIN_COMPONENT_PROVIDER : IChainComponentProvider<CENTRAL_COORDINATOR, CHAIN_COMPONENT_PROVIDER> {

		private readonly CorrelationContext correlationContext;
		private readonly string passphrase;
		private ManualResetEventSlim manualResetEvent;
		private List<int> answers;
		public event Action<int, List<(string puzzle, string instructions)>> PuzzleBeginEvent;
		protected DateTime hardStop;
		
		public PuzzleExecutionWorkflow(CENTRAL_COORDINATOR centralCoordinator, CorrelationContext correlationContext, string passphrase = null) : base(centralCoordinator) {
			this.correlationContext = correlationContext;
			this.passphrase = passphrase;
			this.ExecutionMode = Workflow.ExecutingMode.Single;
		}

		protected void UpdateAppointmentExpiration(IWalletAccount account, TimeSpan window) {
			if(!account.AccountAppointment.AppointmentExpirationTime.HasValue) {
				// we go no further than this relative time window
				this.hardStop = DateTimeEx.CurrentTime + window;
				
				// and past this time, no new puzzle workflow can be started, it would be pointless
				account.AccountAppointment.AppointmentExpirationTime = this.hardStop - TimeSpan.FromSeconds(30);
			}
		}
		
		protected override async Task PerformWork(IChainWorkflow workflow, TaskRoutingContext taskRoutingContext, LockContext lockContext) {
			taskRoutingContext.SetCorrelationContext(this.correlationContext);

			this.centralCoordinator.PostSystemEventImmediate(BlockchainSystemEventTypes.Instance.AppointmentPuzzlePreparation, this.correlationContext);

			var appointmentsProvider = this.centralCoordinator.ChainComponentProvider.AppointmentsProviderBase;
			var walletProvider = this.centralCoordinator.ChainComponentProvider.WalletProviderBase;

			SafeArrayHandle key = null;

			var distilledAppointmentContext = await walletProvider.GetDistilledAppointmentContextFile().ConfigureAwait(false);

			IWalletAccount account = await walletProvider.GetActiveAccount(lockContext).ConfigureAwait(false);
			Guid appointmentId = account.AccountAppointment.AppointmentId.Value;
			DateTime appointmentDate = account.AccountAppointment.AppointmentTime.Value;

			TimeSpan window = TimeSpan.FromSeconds(distilledAppointmentContext.Window);
			
			DateTime recheck = DateTimeEx.MinValue;

			while(true) {

				this.CheckShouldCancel();

				var triggerBytes = await appointmentsProvider.GetAppointmentTriggerGossipMessage(lockContext).ConfigureAwait(false);

				if(triggerBytes?.IsZero == false) {

					try {
						// let's rebuild the message
						var envelope = this.centralCoordinator.ChainComponentProvider.ChainFactoryProviderBase.BlockchainEventsRehydrationFactoryBase.RehydrateEnvelope<ISignedMessageEnvelope>(triggerBytes);

						envelope.RehydrateContents();
						envelope.Contents.Rehydrate(this.centralCoordinator.ChainComponentProvider.ChainFactoryProviderBase.BlockchainEventsRehydrationFactoryBase);

						IAppointmentTriggerMessage appointmentTriggerMessage = (IAppointmentTriggerMessage) envelope.Contents.RehydratedEvent;

						key = appointmentTriggerMessage.Key;

						break;
					} catch(Exception ex) {
						NLog.Default.Error(ex, $"Failed rehydrated trigger from gossip message. may attempt again...");
					}
				}

				Thread.Sleep(TimeSpan.FromSeconds(5));

				if(recheck < DateTimeEx.CurrentTime) {
					// do an explicit recheck
					try {
						key = await appointmentsProvider.CheckAppointmentTriggerUpdate(appointmentDate, lockContext).ConfigureAwait(false);

						if(key != null) {
							break;
						}
					} catch(Exception ex) {
						// do nothing here, we will try again later
						Log.Debug(ex, "failed to get trigger.");
					}

					recheck = DateTimeEx.CurrentTime.AddSeconds(30);
				}

				if(DateTimeEx.CurrentTime > appointmentDate.AddMinutes(5)) {
					// this is the end, we missed the time
					Log.Warning("We failed to run the puzzle in time. We never acquired the appointment trigger.");

					return;
				}
			}

			// ok, we have everything we need to start. First step, let's decrypt the puzzle
			(SafeArrayHandle puzzlePassword, SafeArrayHandle puzzleSalt) = AppointmentUtils.RebuildPuzzlePackagePassword(key);
			using SafeArrayHandle compressedPuzzleBytes = AppointmentUtils.Decrypt(SafeArrayHandle.Wrap(distilledAppointmentContext.PuzzleBytes), puzzlePassword, puzzleSalt);

			BrotliCompression brotli = new BrotliCompression();
			using var puzzleBytes = brotli.Decompress(compressedPuzzleBytes);

			AppointmentContextMessage.PuzzleContext puzzleContext = new AppointmentContextMessage.PuzzleContext();
			using IDataRehydrator puzzleRehydrator = DataSerializationFactory.CreateRehydrator(puzzleBytes);
			puzzleContext.Rehydrate(puzzleRehydrator);

			// ok, we have our puzzle!! now, let's decrypt our open context

			int appointmentKeyHash = AppointmentUtils.GetAppointmentKeyHash(key);

			(var secretPackagePassword, var secretPackageSalt) = AppointmentUtils.RebuildAppointmentApplicantPackagePassword(appointmentId, appointmentDate, appointmentKeyHash);
			using SafeArrayHandle compressedSecretPackageBytes = AppointmentUtils.Decrypt(SafeArrayHandle.Wrap(distilledAppointmentContext.PackageBytes), secretPackagePassword, secretPackageSalt);
			using var secretPackageBytes = brotli.Decompress(compressedSecretPackageBytes);

			AppointmentContextMessage.ApplicantEntry applicantEntry = new AppointmentContextMessage.ApplicantEntry();
			using IDataRehydrator applicantRehydrator = DataSerializationFactory.CreateRehydrator(secretPackageBytes);
			applicantEntry.Rehydrate(applicantRehydrator);

			// ok, we are this far, we have our package and our initial validators to contact. lets contact the validators to obtain our key to continue and open the rest of the package

			long appointmentIndex = account.AccountAppointment.AppointmentIndex.Value;

			SafeArrayHandle validatorCode = null;

			bool timeRecorded = false;
			foreach(var validator in applicantEntry.Validators) {
				// this one has to remain sequential, we only need one answer.

				try {
					var validatorProtocol = this.centralCoordinator.ChainComponentProvider.ChainFactoryProviderBase.ChainTypeCreationFactoryBase.CreateValidatorProtocol(this.centralCoordinator.ChainId);

					validatorCode = await Repeater.RepeatAsync(async () => {
						
						return await validatorProtocol.RequestCodeTranslation(appointmentDate, appointmentIndex, validator.SecretCode, IPUtils.GuidToIP(validator.IP), validator.ValidationPort).ConfigureAwait(false);
					}, 2).ConfigureAwait(false);

					timeRecorded = true;
				} catch(Exception ex) {
					Log.Error(ex, $"Failed to contact validator with IP {validator.IP}:{validator.ValidationPort}");
				}

				if(validatorCode != null && !validatorCode.IsZero) {
					break;
				}
			}

			if(validatorCode == null || validatorCode.IsZero) {
				// this is a serious issue, no validator responded. we are done
				//TODO: add event
				Log.Error( $"Failed to contact validators. Cannot continue.");
				return;
			}

			
			if(timeRecorded) {
				this.UpdateAppointmentExpiration(account, window);
			}
			// good, now we can open the rest of the package

			(var secretInnerPackagePassword, var secretInnerPackageSalt) = AppointmentUtils.RebuildAppointmentApplicantSecretPackagePassword(appointmentId, validatorCode);

			using SafeArrayHandle compressedInnerSecretPackageBytes = AppointmentUtils.Decrypt(applicantEntry.Secret, secretInnerPackagePassword, secretInnerPackageSalt);
			using var innerSecretPackageBytes = brotli.Decompress(compressedInnerSecretPackageBytes);

			AppointmentContextMessage.ApplicantSecretPackage applicantSecretEntry = new AppointmentContextMessage.ApplicantSecretPackage();
			using IDataRehydrator applicantSecretRehydrator = DataSerializationFactory.CreateRehydrator(innerSecretPackageBytes);
			applicantSecretEntry.Rehydrate(applicantSecretRehydrator);

			// OK!!  we are now officially ready to contact validators and begin the process
			var validators = new List<AppointmentContextMessage.ValidatorEntry>();
			validators.AddRange(applicantEntry.Validators);
			validators.AddRange(applicantSecretEntry.Validators);

			SafeArrayHandle verificationResponseSeed = AppointmentUtils.BuildSecretConfirmationCorrelationCodeSeed(applicantEntry.Validators.Select(e => e.IP).ToList(), applicantSecretEntry.Validators.Select(e => e.IP).ToList(), appointmentKeyHash, applicantSecretEntry.SecretCode);

			ConcurrentDictionary<Guid, ushort> validatorSecretCodesL2 = new ConcurrentDictionary<Guid, ushort>();

			await ParallelAsync.ForEach(validators, async item => {
				var validator = item.entry;

				try {
					var validatorProtocol = this.centralCoordinator.ChainComponentProvider.ChainFactoryProviderBase.ChainTypeCreationFactoryBase.CreateValidatorProtocol(this.centralCoordinator.ChainId);
					
					await Repeater.RepeatAsync(async () => {
						
						ushort secretCodeL2 = await validatorProtocol.TriggerSession(appointmentDate, appointmentIndex, applicantSecretEntry.SecretCode, IPUtils.GuidToIP(validator.IP), validator.ValidationPort).ConfigureAwait(false);

						validatorSecretCodesL2.TryAdd(validator.IP, secretCodeL2);
					}, 2).ConfigureAwait(false);
					
					timeRecorded = true;
				} catch(Exception ex) {
					Log.Error(ex, $"Failed to contact validator with IP {validator.IP}:{validator.ValidationPort}");
				}
			}).ConfigureAwait(false);

			if(timeRecorded) {
				this.UpdateAppointmentExpiration(account, window);
			}
			
			// ok, we received our validator L2 answers.  lets transform the secretCode
			int finalPuzzleCode = applicantSecretEntry.SecretCode;

			//TODO: here we should detect bad acting validators by using consensus
			foreach(var secretCodeL2 in validatorSecretCodesL2.Values.Distinct()) {
				finalPuzzleCode ^= secretCodeL2;
			}

			// OK, we can officially start the puzzle!

			// first thing, lets begin the POW in a thread
			var powTask = Task.Run(async () => {

				CPUPOWRulesSet powRuleSet = new CPUPOWRulesSet();
				powRuleSet.Rehydrate(SafeArrayHandle.Wrap(distilledAppointmentContext.POWRuleSet));

				CPUPowEngine powEngine = new CPUPowEngine(powRuleSet, true);

				using var powHash = AppointmentUtils.PreparePOWHash(key, finalPuzzleCode);

				var result = await powEngine.PerformPow(powHash, (currentNonce, difficulty) => {
					// we need this to play nice with the rest
					Thread.Sleep(10);
					return Task.CompletedTask;
				}).ConfigureAwait(false);

				return result;
			});

			// now, lets send the Puzzle to the GUI

			// prepare the puzzles
			List<(string puzzle, string instructions)> formattedPuzzles = new List<(string puzzle, string instructions)>();

			int index = 1;

			foreach(var puzzle in puzzleContext.Puzzles) {

				string translationTable = "";
				string instructions = "";

				var engine = AppointmentPuzzleEngineFactory.CreateEngine(puzzle.EngineVersion);
				
				if(puzzle.Instructions.ContainsKey(GlobalSettings.Instance.Locale)) {
					instructions = engine.PackageInstructions(puzzle.Instructions[GlobalSettings.Instance.Locale]);
				}
				
				if(puzzle.Locales.ContainsKey(GlobalSettings.Instance.Locale)) {
					translationTable = puzzle.Locales[GlobalSettings.Instance.Locale];
				}

				if(string.IsNullOrWhiteSpace(translationTable) && puzzle.Locales.ContainsKey(GlobalsService.DEFAULT_LOCALE)) {
					translationTable = puzzle.Locales[GlobalsService.DEFAULT_LOCALE];
				}

				formattedPuzzles.Add((await engine.PackagePuzzle(index, appointmentKeyHash, translationTable, puzzle.Code, puzzle.Libraries).ConfigureAwait(false), instructions));
				index += 1;
			}

			this.centralCoordinator.PostSystemEventImmediate(SystemEventGenerator.AppointmentPuzzleBegin(finalPuzzleCode, formattedPuzzles), this.correlationContext);

			if(this.PuzzleBeginEvent != null) {
				this.PuzzleBeginEvent(finalPuzzleCode, formattedPuzzles);
			}

			this.manualResetEvent = new ManualResetEventSlim();

			// now we wait for the answer
			var remainingTime = this.hardStop - DateTimeEx.CurrentTime + TimeSpan.FromSeconds(5);
			if(!this.AnswersSet && !this.manualResetEvent.Wait(remainingTime)) {
				// we got no answer in time.
				return;
			}

			if(!this.AnswersSet) {
				// we got no answer in time.
				return;
			}

			var powResult = await powTask.ConfigureAwait(false);

			this.centralCoordinator.PostSystemEventImmediate(BlockchainSystemEventTypes.Instance.AppointmentPuzzleCompleted, this.correlationContext);

			//TODO: this should be more robust
			await ParallelAsync.ForEach(validators, async item => {
				var validator = item.entry;
				
				try {
					
					var results = new Dictionary<Enums.AppointmentsResultTypes, SafeArrayHandle>();
					List<int> puzzleAnswers = new List<int>();
					puzzleAnswers.Add(finalPuzzleCode);
					puzzleAnswers.AddRange(this.answers);
					results.Add(Enums.AppointmentsResultTypes.Puzzle, AppointmentsResultTypeSerializer.SerializePuzzleResult(puzzleAnswers));
					results.Add(Enums.AppointmentsResultTypes.POW, AppointmentsResultTypeSerializer.SerializePOW(powResult.nonce, powResult.solution));
					results.Add(Enums.AppointmentsResultTypes.SecretCodeL2, AppointmentsResultTypeSerializer.SerializeSecretCodeL2(validatorSecretCodesL2[validator.IP]));

					var validatorProtocol = this.centralCoordinator.ChainComponentProvider.ChainFactoryProviderBase.ChainTypeCreationFactoryBase.CreateValidatorProtocol(this.centralCoordinator.ChainId);

					await Repeater.RepeatAsync(async () => {
						
						await validatorProtocol.CompleteSession(appointmentDate, appointmentIndex, results, IPUtils.GuidToIP(validator.IP), validator.ValidationPort).ConfigureAwait(false);
					}, 2).ConfigureAwait(false);
				} catch(Exception ex) {
					Log.Error(ex, $"Failed to contact validator with IP {validator.IP}:{validator.ValidationPort}");
				}
			}).ConfigureAwait(false);

			// we are done! :D
			await walletProvider.CleanSynthesizedBlockCache(lockContext).ConfigureAwait(false);

			await walletProvider.ScheduleTransaction(async (provider, token, lc) => {

				account.AccountAppointment.AppointmentContextDetailsCached = false;
				account.AccountAppointment.AppointmentStatus = Enums.AppointmentStatus.AppointmentPuzzleCompleted;
				account.AccountAppointment.VerificationResponseSeed = verificationResponseSeed.ToExactByteArrayCopy();

				return true;
			}, lockContext).ConfigureAwait(false);

			this.centralCoordinator.PostSystemEventImmediate(BlockchainSystemEventTypes.Instance.AppointmentVerificationRequestCompleted, this.correlationContext);

			//return this.centralCoordinator.ChainComponentProvider.WalletProviderBase.LoadWallet(this.correlationContext, lockContext, this.passphrase);
		}

		private bool AnswersSet => this.answers != null && this.answers.Any();

		public void SetAnswers(List<int> answers) {

			this.answers = answers;
			this.manualResetEvent?.Set();
		}

		protected override Task DisposeAllAsync() {
			return base.DisposeAllAsync();

			this.manualResetEvent?.Dispose();
		}
	}
}