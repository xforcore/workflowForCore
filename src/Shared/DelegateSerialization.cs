// workflowForCore's own code (MIT). Licensed under the MIT license; see LICENSE.
//
// Delegates in WF3's instance state, in .NET Framework's wire format. WF3 keeps an activity's event handlers
// (CodeActivity.ExecuteCode, CodeCondition.Condition, ...) in the activity's instance values, so every Activity.Save,
// and so every While iteration, state transition, Replicator child and persisted instance, serializes delegates.
// .NET's delegates throw from GetObjectData ("Serializing delegates is not supported on this platform"), and
// webForCore's BinaryFormatter has no surrogate for them (yet), so workflowForCore's formatter adds one.
//
// It writes what .NET Framework 4.8 writes (microsoft/referencesource mscorlib/system/multicastdelegate.cs,
// GetObjectData, and delegateserializationholder.cs): a System.DelegateSerializationHolder (mscorlib) with a "Delegate"
// chain of System.DelegateSerializationHolder+DelegateEntry records, one per delegate of the invocation list from the
// last to the first, each target as "target<n>" and each method as "method<n>". It reads the same records (written by
// .NET Framework or by itself) through a binder that maps those two mscorlib names to the classes below, and makes the
// delegate with Delegate.CreateDelegate, as the holder's GetRealObject does.
using System;
using System.Collections.Generic;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.Serialization;
using System.Text;

#pragma warning disable SYSLIB0050, SYSLIB0051

namespace WorkflowForCore.Serialization
{
    internal static class DelegateSerialization
    {
        internal const string Mscorlib = "mscorlib, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089";
        internal const string HolderName = "System.DelegateSerializationHolder";
        internal const string EntryName = "System.DelegateSerializationHolder+DelegateEntry";

        // A type's name and assembly as .NET Framework writes them: types that moved from mscorlib or System
        // ([TypeForwardedFrom]) under their .NET Framework assembly, generic arguments too.
        internal static string AssemblyName(Type type)
        {
            Type definition = type.IsGenericType ? type.GetGenericTypeDefinition() : type;
            var forwarded = definition.GetCustomAttribute<TypeForwardedFromAttribute>(false);
            if (forwarded != null)
                return forwarded.AssemblyFullName;
            // .NET's core library has what was mscorlib's, not all of it marked [TypeForwardedFrom] (EventHandler<T> isn't).
            return definition.Assembly == typeof(object).Assembly ? Mscorlib : definition.Assembly.FullName;
        }

        internal static string FullName(Type type)
        {
            if (!type.IsGenericType || type.IsGenericTypeDefinition)
                return type.FullName;
            var name = new StringBuilder(type.GetGenericTypeDefinition().FullName);
            name.Append('[');
            Type[] arguments = type.GetGenericArguments();
            for (int i = 0; i < arguments.Length; i++)
            {
                if (i > 0) name.Append(',');
                name.Append('[').Append(FullName(arguments[i])).Append(", ").Append(AssemblyName(arguments[i])).Append(']');
            }
            return name.Append(']').ToString();
        }
    }

    // Consulted before the caller's selector for delegates and for the delegate entries this writes.
    internal sealed class DelegateSurrogateSelector : ISurrogateSelector
    {
        private static readonly DelegateSurrogate delegateSurrogate = new DelegateSurrogate();
        private static readonly EntrySurrogate entrySurrogate = new EntrySurrogate();
        private ISurrogateSelector next;

        internal DelegateSurrogateSelector(ISurrogateSelector next)
        {
            this.next = next;
        }

        internal ISurrogateSelector Next => this.next;

        public void ChainSelector(ISurrogateSelector selector) => this.next = selector;

        public ISurrogateSelector GetNextSelector() => this.next;

        public ISerializationSurrogate GetSurrogate(Type type, StreamingContext context, out ISurrogateSelector selector)
        {
            if (typeof(Delegate).IsAssignableFrom(type) && type != typeof(Delegate) && type != typeof(MulticastDelegate))
            {
                selector = this;
                return delegateSurrogate;
            }
            if (type == typeof(DelegateEntry))
            {
                selector = this;
                return entrySurrogate;
            }
            if (this.next != null)
                return this.next.GetSurrogate(type, context, out selector);
            selector = null;
            return null;
        }

