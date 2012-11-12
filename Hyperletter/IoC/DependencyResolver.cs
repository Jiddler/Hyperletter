using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;

namespace Hyperletter.IoC {
    public class DependencyResolver<TService> : Resolver {
        private readonly Container _container;
        private bool _singleton;
        private Type _type;
        private ConstructorInfo _constructor;
        private ParameterInfo[] _parameters;
        private bool _prepared;
        private object _instance;
        private readonly Dictionary<string, object> _values = new Dictionary<string, object>();

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
            var arguments = BuildArguments(parameters);
            return _constructor.Invoke(arguments);
        }

        public DependencyResolver<TService> AsSingleton() {
            _singleton = true;
            return this;
        }

        public DependencyResolver<TService> WithValue(string parameterName, object value) {
            _values[parameterName] = value;

            return this;
        }

        private object[] BuildArguments(IList<object> parameters) {
            var parameterIndex = 0;
            var constructorParameterIndex = 0;
            var arguments = new object[_parameters.Length];
            foreach(var p in _parameters) {
                object obj;
                if(_values.TryGetValue(p.Name, out obj)) {
                    arguments[constructorParameterIndex++] = obj;
                } else if(parameters.Count > parameterIndex && p.ParameterType.IsInstanceOfType(parameters[parameterIndex])) {
                    arguments[constructorParameterIndex++] = parameters[parameterIndex++];
                } else {
                    if (_container.TryResolve(p.ParameterType, out obj))
                        arguments[constructorParameterIndex++] = obj;
                    else if (IsFunc(p.ParameterType) && _container.IsRegistered(GetReturnType(p.ParameterType)))
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
            if (type.IsGenericTypeDefinition)
                generic = type;
            else if (type.IsGenericType)
                generic = type.GetGenericTypeDefinition();
            
            if (generic == null)
                return false;

            if (generic == typeof(Func<>)
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
            var invokeMethod = type.GetMethod("Invoke");

            var target = _container.GetType().GetMethod("Resolve", new[] { typeof(object[]) });
            target = target.MakeGenericMethod(invokeMethod.ReturnType);

            var t = Expression.Constant(_container);

            var parameters = invokeMethod
              .GetParameters()
              .Select(pi => Expression.Parameter(pi.ParameterType, pi.Name))
              .ToList();

            var convertedParameters = (IEnumerable<Expression>) parameters.Select(p => Expression.Convert(p, typeof(object)));
            var parametersExpression = Expression.NewArrayInit(typeof(object), convertedParameters);
            var body = Expression.Call(t, target, parametersExpression);
            return Expression.Lambda(type, body, parameters).Compile();
        }
    }
}