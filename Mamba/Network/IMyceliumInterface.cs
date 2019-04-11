using Enklu.Data;

namespace Enklu.Mamba.Network
{
    /// <summary>
    /// Simple interface for sending network events.
    /// </summary>
    public interface IMyceliumInterface
    {
        /// <summary>
        /// Sends element actions.
        /// </summary>
        /// <param name="actions">The actions to send.</param>
        void Send(ElementActionData[] actions);
    }
}