        private sealed class DelegateSurrogate : ISerializationSurrogate
        {
            public void GetObjectData(object obj, SerializationInfo info, StreamingContext context)
            {
                Delegate[] list = ((Delegate)obj).GetInvocationList();
                info.FullTypeName = DelegateSerialization.HolderName;
                info.AssemblyName = DelegateSerialization.Mscorlib;
                DelegateEntry previous = null;
                int index = 0;
                for (int i = list.Length; --i >= 0;)
                {
                    Delegate d = list[i];
                    MethodInfo method = d.Method;
                    if (method.DeclaringType == null)
                        throw new NotSupportedException("Serialization of global methods (including implicit serialization via the use of asynchronous delegates) is not supported.");
                    Type type = d.GetType();
                    var entry = new DelegateEntry
                    {
                        type = DelegateSerialization.FullName(type),
                        assembly = DelegateSerialization.AssemblyName(type),
                        target = d.Target,
                        targetTypeAssembly = DelegateSerialization.AssemblyName(method.ReflectedType),
                        targetTypeName = DelegateSerialization.FullName(method.ReflectedType),
                        methodName = method.Name,
                    };
                    if (info.MemberCount == 0)
                        info.AddValue("Delegate", entry, typeof(object));
                    if (entry.target != null)
                    {
                        string targetName = "target" + index;
                        info.AddValue(targetName, entry.target);
                        entry.target = targetName;
                    }
                    info.AddValue("method" + index, method);
                    if (previous != null)
                        previous.delegateEntry = entry;
                    previous = entry;
                    index++;
                }
            }

            public object SetObjectData(object obj, SerializationInfo info, StreamingContext context, ISurrogateSelector selector)
                => throw new NotSupportedException();
        }

        private sealed class EntrySurrogate : ISerializationSurrogate
        {
            public void GetObjectData(object obj, SerializationInfo info, StreamingContext context)
            {
                var entry = (DelegateEntry)obj;
                info.FullTypeName = DelegateSerialization.EntryName;
                info.AssemblyName = DelegateSerialization.Mscorlib;
                info.AddValue("type", entry.type, typeof(string));
                info.AddValue("assembly", entry.assembly, typeof(string));
                info.AddValue("target", entry.target, typeof(object));
                info.AddValue("targetTypeAssembly", entry.targetTypeAssembly, typeof(string));
                info.AddValue("targetTypeName", entry.targetTypeName, typeof(string));
                info.AddValue("methodName", entry.methodName, typeof(string));
                info.AddValue("delegateEntry", entry.delegateEntry, typeof(object));
            }

            public object SetObjectData(object obj, SerializationInfo info, StreamingContext context, ISurrogateSelector selector)
            {
                // Read by member name, as .NET Framework reads the entry's fields.
                var entry = (DelegateEntry)obj;
                foreach (SerializationEntry member in info)
                {
                    switch (member.Name)
                    {
                        case "type": entry.type = (string)member.Value; break;
                        case "assembly": entry.assembly = (string)member.Value; break;
                        case "target": entry.target = member.Value; break;
                        case "targetTypeAssembly": entry.targetTypeAssembly = (string)member.Value; break;
                        case "targetTypeName": entry.targetTypeName = (string)member.Value; break;
                        case "methodName": entry.methodName = (string)member.Value; break;
                        case "delegateEntry": entry.delegateEntry = (DelegateEntry)member.Value; break;
                    }
                }
                return entry;
            }
        }
    }

    // Binds .NET Framework's delegate holder and entry to the classes below; everything else to the caller's binder,
    // or to the formatter's own binding.
    internal sealed class DelegateBinder : SerializationBinder
    {
        internal DelegateBinder(SerializationBinder next)
        {
            Next = next;
        }

        internal SerializationBinder Next { get; }

        public override Type BindToType(string assemblyName, string typeName)
        {
            if (typeName == DelegateSerialization.HolderName && IsMscorlib(assemblyName))
                return typeof(DelegateHolder);
            if (typeName == DelegateSerialization.EntryName && IsMscorlib(assemblyName))
                return typeof(DelegateEntry);
            return Next?.BindToType(assemblyName, typeName);
        }

