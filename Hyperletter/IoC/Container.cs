using System;
using System.Collections.Generic;

namespace Hyperletter.IoC {
    public class Container {
        private readonly Dictionary<Type, Resolver> _services = new Dictionary<Type,Resolver>();

        public bool AutoRegister { get; set; }

        public Container() {
            RegisterInstance(this);
        }

        public DependencyResolver<TConcrete> Register<TConcrete>() {
            return Register<TConcrete, TConcrete>();
        }

        public DependencyResolver<TConcrete> Register<TService, TConcrete>() where TConcrete : TService {
            var resolver = new DependencyResolver<TConcrete>(this);
            _services[typeof(TService)] = resolver;
            return resolver;
        }

        public void RegisterInstance<TService>(TService instance) {
            RegisterInstance<TService, TService>(instance);
        }

        public void RegisterInstance<TService, TConcrete>(TConcrete instance) {
            _services[typeof(TService)] = new InstanceResolver<TConcrete>(instance);
        }

        public TService Resolve<TService>(params object[] parameters) where TService : class {
            return Resolve<TService>(typeof(TService), parameters);
        }

        public TService Resolve<TService>(Type type, params object[] parameters) where TService : class {
            Resolver dependencyManager;
            if (!_services.TryGetValue(type, out dependencyManager) && !AutoRegister) {
                throw new NoRegistrationException(type.FullName + " is not registerd");
            }

            if(dependencyManager == null) {
                dependencyManager = new DependencyResolver<TService>(this);
                _services[typeof(TService)] = dependencyManager;
            }

            return (TService)dependencyManager.Resolve(parameters);
        }

        public bool TryResolve<TService>(Type type, out TService service, params object[] parameters) where TService : class {
            Resolver dependencyManager;
            if (!_services.TryGetValue(type, out dependencyManager)) {
                service = null;
                return false;
            }

            service = (TService)dependencyManager.Resolve(parameters);
            return true;
        }

        public bool IsRegistered(Type type) {
            return _services.ContainsKey(type);
        }

        public void Build() {
            foreach(var service in _services.Values)
                service.Build();
        }
    }
}
