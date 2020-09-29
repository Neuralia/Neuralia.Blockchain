using System;

namespace Neuralia.Blockchains.Common.Classes.Blockchains.Common.Tools.Appointments {
	public static class AppointmentPuzzleEngineFactory {
		public static AppointmentPuzzleEngineBase CreateEngine(int version) {

			if(version == 1) {
				return new AppointmentPuzzleEngine1();
			}
			throw new NotImplementedException();
		}
	}
}