        public override void BindToName(Type serializedType, out string assemblyName, out string typeName)
        {
            if (Next != null)
            {
                Next.BindToName(serializedType, out assemblyName, out typeName);
                return;
            }
            assemblyName = null;
            typeName = null;
        }

        private static bool IsMscorlib(string assemblyName)
            => assemblyName != null && (assemblyName == "mscorlib" || assemblyName.StartsWith("mscorlib,", StringComparison.Ordinal));
    }

    // .NET Framework's System.DelegateSerializationHolder+DelegateEntry: the same fields, which the stream names.
    [Serializable]
    internal sealed class DelegateEntry
    {
        internal string type;
        internal string assembly;
        internal object target;
        internal string targetTypeAssembly;
        internal string targetTypeName;
        internal string methodName;
        internal DelegateEntry delegateEntry;
    }

    // What .NET Framework's System.DelegateSerializationHolder does when it is read: the delegate entries and methods
    // become the delegate (GetRealObject).
    [Serializable]
    internal sealed class DelegateHolder : ISerializable, IObjectReference
    {
        private readonly DelegateEntry entries;
        private readonly MethodInfo[] methods;

        private DelegateHolder(SerializationInfo info, StreamingContext context)
        {
            this.entries = (DelegateEntry)info.GetValue("Delegate", typeof(object));
            int count = 0;
            for (DelegateEntry entry = this.entries; entry != null; entry = entry.delegateEntry)
            {
                if (entry.target is string targetName)
                    entry.target = info.GetValue(targetName, typeof(object));
                count++;
            }
            var found = new MethodInfo[count];
            var names = new HashSet<string>(StringComparer.Ordinal);
            foreach (SerializationEntry member in info)
                names.Add(member.Name);
            for (int i = 0; i < count; i++)
            {
                if (!names.Contains("method" + i))
                    return;
                found[i] = (MethodInfo)info.GetValue("method" + i, typeof(object));
            }
            this.methods = found;
        }

        public void GetObjectData(SerializationInfo info, StreamingContext context)
            => throw new NotSupportedException("The DelegateSerializationHolder object is designed to transmit delegate information only.");

        public object GetRealObject(StreamingContext context)
        {
            var list = new List<DelegateEntry>();
            for (DelegateEntry entry = this.entries; entry != null; entry = entry.delegateEntry)
                list.Add(entry);
            if (list.Count == 1)
                return GetDelegate(list[0], 0);
            // The entries go from the last delegate of the invocation list to the first.
            var delegates = new Delegate[list.Count];
            for (int i = 0; i < list.Count; i++)
                delegates[list.Count - 1 - i] = GetDelegate(list[i], i);
            return Delegate.Combine(delegates);
        }

        private Delegate GetDelegate(DelegateEntry entry, int index)
        {
            try
            {
                if (string.IsNullOrEmpty(entry.methodName))
                    throw new SerializationException("Insufficient state to deserialize the object. Missing field 'MethodName'.");
                if (string.IsNullOrEmpty(entry.assembly))
                    throw new SerializationException("Insufficient state to deserialize the object. Missing field 'DelegateAssembly'.");
                if (string.IsNullOrEmpty(entry.targetTypeName))
                    throw new SerializationException("Insufficient state to deserialize the object. Missing field 'TargetTypeName'.");
                Type type = Type.GetType(entry.type + ", " + entry.assembly, true);
                Type targetType = Type.GetType(entry.targetTypeName + ", " + entry.targetTypeAssembly, true);
                if (this.methods != null)
                {
                    if (entry.target != null && !targetType.IsInstanceOfType(entry.target))
                        throw new InvalidCastException();
                    return entry.target == null
                        ? Delegate.CreateDelegate(type, this.methods[index])
                        : Delegate.CreateDelegate(type, entry.target, this.methods[index]);
                }
                if (entry.target != null)
                {
                    if (!targetType.IsInstanceOfType(entry.target))
                        throw new InvalidCastException();
                    return Delegate.CreateDelegate(type, entry.target, entry.methodName);
                }
                return Delegate.CreateDelegate(type, targetType, entry.methodName);
            }
            catch (Exception e) when (!(e is SerializationException))
            {
                throw new SerializationException(e.Message, e);
            }
        }
    }
}
