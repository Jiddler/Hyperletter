using System;

namespace Hyperletter.Core.Dispatcher {
    public interface ITransportSerializer {
        byte[] Serialize(object obj);
        T Deserialize<T>(byte[] value);
        T Deserialize<T>(byte[] value, Type concreteType);
    }
}