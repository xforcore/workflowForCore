// workflowForCore's own code (MIT). Licensed under the MIT license; see LICENSE.
//
// The shapes of .NET Framework's remoting messages that ExternalDataExchange uses, which .NET doesn't have. On .NET
// Framework, HandleExternalEvent's queue items are MethodMessages (System.Workflow.Activities.MethodMessage), which
// implement remoting's IMethodMessage and carry a LogicalCallContext holding the identity of the code that raised the
// event (IdentityContextData); HandleExternalEventActivity reads the arguments and the identity back through
// IMethodMessage. Nothing is sent through remoting: these are the members that MethodMessage implements and its readers
// call, with .NET Framework's names and signatures, internal to System.Workflow.Activities (docs/MODIFICATIONS.md,
// "Remoting").
using System.Collections;
using System.Collections.Generic;
using System.Reflection;

namespace System.Runtime.Remoting.Messaging
{
    internal interface IMessage
    {
        IDictionary Properties { get; }
    }

    internal interface IMethodMessage : IMessage
    {
        string Uri { get; }
        string MethodName { get; }
        string TypeName { get; }
        object MethodSignature { get; }
        int ArgCount { get; }
        string GetArgName(int index);
        object GetArg(int argNum);
        object[] Args { get; }
        bool HasVarArgs { get; }
        LogicalCallContext LogicalCallContext { get; }
        MethodBase MethodBase { get; }
    }

    // Marks call context data that flows with a call (on .NET Framework, across application domains too).
    internal interface ILogicalThreadAffinative
    {
    }

    // The data a message carries with it: named values, cloned with the message.
    [Serializable]
    internal sealed class LogicalCallContext : ICloneable
    {
        private Dictionary<string, object> data = new Dictionary<string, object>(StringComparer.Ordinal);

        public object GetData(string name)
        {
            return this.data.TryGetValue(name, out object value) ? value : null;
        }

        public void SetData(string name, object data)
        {
            this.data[name] = data;
        }

        public void FreeNamedDataSlot(string name)
        {
            this.data.Remove(name);
        }

        public bool HasInfo => this.data.Count > 0;

        public object Clone()
        {
            var clone = new LogicalCallContext();
            foreach (KeyValuePair<string, object> entry in this.data)
                clone.data.Add(entry.Key, entry.Value);
            return clone;
        }
    }
}
