using System;
using Enklu.Mycelium.Messages;
using Enklu.Mycerializer;

namespace Enklu.Mamba.Network
{
    public class TypeBinder : ITypeBinder
    {
        public ushort Id(Type type)
        {
            return MyceliumMessagesMap.Get(type);
        }

        public Type Type(ushort id)
        {
            return MyceliumMessagesMap.Get(id);
        }
    }
}