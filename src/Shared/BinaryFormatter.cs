// workflowForCore's own code (MIT). Licensed under the MIT license; see LICENSE.
//
// The BinaryFormatter WF3 serializes with: workflow instance state (Activity.Save/Load, which the persistence services
// and Activity.Clone call), tracking data, and the arguments of local service calls and events, which it clones. .NET's
// BinaryFormatter throws, so WF3's files that create one name this class instead (a using alias, docs/MODIFICATIONS.md,
// "Serialization"). It is webForCore's port of .NET Framework's BinaryFormatter (WebFormsForCore.Serialization.Formatters,
// wire-compatible with .NET Framework's), as a System.Runtime.Serialization.IFormatter, the interface WF3's public
// methods take.
//
// It is internal, and compiled into each of the three assemblies.
using System.IO;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Formatters;
using Port = WebFormsForCore.Serialization.Formatters.Binary;

#pragma warning disable SYSLIB0011, SYSLIB0050, SYSLIB0051

namespace WorkflowForCore.Serialization
{
    internal sealed class BinaryFormatter : IFormatter
    {
        private readonly Port.BinaryFormatter formatter;

        public BinaryFormatter()
        {
            this.formatter = new Port.BinaryFormatter();
        }

        public BinaryFormatter(ISurrogateSelector selector, StreamingContext context)
        {
            this.formatter = new Port.BinaryFormatter(selector, context);
        }

        public ISurrogateSelector SurrogateSelector
        {
            get => this.formatter.SurrogateSelector;
            set => this.formatter.SurrogateSelector = value;
        }

        public SerializationBinder Binder
        {
            get => this.formatter.Binder;
            set => this.formatter.Binder = value;
        }

        public StreamingContext Context
        {
            get => this.formatter.Context;
            set => this.formatter.Context = value;
        }

        public FormatterAssemblyStyle AssemblyFormat
        {
            get => this.formatter.AssemblyFormat;
            set => this.formatter.AssemblyFormat = value;
        }

        public FormatterTypeStyle TypeFormat
        {
            get => this.formatter.TypeFormat;
            set => this.formatter.TypeFormat = value;
        }

        public TypeFilterLevel FilterLevel
        {
            get => this.formatter.FilterLevel;
            set => this.formatter.FilterLevel = value;
        }

        public object Deserialize(Stream serializationStream) => this.formatter.Deserialize(serializationStream);

        public void Serialize(Stream serializationStream, object graph) => this.formatter.Serialize(serializationStream, graph);

        // A formatter an application passes to Activity.Save or Activity.Load: .NET's BinaryFormatter, which throws, is
        // replaced by this one with the same settings, so that code written for .NET Framework, such as a persistence
        // service that saves instances with a new BinaryFormatter(), works unchanged. Any other formatter is used as it is.
        internal static IFormatter ForWorkflow(IFormatter formatter)
        {
            if (formatter is System.Runtime.Serialization.Formatters.Binary.BinaryFormatter net)
            {
                return new BinaryFormatter
                {
                    SurrogateSelector = net.SurrogateSelector,
                    Binder = net.Binder,
                    Context = net.Context,
                    AssemblyFormat = net.AssemblyFormat,
                    TypeFormat = net.TypeFormat,
                    FilterLevel = net.FilterLevel,
                };
            }
            return formatter;
        }
    }
}
