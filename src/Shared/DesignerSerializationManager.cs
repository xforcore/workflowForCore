// workflowForCore's own code (MIT). Licensed under the MIT license; see LICENSE.
//
// WF3 creates a System.ComponentModel.Design.Serialization.DesignerSerializationManager for every markup (XOML and
// .rules) load and save: WorkflowMarkupSerializer.Deserialize/Serialize, the runtime's CreateWorkflow(XmlReader), the
// default loader service, WorkflowChanges, the SQL tracking service's definitions and the rules' helpers. On .NET
// Framework the class is in System.Design; on .NET it is in Windows Forms' design assembly (Windows only), and the
// Reference Source doesn't have it. This is a smaller one with the behaviour WF3 relies on, as .NET Framework documents
// it: sessions, the context stack, errors, serialization providers, serializers from [DesignerSerializer] and
// [DefaultSerializationProvider], named instances and services. WF3 only ever calls it through
// IDesignerSerializationManager, CreateSession and Errors (docs/MODIFICATIONS.md, "Classes the Reference Source
// doesn't have").
//
// It is internal, and compiled into each of the three assemblies that create one.
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.Reflection;
using System.Runtime.Serialization;

namespace System.ComponentModel.Design.Serialization
{
    internal class DesignerSerializationManager : IDesignerSerializationManager
    {
        private readonly IServiceProvider provider;
        private Session session;
        private readonly List<IDesignerSerializationProvider> providers = new List<IDesignerSerializationProvider>();
        private readonly HashSet<Type> defaultProviders = new HashSet<Type>();

        public DesignerSerializationManager()
        {
        }

        public DesignerSerializationManager(IServiceProvider provider)
        {
            this.provider = provider ?? throw new ArgumentNullException(nameof(provider));
        }

        // What a session holds. A new session starts empty.
        private sealed class Session
        {
            internal readonly ContextStack Context = new ContextStack();
            internal readonly ArrayList Errors = new ArrayList();
            internal readonly Dictionary<string, object> Instances = new Dictionary<string, object>(StringComparer.Ordinal);
            internal readonly Dictionary<object, string> Names = new Dictionary<object, string>(ReferenceEqualityComparer.Instance);
            internal readonly Dictionary<(Type, Type), object> Serializers = new Dictionary<(Type, Type), object>();
        }

        private sealed class SessionScope : IDisposable
        {
            private DesignerSerializationManager manager;

            internal SessionScope(DesignerSerializationManager manager)
            {
                this.manager = manager;
            }

            public void Dispose()
            {
                DesignerSerializationManager m = this.manager;
                this.manager = null;
                m?.EndSession();
            }
        }

        public IContainer Container => GetService(typeof(IDesignerHost)) is IDesignerHost host ? host.Container : null;

        public IList Errors
        {
            get
            {
                CheckSession();
                return this.session.Errors;
            }
        }

        public IDisposable CreateSession()
        {
            if (this.session != null)
                throw new InvalidOperationException("A serialization session is already active.");
            this.session = new Session();
            return new SessionScope(this);
        }

        private void EndSession()
        {
            try
            {
                SerializationComplete?.Invoke(this, EventArgs.Empty);
            }
            finally
            {
                this.session = null;
                ResolveName = null;
                SerializationComplete = null;
            }
        }

        private void CheckSession()
        {
            if (this.session == null)
                throw new InvalidOperationException("There is no serialization session. Call CreateSession first.");
        }

        public object GetService(Type serviceType)
        {
            if (serviceType == null)
                throw new ArgumentNullException(nameof(serviceType));
            if (serviceType == typeof(IDesignerSerializationManager) || serviceType == typeof(DesignerSerializationManager))
                return this;
            if (serviceType == typeof(IContainer))
                return Container;
            return this.provider?.GetService(serviceType);
        }

        #region IDesignerSerializationManager

        public ContextStack Context
        {
            get
            {
                CheckSession();
                return this.session.Context;
            }
        }

        public PropertyDescriptorCollection Properties => PropertyDescriptorCollection.Empty;

        public event ResolveNameEventHandler ResolveName;

        public event EventHandler SerializationComplete;

        public void AddSerializationProvider(IDesignerSerializationProvider provider)
        {
            if (provider == null)
                throw new ArgumentNullException(nameof(provider));
            if (!this.providers.Contains(provider))
                this.providers.Add(provider);
        }

        public void RemoveSerializationProvider(IDesignerSerializationProvider provider)
        {
            this.providers.Remove(provider);
        }

        public object CreateInstance(Type type, ICollection arguments, string name, bool addToContainer)
        {
            if (type == null)
                throw new ArgumentNullException(nameof(type));
            CheckSession();
            if (name != null && this.session.Instances.ContainsKey(name))
                throw new SerializationException($"Duplicate declaration of member '{name}'");

            object[] args = null;
            if (arguments != null && arguments.Count > 0)
            {
                args = new object[arguments.Count];
                arguments.CopyTo(args, 0);
            }
            object instance = CreateInstance(type, args);

            if (name != null)
            {
                this.session.Instances[name] = instance;
                this.session.Names[instance] = name;
            }
            if (addToContainer && instance is IComponent component && Container is IContainer container)
                container.Add(component, name);
            return instance;
        }

