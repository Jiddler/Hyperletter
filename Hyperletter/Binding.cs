using System.Net;

namespace Hyperletter {
    public class Binding {
        public IPAddress IpAddress { get; private set; }
        public int Port { get; private set; }

        public Binding(IPAddress ipAddress, int port) {
            IpAddress = ipAddress;
            Port = port;
        }

        protected bool Equals(Binding other) {
            return IpAddress.Equals(other.IpAddress) && Port == other.Port;
        }

        public override bool Equals(object obj) {
            if (ReferenceEquals(null, obj)) return false;
            if (ReferenceEquals(this, obj)) return true;
            if (obj.GetType() != this.GetType()) return false;
            return Equals((Binding) obj);
        }

        public override int GetHashCode() {
            unchecked {
                return (IpAddress.GetHashCode() * 397) ^ Port;
            }
        }

        public override string ToString() {
            return IpAddress + ":" + Port;
        }
    }
}