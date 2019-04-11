using Enklu.Data;

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
        void Create(string parentId, ElementData element);

        /// <summary>
        /// Sends update actions.
        /// </summary>
        /// <param name="actions">The actions to send.</param>
        void Update(ElementActionData[] actions);

        /// <summary>
        /// Destroys an element.
        /// </summary>
        /// <param name="id">The id.</param>
        void Destroy(string id);
    }
}