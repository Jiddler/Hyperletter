using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;

namespace Hyperletter.IoC {
    public class DependencyResolver<TService> : Resolver {
        private readonly Container _container;
        private readonly Dictionary<Type, object> _factoryCache = new Dictionary<Type, object>();
        private readonly Dictionary<string, object> _values = new Dictionary<string, object>();
        private Action<TService> _activatedCallback;
        private bool _autoResolveOnBuild;
        private ConstructorInfo _constructor;
        private volatile object _instance;
        private ParameterInfo[] _parameters;
        private bool _prepared;
        private bool _singleton;
        private Type _type;

        public DependencyResolver(Container container) {
            _container = container;
        }

        private void PrepareMap() {
            if(_prepared)
                return;

            lock(this) {
                if(_prepared)
                    return;

                _type = typeof(TService);
                _constructor = _type.GetConstructors().First();
                _parameters = _constructor.GetParameters();

                _prepared = true;
            }
        }

        public override object Resolve(params object[] parameters) {
            PrepareMap();

            if(!_singleton) {
                return CreateInstance(parameters);
            }

            if(_instance != null)
                return _instance;

            lock(this) {
                if(_instance != null)
                    return _instance;

                _instance = CreateInstance(parameters);
            }

            return _instance;
        }

        private object CreateInstance(IList<object> parameters) {
            object[] arguments = BuildArguments(parameters);
            var service = (TService) _constructor.Invoke(arguments);
            if(_activatedCallback != null)
                _activatedCallback(service);

            return service;
        }

        public DependencyResolver<TService> AsSingleton() {
            _singleton = true;
            return this;
        }

        public DependencyResolver<TService> AutoResolveOnBuild() {
            _autoResolveOnBuild = true;

            return this;
        }

        public DependencyResolver<TService> OnActivated(Action<TService> activatedCallback) {
            _activatedCallback = activatedCallback;
            return this;
        }

        public DependencyResolver<TService> WithValue(string parameterName, object value) {
            _values[parameterName] = value;

            return this;
        }

        public override void Build() {
            if(_autoResolveOnBuild)
                Resolve();
        }

        private object[] BuildArguments(IList<object> parameters) {
            int parameterIndex = 0;
            int constructorParameterIndex = 0;
            var arguments = new object[_parameters.Length];
            foreach(ParameterInfo p in _parameters) {
                object obj;
                if(_values.TryGetValue(p.Name, out obj)) {
                    arguments[constructorParameterIndex++] = obj;
                } else if(parameters.Count > parameterIndex && p.ParameterType.IsInstanceOfType(parameters[parameterIndex])) {
                    arguments[constructorParameterIndex++] = parameters[parameterIndex++];
                } else {
                    if(_container.TryResolve(p.ParameterType, out obj))
                        arguments[constructorParameterIndex++] = obj;
                    else if(IsFunc(p.ParameterType) && _container.IsRegistered(GetReturnType(p.ParameterType)))
                        arguments[constructorParameterIndex++] = GenerateFactory(p.ParameterType);
                    else
                        throw new ResolveException("Cant find parameter " + p.Name + " (" + p.ParameterType + ") in arguments or registerd in container");
                }
            }
            return arguments;
        }

        private Type GetReturnType(Type type) {
            return type.GetMethod("Invoke").ReturnType;
        }

        private bool IsFunc(Type type) {
            Type generic = null;
            if(type.IsGenericTypeDefinition)
                generic = type;
            else if(type.IsGenericType)
                generic = type.GetGenericTypeDefinition();

            if(generic == null)
                return false;

            if(generic == typeof(Func<>)
               || generic == typeof(Func<,>)
               || generic == typeof(Func<,,>)
               || generic == typeof(Func<,,,>)
               || generic == typeof(Func<,,,,>)
               || generic == typeof(Func<,,,,,>)
               || generic == typeof(Func<,,,,,,>)
               || generic == typeof(Func<,,,,,,,>)
               || generic == typeof(Func<,,,,,,,,>)
               || generic == typeof(Func<,,,,,,,,,>)
               || generic == typeof(Func<,,,,,,,,,,>)
               || generic == typeof(Func<,,,,,,,,,,,>)
               || generic == typeof(Func<,,,,,,,,,,,,>)
               || generic == typeof(Func<,,,,,,,,,,,,,>)
               || generic == typeof(Func<,,,,,,,,,,,,,,>)
               || generic == typeof(Func<,,,,,,,,,,,,,,,>)
               || generic == typeof(Func<,,,,,,,,,,,,,,,,>))
                return true;

            return false;
        }

        private object GenerateFactory(Type type) {
            object factory;
            if(_factoryCache.TryGetValue(type, out factory))
                return factory;

            MethodInfo invokeMethod = type.GetMethod("Invoke");

            MethodInfo target = _container.GetType().GetMethod("Resolve", new[] {typeof(object[])});
            target = target.MakeGenericMethod(invokeMethod.ReturnType);

            ConstantExpression t = Expression.Constant(_container);

            List<ParameterExpression> parameters = invokeMethod
                .GetParameters()
                .Select(pi => Expression.Parameter(pi.ParameterType, pi.Name))
                .ToList();

            var convertedParameters = (IEnumerable<Expression>) parameters.Select(p => Expression.Convert(p, typeof(object)));
            NewArrayExpression parametersExpression = Expression.NewArrayInit(typeof(object), convertedParameters);
            MethodCallExpression body = Expression.Call(t, target, parametersExpression);
            return Expression.Lambda(type, body, parameters).Compile();
        }
    }
}