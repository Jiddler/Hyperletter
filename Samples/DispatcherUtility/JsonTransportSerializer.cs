using System;
using System.Text;
using Hyperletter.Dispatcher;
using Newtonsoft.Json;

namespace DispatcherUtility {
    public class JsonTransportSerializer : ITransportSerializer {
        public byte[] Serialize(object obj) {
            return Encoding.UTF8.GetBytes((string) JsonConvert.SerializeObject(obj));
        }

        public T Deserialize<T>(byte[] value) {
            return JsonConvert.DeserializeObject<T>(Encoding.UTF8.GetString(value));
        }

        public T Deserialize<T>(byte[] value, Type concreteType) {
            return (T) JsonConvert.DeserializeObject(Encoding.UTF8.GetString(value), concreteType);
        }
    }
}