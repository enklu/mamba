using System;
using Enklu.Mycelium.Messages;
using Enklu.Mycerializer;

namespace Enklu.Mamba.Network
{
    /// <summary>
    /// Type binder with mycelium messages.
    /// </summary>
    public class TypeBinder : ITypeBinder
    {
        /// <inheritdoc />
        public ushort Id(Type type)
        {
            return MyceliumMessagesMap.Get(type);
        }

        /// <inheritdoc />
        public Type Type(ushort id)
        {
            return MyceliumMessagesMap.Get(id);
        }
    }
}