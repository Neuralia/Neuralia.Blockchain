using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace Neuralia.Blockchains.Common.Classes.Blockchains.Common.Tools.Appointments {
	public class AppointmentPuzzleEngine1 : AppointmentPuzzleEngineBase {
		protected override int EngineVersion => 1;
		protected override string GetPath => $"v{EngineVersion}";
    }
}