        private static object CreateInstance(Type type, object[] args)
        {
            try
            {
                return TypeDescriptor.CreateInstance(null, type, null, args);
            }
            catch (MissingMethodException)
            {
                // Arguments that aren't of the parameters' types: converted, as the designer's manager converts them,
                // with the parameter type's converter.
                if (args == null)
                    throw;
                foreach (ConstructorInfo ctor in type.GetConstructors(BindingFlags.Public | BindingFlags.Instance))
                {
                    ParameterInfo[] parameters = ctor.GetParameters();
                    if (parameters.Length != args.Length)
                        continue;
                    object[] converted = new object[args.Length];
                    bool ok = true;
                    for (int i = 0; ok && i < args.Length; i++)
                    {
                        Type p = parameters[i].ParameterType;
                        if (args[i] == null || p.IsInstanceOfType(args[i]))
                        {
                            converted[i] = args[i];
                            continue;
                        }
                        TypeConverter converter = TypeDescriptor.GetConverter(p);
                        if (converter.CanConvertFrom(args[i].GetType()))
                            converted[i] = converter.ConvertFrom(null, Globalization.CultureInfo.InvariantCulture, args[i]);
                        else if (args[i] is IConvertible && typeof(IConvertible).IsAssignableFrom(p))
                            converted[i] = Convert.ChangeType(args[i], p, Globalization.CultureInfo.InvariantCulture);
                        else
                            ok = false;
                    }
                    if (ok)
                        return ctor.Invoke(converted);
                }
                throw;
            }
        }

        public object GetInstance(string name)
        {
            if (name == null)
                throw new ArgumentNullException(nameof(name));
            CheckSession();
            if (this.session.Instances.TryGetValue(name, out object instance))
                return instance;
            if (Container is IContainer container && container.Components[name] is IComponent component)
                return component;
            if (ResolveName != null)
            {
                var e = new ResolveNameEventArgs(name);
                ResolveName(this, e);
                return e.Value;
            }
            return null;
        }

        public string GetName(object value)
        {
            if (value == null)
                throw new ArgumentNullException(nameof(value));
            CheckSession();
            if (this.session.Names.TryGetValue(value, out string name))
                return name;
            return (value as IComponent)?.Site?.Name;
        }

        public void SetName(object instance, string name)
        {
            if (instance == null)
                throw new ArgumentNullException(nameof(instance));
            if (name == null)
                throw new ArgumentNullException(nameof(name));
            CheckSession();
            if (this.session.Instances.ContainsKey(name))
                throw new ArgumentException($"The name '{name}' is already in use by another object.", nameof(name));
            if (this.session.Names.ContainsKey(instance))
                throw new ArgumentException("The object already has a name.", nameof(instance));
            this.session.Instances[name] = instance;
            this.session.Names[instance] = name;
        }

        public void ReportError(object errorInformation)
        {
            CheckSession();
            if (errorInformation != null)
                this.session.Errors.Add(errorInformation);
        }

        public Type GetType(string typeName)
        {
            if (typeName == null)
                return null;
            if (GetService(typeof(ITypeResolutionService)) is ITypeResolutionService resolution)
                return resolution.GetType(typeName);
            return Type.GetType(typeName);
        }

        // The serializer for objects of objectType, of type serializerType: the session's, if one was made for them in
        // this session; else the one [DesignerSerializer] on objectType names for serializerType; and then each
        // serialization provider in turn, the ones added and the one [DefaultSerializationProvider] on serializerType
        // names, may replace it, until none replaces it any more.
        public object GetSerializer(Type objectType, Type serializerType)
        {
            if (serializerType == null)
                throw new ArgumentNullException(nameof(serializerType));

            object serializer = null;
            if (objectType != null)
            {
                if (this.session != null && this.session.Serializers.TryGetValue((objectType, serializerType), out serializer))
                    return serializer;

                foreach (Attribute attribute in TypeDescriptor.GetAttributes(objectType))
                {
                    if (attribute is DesignerSerializerAttribute designerSerializer)
                    {
                        Type baseType = GetRuntimeType(designerSerializer.SerializerBaseTypeName);
                        if (baseType != serializerType)
                            continue;
                        Type type = GetRuntimeType(designerSerializer.SerializerTypeName);
                        if (type != null)
                        {
                            serializer = Activator.CreateInstance(type);
                            break;
                        }
                    }
                }
            }

            if (serializer == null && this.defaultProviders.Add(serializerType))
            {
                foreach (Attribute attribute in TypeDescriptor.GetAttributes(serializerType))
                {
                    if (attribute is DefaultSerializationProviderAttribute defaultProvider)
                    {
                        Type type = GetRuntimeType(defaultProvider.ProviderTypeName);
                        if (type != null && typeof(IDesignerSerializationProvider).IsAssignableFrom(type))
                            AddSerializationProvider((IDesignerSerializationProvider)Activator.CreateInstance(type));
                        break;
                    }
                }
            }

            bool changed = true;
            for (int pass = 0; changed && pass < this.providers.Count; pass++)
            {
                changed = false;
                foreach (IDesignerSerializationProvider p in this.providers.ToArray())
                {
                    object replacement = p.GetSerializer(this, serializer, objectType, serializerType);
                    if (replacement != null)
                    {
                        changed = !ReferenceEquals(replacement, serializer);
                        serializer = replacement;
                    }
                }
            }

            if (serializer != null && objectType != null && this.session != null)
                this.session.Serializers[(objectType, serializerType)] = serializer;
            return serializer;
        }

        // A type named in an attribute: by the type resolution service if there is one, else by its assembly-qualified
        // name.
        private Type GetRuntimeType(string typeName)
        {
            if (string.IsNullOrEmpty(typeName))
                return null;
            return GetType(typeName) ?? Type.GetType(typeName);
        }

        #endregion
    }
}
