namespace Neuralia.Blockchains.Core.Workflows.Tasks {
	public interface IColoredTask : IBasicTask<object> {
	}

	/// <summary>
	///     inherit this message type to filter events based on message type, not on actions
	/// </summary>
	public abstract class ColoredTask : BasicTask<object>, IColoredTask {
	}
}