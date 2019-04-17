using System.Threading.Tasks;
using Enklu.Data;
using Enklu.Mycelium.Messages.Experience;

namespace Enklu.Mamba.Network
{
    /// <summary>
    /// Simple interface for sending network events.
    /// </summary>
    public interface IMyceliumInterface
    {
        /// <summary>
        /// Creates an element.
        /// </summary>
        /// <param name="parentId">The parent to attach this element to.</param>
        /// <param name="element">The data for the element to create.</param>
        Task<ElementData> Create(
            string parentId,
            ElementData element,
            string owner = null,
            ElementExpirationType expiration = ElementExpirationType.Session);

        /// <summary>
        /// Sends update actions.
        /// </summary>
        /// <param name="actions">The actions to send.</param>
        void Update(ElementActionData[] actions);

        /// <summary>
        /// Destroys an element.
        /// </summary>
        /// <param name="id">The id.</param>
        Task Destroy(string id);
    }
}